# Richer XML Doc Capture (v3.1): Design

## Goal

Surface the `<param>`, `<typeparam>`, `<returns>`, `<remarks>`, `<example>`, and `<exception>` elements from XML doc comments into the package map, so AI consumers receive the same structured guidance a human IntelliSense user gets, not just the one-line summary NuSpec.AI captures today.

## Motivation

Real-world inspection of EF Core 8.0.10 packed with NuSpec.AI showed the map captures only `<summary>`. The sibling `.xml` doc file, which the AI consumer doesn't get from a typical NuGet install (and which decompilation cannot recover), contains the parameter docs, return docs, and exception/remark text that are the highest-signal items for "call this API correctly on the first try."

A method like `JsonConvert.DeserializeObject<T>(string)` currently emits:

```json
"documentation": "Deserializes the JSON to the specified .NET type."
```

It should emit (when full docs are enabled):

```json
"documentation": "Deserializes the JSON to the specified .NET type.",
"docs": {
  "typeparams": { "T": "The type of the object to deserialize to." },
  "params": { "value": "The JSON to deserialize." },
  "returns": "The deserialized object from the JSON string. A null value is returned if the provided JSON is valid but represents a null value."
}
```

## Schema

Add an optional `docs` object to types and members. Keep the existing `documentation` string in place (still the bare summary) so v2-aware consumers continue to work.

### Member-level (and type-level) `docs` object

All fields optional; emitted only when present in the source XML.

| Field | Type | Source XML element |
|---|---|---|
| `summary` | string | `<summary>` (omitted on members because already in `documentation`; included on types when present and full docs enabled, for symmetry) |
| `params` | `{ name: string }` map | one entry per `<param name="...">` |
| `typeparams` | `{ name: string }` map | one entry per `<typeparam name="...">` |
| `returns` | string | `<returns>` |
| `remarks` | string | `<remarks>` |
| `example` | string | `<example>` |
| `exceptions` | array of `{ type, when }` | one entry per `<exception cref="T:...">` |

### `documentation` field: unchanged behavior

Stays as the bare summary text, no nested objects, no breaking change. Existing consumers reading only this field see no difference.

### `<see cref="..."/>` handling

Inside any captured doc text, `<see cref="T:Foo.Bar"/>` and `<see cref="M:Foo.Bar.Baz(System.Int32)"/>` are replaced with their bare symbol form (`Foo.Bar`, `Foo.Bar.Baz`). `<see langword="null"/>` becomes the literal word `null`. `<paramref name="x"/>` and `<typeparamref name="T"/>` become `x` and `T`. This is uniform across `summary`, `remarks`, `params`, `returns`, etc.

### Whitespace normalization

Same rule already used for summary: trim, collapse internal runs of whitespace to a single space. Applied uniformly to every captured field.

### `[AiDescription]` interaction

Unchanged: `[AiDescription("...")]` overrides the summary for the `documentation` field. The new `docs` object continues to populate from the XML, so an attribute can override the user-facing summary without losing structured `<param>`/`<returns>` text. If a future need emerges for attribute-based override of the structured fields, that's a separate enhancement.

## Configuration

New MSBuild property, opt-in:

```xml
<PropertyGroup>
  <NuSpecAiIncludeFullDocs>true</NuSpecAiIncludeFullDocs>
</PropertyGroup>
```

Default: `false`. When `false`, the map looks identical to v3.0 output. When `true`, every `docs` object that has at least one field gets emitted. Empty `docs` objects (e.g., a method with only a `<summary>`) are omitted entirely.

The CLI gains a matching flag: `--full-docs` (boolean), threaded through `Program.cs` to `ProjectAnalyzer.Analyze`.

## Format support

- **JSON / compact JSON / YAML**: emit the `docs` object as-is.
- **Ultra**: ultra is a positional minimal-tokens format. Adding a structured `docs` object would defeat its purpose. Decision: ultra ignores `--full-docs` entirely. Document this in the README's Output Formats section.

