# Transitive Map Discovery: Design

**Status:** Draft
**Date:** 2026-04-25
**Author:** Sean Cooper

## Problem

When a NuGet package `B` depends on package `A`, and both packages use NuSpec.AI, an AI assistant working on a project that consumes `B` cannot easily discover or read the package map for `A`. Today each package's `.nupkg` contains its own `ai/package-map.json`, but those maps stand in isolation: there is no link from `B`'s map to `A`'s map.

The result: an AI helping with code that uses `B` sees that `B.OrderRepository.GetById(...)` returns an `Acme.OrdersCore.Order`, but has no structured way to find the package map for `Acme.OrdersCore` and learn what `Order` looks like.

## Goal

When a NuSpec.AI-using package is packed, its `package-map.json` records its resolved direct package dependencies along with a flag indicating whether each dep itself ships a NuSpec.AI map. AI tools reading a package's map can recurse into any flagged dep's map by resolving the standard NuGet cache path.

## Non-goals

- **Inlining dep data into B's map.** Out of scope. The consumer always has packages restored locally; cache-walk is sufficient.
- **Self-contained `.nupkg` analysis.** A `.nupkg` viewed in isolation (e.g., on nuget.org's package explorer or in an air-gapped environment) does *not* need to provide detail about its dependencies. Edge case, intentionally not solved.
- **Transitive flattening.** B's map records direct deps only. AI tools recurse through each direct dep's own map to reach further levels of the graph.
- **Project-to-project (`<ProjectReference>`) linking.** Out of scope. ProjectReferences don't have a NuGet cache path.

## Approach

Pointer-only with cache-walk recursion. Each emitted `package-map.json` enriches its existing `dependencies.packageReferences` list to include the resolved version and a `hasNuSpecAiMap` flag for each direct ref. AI tools resolve the file path via NuGet's standard cache convention (`<NuGet packages root>/<id-lowercase>/<version>/ai/package-map.json`).

This requires no behavior change at consume time other than `dotnet restore` having run, which is universally true for any project an AI is helping with.

## Schema change (`schemaVersion: 2`)

The `dependencies.packageReferences` field changes from `string[]` to an array of objects.

**Before (v1):**
```json
"dependencies": {
  "packageReferences": ["Microsoft.EntityFrameworkCore", "Acme.OrdersCore"],
  "frameworkReferences": []
}
```

**After (v2):**
```json
"dependencies": {
  "packageReferences": [
    {
      "id": "Microsoft.EntityFrameworkCore",
      "version": "8.0.0",
      "hasNuSpecAiMap": false
    },
    {
      "id": "Acme.OrdersCore",
      "version": "1.2.0",
      "hasNuSpecAiMap": true
    }
  ],
  "frameworkReferences": []
}
```

### Field semantics

| Field | Type | Description |
|-------|------|-------------|
| `id` | `string` | Package id, case preserved as declared in the consumer's `.csproj`. |
| `version` | `string \| null` | Resolved version from `obj/project.assets.json`. `null` if assets file is missing (restore not run). |
| `hasNuSpecAiMap` | `bool` | `true` iff `<NuGet packages root>/<id-lowercase>/<version>/ai/package-map.json` exists at pack time on the machine running the pack. |

### Preserved behavior

- Deps with `PrivateAssets="all"` are still excluded.
- `frameworkReferences` unchanged (still a `string[]`).
- `schemaVersion` at the root bumps from `1` to `2`.

### Schema bump rationale

NuSpec.AI is at v2.0.0 and was open-sourced recently (~472 downloads). There is effectively no installed base of consumers reading v1 maps to break.

## CLI tool changes (`src/NuSpec.AI.Tool/`)

### `ProjectMetadata/AssetsReader.cs` (new)

Parses `obj/project.assets.json` and returns:
- The configured NuGet packages roots (`packageFolders` in declaration order).
- A map of `packageId` → resolved `version` for every package in the resolved graph.

Reuses the JSON parsing pattern already established in `ProjectAnalyzer.GetNuGetContentFiles`.

### `ProjectMetadata/CsprojReader.cs` (modified)

`ReadDependencies` continues to read declared `<PackageReference>` items from the `.csproj` (the source of truth for "is this a direct ref?"). Returns each declared ref along with a flag for `PrivateAssets="all"`.

### `ProjectMetadata/DependencyResolver.cs` (new)

Combines `CsprojReader` and `AssetsReader`. For each declared direct ref (excluding `PrivateAssets="all"`):

1. Look up the resolved version in the assets map.
2. For each configured packages root in order, check whether `<root>/<id-lowercase>/<version>/ai/package-map.json` exists. Stop on first hit.
3. Return a `DependencyInfo { Id, Version, HasNuSpecAiMap }`.

If the assets file is missing, return refs with `Version = null` and `HasNuSpecAiMap = false`.

### `Models/` (modified)

- `PackageMap.SchemaVersion` constant bumps to `2`.
- `PackageMap.Dependencies.PackageReferences` changes from `IReadOnlyList<string>` to `IReadOnlyList<DependencyInfo>`.
- New `DependencyInfo` record/class with `Id`, `Version` (nullable string), `HasNuSpecAiMap` (bool).

### `Analysis/ProjectAnalyzer.cs` (modified)

`Analyze(csprojPath)` swaps its current call to `CsprojReader.ReadDependencies` for `DependencyResolver.Resolve(csprojPath)`.

### Formatters

All four formatters need to emit the new shape:

- **`JsonFormatter`** and **`CompactJsonFormatter`**: System.Text.Json serializes the object array automatically once the model changes.
- **`YamlFormatter`**: YamlDotNet serializes objects natively.
- **`UltraCompactFormatter`**: needs a positional convention for the dep object. Match the existing ultra style; e.g., `id|version|1` per dep where the trailing field is `1`/`0` for the map flag. Exact convention chosen during implementation to match the rest of the ultra format.

## Edge cases

| Case | Behavior |
|------|----------|
| `project.assets.json` missing (restore not run) | Emit refs with `version: null, hasNuSpecAiMap: false`. Do not fail the pack. |
| Multiple `packageFolders` configured | Iterate in declaration order; first match wins. |
| Floating versions (e.g. `Version="1.*"`) | Use the resolved concrete version from `project.assets.json`. |
| `<ProjectReference>` items | Out of scope. Not in `packageReferences`; ignored as today. |
| `PrivateAssets="all"` deps | Excluded from output (preserves current behavior). NuSpec.AI itself never appears in consumer output. |
| Self-referencing dependency loop | Prevented by NuGet itself. Even if it occurred, only direct deps are emitted, so no loop forms in JSON. |
| Pack-time machine has the map but consumer's machine doesn't | `hasNuSpecAiMap: true` is a pack-time fact. AI tools reading B's map should attempt the file read and fall back gracefully if missing. No special handling required in the producer. |
| Id casing | NuGet v3 cache always lowercases the id segment in the folder layout. Cache check uses `id.ToLowerInvariant()`; emitted JSON preserves the declared casing. |

## Testing

Unit, schema/serialization, and integration coverage. All test compilations follow the existing pattern of building source from in-memory strings, with no fixture files on disk.

### Unit tests: `DependencyResolverTests.cs`

- `Resolves_DirectRef_ResolvedVersion_FromAssetsJson`: given a fake assets JSON string and a list of declared refs, returns the resolved version per ref.
- `MissingAssetsJson_ReturnsRefsWithNullVersion`: graceful degradation when restore hasn't run.
- `PrivateAssetsAll_RefIsExcluded`: preserves existing semantics.
- `HasMap_TrueWhenFileExists`: uses a temp directory acting as a fake NuGet cache.
- `HasMap_FalseWhenFileMissing`.
- `LowercasesIdSegment_ForCachePath`: explicitly tests the case-folding behavior.
- `FloatingVersion_UsesResolvedConcreteVersion`.
- `MultiplePackageFolders_FirstMatchWins`.

### Schema/serialization tests

- `SchemaVersion_Is_2`.
- `Json_DependencyReference_SerializesAsObject`: assert against an expected JSON snippet.
- `UltraCompact_DependencyReference_UsesPositionalForm`: assert against the new ultra layout.
- `Yaml_DependencyReference_SerializesAsObject`.
- `CompactJson_DependencyReference_SerializesAsObject`.

### Integration test

Extend `tests/SampleProject/` coverage, or add a `tests/SampleConsumer/` project that references `SampleProject` via a local source. Run `dotnet pack` and assert the emitted `ai/package-map.json` contains a `packageReferences` entry for `SampleProject` with `hasNuSpecAiMap: true` and the resolved version.

If wiring up a second sample project proves heavy, a CLI-level integration test that builds an in-memory compilation against a fake on-disk NuGet cache structure is an acceptable substitute.

### Recursion smoke test

One unit-level test simulating the AI walk: given B's emitted JSON pointing at A, verify that resolving the cache path lands on A's `package-map.json`. Documents the expected consumer pattern.

## Documentation updates

### `README.md`

- Bump `schemaVersion` mention from `1` to `2`.
- Update the "Schema Reference → Dependencies" table: `packageReferences` is now an object array with `id` (string), `version` (string, may be null), `hasNuSpecAiMap` (bool).
- Update the example `package-map.json` snippet to reflect the new shape.
- Add a "Transitive Map Discovery" subsection explaining: AI tools should resolve `<NuGet packages root>/<id-lower>/<version>/ai/package-map.json` for any direct dep flagged `hasNuSpecAiMap: true`, and recurse the same way through that dep's own `packageReferences` entries.

### `NUGET_README.md`

- Same `schemaVersion` and example-snippet updates.
- Keep consumer-facing readme short; no full schema reference.

### `CLAUDE.md`

- Update the "Conventions" section: `schemaVersion` is now `2`.
- Add a one-or-two-sentence note under "Architecture" explaining transitive map discovery.

## Versioning

This is a breaking schema change. Bump the package version to `3.0.0` (major) on release, per semver. Update the version in:
- `src/NuSpec.AI/NuSpec.AI.csproj` (`<Version>`)
- `README.md` Quick Start example
- `NUGET_README.md`

The release workflow already extracts the version from the git tag (`v3.0.0`).

## Open questions

None remaining at design time. Ultra-format positional layout will be chosen during implementation to match the existing ultra style.
