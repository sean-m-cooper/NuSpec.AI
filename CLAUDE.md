# NuSpec.AI

## What This Project Is

NuSpec.AI is a NuGet package that automatically generates `ai/package-map.json` during `dotnet pack`. The JSON describes a package's public API surface — types, members, signatures, XML doc comments, and inferred semantic roles — so AI coding assistants get instant structured context about any package that includes it.

## Architecture

- **`src/NuSpec.AI.Tool/`** — .NET 8.0 console app (the CLI tool). Uses Roslyn's `CSharpCompilation` and symbol API to analyze C# projects in a single namespace walk. No syntax tree ancestor traversal — everything comes from `INamedTypeSymbol` directly.
- **`src/NuSpec.AI/`** — Packaging-only project. Ships the CLI tool + MSBuild `.props`/`.targets` as a NuGet package. Not included in `dotnet build` (has `NoBuild=true`); only used for `dotnet pack`.
- **`tests/NuSpec.AI.Tool.Tests/`** — xUnit tests. Tests use in-memory `CSharpCompilation` instances built from C# source strings — no fixture files on disk.
- **`tests/SampleProject/`** — Integration test project that references NuSpec.AI locally to verify end-to-end `dotnet pack` behavior.

## Key Design Decisions

- **Symbol-based analysis, not syntax walking.** `ApiSurfaceCollector` walks `compilation.GlobalNamespace` recursively. Partial classes merge automatically, base types are resolved, doc comments come from `GetDocumentationCommentXml()`, and role inference happens in the same pass.
- **Filters to project sources only.** Types from metadata references (System.*, etc.) are excluded by checking `DeclaringSyntaxReferences` against the compilation's syntax trees.
- **MSBuild integration uses `TargetsForTfmSpecificContentInPackage`.** This is the official NuGet SDK extensibility point. Earlier attempts with `None Pack="true"` and `_PackageFiles` added dynamically in targets did not work — items added in targets run too late for NuGet's static item collection.

## Build Commands

```bash
# Build and test
dotnet build NuSpec.AI.slnx
dotnet test NuSpec.AI.slnx

# Create the NuGet package
dotnet publish src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj -c Release -o src/NuSpec.AI/tools/net8.0 --no-self-contained
dotnet pack src/NuSpec.AI/NuSpec.AI.csproj -c Release -o artifacts --no-build

# Run the CLI tool directly against any .csproj
dotnet run --project src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj -- path/to/Some.csproj
```

## Version Management

- Version is set in `src/NuSpec.AI/NuSpec.AI.csproj` (`<Version>`)
- Also referenced in `README.md` and `NUGET_README.md` — update all three when bumping
- GitHub Actions publish workflow extracts version from git tags (`v1.0.1` → `1.0.1`) and overrides the .csproj version

## Release Process

```bash
# 1. Update version in src/NuSpec.AI/NuSpec.AI.csproj, README.md, NUGET_README.md
# 2. Commit and push
# 3. Tag and push — GitHub Actions handles the rest
git tag v1.x.x
git push origin v1.x.x
```

## Conventions

- JSON output file is always `ai/package-map.json` inside the .nupkg
- `schemaVersion` in the JSON is `1` — increment on breaking schema changes
- Role inference is heuristic-based; a type can have multiple roles
- `documentation` fields are omitted from JSON when no XML doc comment exists (not null)
- `NUGET_README.md` is the consumer-facing readme for nuget.org; `README.md` is for GitHub

## Planned Enhancements

- `[AiRole("...")]` attribute for explicit role tagging
- `[AiIgnore]` attribute for type/member exclusion
- MSBuild property-based exclusions (`<NuSpecAiExcludeNamespaces>`, `<NuSpecAiExcludeTypes>`)