## Schema version

Bump `schemaVersion` from `2` to `3`. The `docs` object is additive (v2 consumers ignoring unknown fields keep working), but the bump is a clean signal for AI tooling that wants to opt into reading the new fields.

## Size impact

Estimate (Newtonsoft.Json, with `--full-docs`): map roughly 1.6–2.0× current size. EF Core: similar ratio, so ~7–8 MB raw / ~1.7 MB inside `.nupkg`. Opt-in is the right default for that reason: packages picking ultra or compact for size already won't see the bump.

## Testing

Three layers:

1. **Unit tests** (`tests/NuSpec.AI.Tool.Tests/`): in-memory `CSharpCompilation` with C# source containing rich XML doc comments. Assert the extracted `docs` object matches expected shape per field. Cover: `<param>`, `<typeparam>`, `<returns>`, `<remarks>`, `<example>`, `<exception>`, `<see cref>` rewrite, `<paramref>` rewrite, missing-element cases, all-empty case (no `docs` object emitted), `--full-docs=false` case (`docs` absent even when XML has the elements).

2. **Format tests**: JsonFormatter / CompactJsonFormatter / YamlFormatter emit the `docs` object correctly. UltraCompactFormatter ignores it.

3. **Integration test**: end-to-end `ProjectAnalyzer.Analyze` on a small fixture with an MSBuild property set, confirming the property propagates.

## Open questions (decided)

- **`<see href="..."/>` external links?** Drop the tag, keep the inner text or the URL. Implementation pick: keep the inner text if present, else the URL. Low priority; covered in implementation.
- **Inheritance via `<inheritdoc/>`?** Roslyn's `GetDocumentationCommentXml(expandIncludes: true)`, but inheritdoc resolution is hard to do correctly with multiple inheritance paths. Out of scope for v3.1; current behavior (literal `<inheritdoc/>` shows up as empty) is preserved. Document this as a known limitation.
- **CDATA blocks in `<example>`?** Strip the wrapper, keep the contents. Already implicit in `XElement.Value`.

## Out of scope

- Capturing private/internal members' docs (still public-only).
- Pretty-printing or syntax-highlighting of `<example>` code blocks.
- Markdown-rendering of `<remarks>`. Stays raw text after tag substitution.
- Full `<inheritdoc/>` resolution.

## Files affected

- `src/NuSpec.AI.Tool/Models/MemberInfo.cs`: add `Docs` property.
- `src/NuSpec.AI.Tool/Models/TypeInfo.cs`: add `Docs` property.
- `src/NuSpec.AI.Tool/Models/DocsInfo.cs`: **new** model class.
- `src/NuSpec.AI.Tool/Analysis/XmlDocParser.cs`: **new**, single home for XML→DocsInfo parsing and inline-tag rewriting (extract from inline code in `ApiSurfaceCollector`).
- `src/NuSpec.AI.Tool/Analysis/ApiSurfaceCollector.cs`: call `XmlDocParser` instead of inline summary-only logic.
- `src/NuSpec.AI.Tool/Analysis/ProjectAnalyzer.cs`: accept `includeFullDocs` parameter, plumb to collector.
- `src/NuSpec.AI.Tool/Program.cs`: add `--full-docs` flag.
- `src/NuSpec.AI/build/NuSpec.AI.targets`: pass `NuSpecAiIncludeFullDocs` MSBuild property to the CLI.
- `src/NuSpec.AI.Tool/Formats/UltraCompactFormatter.cs`: comment confirming it ignores `docs`.
- `src/NuSpec.AI/NuSpec.AI.csproj`: bump `<Version>` to `3.1.0`.
- Schema bump in `PackageMap.cs` constant from 2 → 3.
- Tests under `tests/NuSpec.AI.Tool.Tests/`.
- `README.md`, `NUGET_README.md`, `CLAUDE.md`: document the new property and flag, update version references.
