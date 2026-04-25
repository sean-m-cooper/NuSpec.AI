# Richer XML Doc Capture (v3.1) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Capture `<param>`, `<typeparam>`, `<returns>`, `<remarks>`, `<example>`, and `<exception>` elements from XML doc comments into a new optional `docs` object on each type/member, behind an opt-in `--full-docs` flag and `<NuSpecAiIncludeFullDocs>` MSBuild property.

**Architecture:** A new `XmlDocParser` class owns all XML-doc-comment parsing and inline-tag rewriting. `ApiSurfaceCollector` calls into it instead of inlining summary-only logic. A new `DocsInfo` model serializes the captured fields. The flag is plumbed through `Program.cs` → `ProjectAnalyzer.Analyze` → `ApiSurfaceCollector`. The MSBuild targets pass the property to the CLI as `--full-docs`. Schema bumps from 2 to 3.

**Tech Stack:** .NET 8.0, Roslyn (Microsoft.CodeAnalysis.CSharp), `System.Xml.Linq` (already used), `System.Text.Json`, xUnit.

---

## File Structure

**New files:**
- `src/NuSpec.AI.Tool/Models/DocsInfo.cs`: model for the `docs` object
- `src/NuSpec.AI.Tool/Analysis/XmlDocParser.cs`: XML doc parsing + inline-tag rewriting
- `tests/NuSpec.AI.Tool.Tests/Analysis/XmlDocParserTests.cs`: unit tests for the parser

**Modified files:**
- `src/NuSpec.AI.Tool/Models/MemberInfo.cs`: add optional `Docs` property
- `src/NuSpec.AI.Tool/Models/TypeInfo.cs`: add optional `Docs` property
- `src/NuSpec.AI.Tool/Models/PackageMap.cs`: bump default `SchemaVersion` from 2 to 3
- `src/NuSpec.AI.Tool/Analysis/ApiSurfaceCollector.cs`: accept `includeFullDocs`, delegate to `XmlDocParser`
- `src/NuSpec.AI.Tool/Analysis/ProjectAnalyzer.cs`: accept and forward `includeFullDocs`
- `src/NuSpec.AI.Tool/Program.cs`: parse `--full-docs` flag
- `src/NuSpec.AI/build/NuSpec.AI.targets`: pass `NuSpecAiIncludeFullDocs` to CLI
- `src/NuSpec.AI/NuSpec.AI.csproj`: bump `<Version>` to 3.1.0
- `README.md`, `NUGET_README.md`, `CLAUDE.md`: document the new feature
- Existing tests asserting `SchemaVersion == 2` → update to 3

---

## Task 1: DocsInfo model

**Files:**
- Create: `src/NuSpec.AI.Tool/Models/DocsInfo.cs`

- [ ] **Step 1: Write the model class**

```csharp
using System.Text.Json.Serialization;

namespace NuSpec.AI.Tool.Models;

public sealed class DocsInfo
{
    [JsonPropertyName("summary")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Summary { get; init; }

    [JsonPropertyName("params")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? Params { get; init; }

    [JsonPropertyName("typeparams")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyDictionary<string, string>? TypeParams { get; init; }

    [JsonPropertyName("returns")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Returns { get; init; }

    [JsonPropertyName("remarks")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Remarks { get; init; }

    [JsonPropertyName("example")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Example { get; init; }

    [JsonPropertyName("exceptions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public IReadOnlyList<DocsExceptionInfo>? Exceptions { get; init; }

    /// <summary>
    /// True when no fields are populated. Callers use this to decide whether to attach the object at all.
    /// </summary>
    [JsonIgnore]
    public bool IsEmpty =>
        Summary is null
        && (Params is null || Params.Count == 0)
        && (TypeParams is null || TypeParams.Count == 0)
        && Returns is null
        && Remarks is null
        && Example is null
        && (Exceptions is null || Exceptions.Count == 0);
}

public sealed class DocsExceptionInfo
{
    [JsonPropertyName("type")]
    public required string Type { get; init; }

    [JsonPropertyName("when")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? When { get; init; }
}
```

- [ ] **Step 2: Build to confirm compile**

Run: `dotnet build src/NuSpec.AI.Tool/NuSpec.AI.Tool.csproj`
Expected: succeeds (no callers yet, just a new type)

- [ ] **Step 3: Commit**

```bash
git add src/NuSpec.AI.Tool/Models/DocsInfo.cs
git commit -m "feat(model): add DocsInfo for richer XML doc capture (v3.1 schema)"
```

---

## Task 2: XmlDocParser: `<see>` / `<paramref>` rewriting

**Files:**
- Create: `src/NuSpec.AI.Tool/Analysis/XmlDocParser.cs`
- Create: `tests/NuSpec.AI.Tool.Tests/Analysis/XmlDocParserTests.cs`

- [ ] **Step 1: Write the failing tests**

```csharp
using NuSpec.AI.Tool.Analysis;

namespace NuSpec.AI.Tool.Tests.Analysis;

public class XmlDocParserTests
{
    [Fact]
    public void RewriteInline_SeeCref_StripsPrefix()
    {
        var xml = "Use <see cref=\"T:Foo.Bar\"/> for serialization.";
        Assert.Equal("Use Foo.Bar for serialization.", XmlDocParser.RewriteInline(xml));
    }

    [Fact]
    public void RewriteInline_SeeCrefMember_StripsPrefixAndArgs()
    {
        var xml = "See <see cref=\"M:Foo.Bar.Baz(System.Int32)\"/>.";
        Assert.Equal("See Foo.Bar.Baz.", XmlDocParser.RewriteInline(xml));
    }

    [Fact]
    public void RewriteInline_SeeLangwordNull_BecomesLiteralNull()
    {
        var xml = "Returns <see langword=\"null\"/> on failure.";
        Assert.Equal("Returns null on failure.", XmlDocParser.RewriteInline(xml));
    }

    [Fact]
    public void RewriteInline_Paramref_BecomesBareName()
    {
        var xml = "The <paramref name=\"value\"/> parameter must be non-null.";
        Assert.Equal("The value parameter must be non-null.", XmlDocParser.RewriteInline(xml));
    }

    [Fact]
    public void RewriteInline_Typeparamref_BecomesBareName()
    {
        var xml = "Where <typeparamref name=\"T\"/> is the type.";
        Assert.Equal("Where T is the type.", XmlDocParser.RewriteInline(xml));
    }

    [Fact]
    public void RewriteInline_SeeHrefWithText_KeepsInnerText()
    {
        var xml = "Read <see href=\"https://example.com\">the docs</see> for more.";
        Assert.Equal("Read the docs for more.", XmlDocParser.RewriteInline(xml));
    }

    [Fact]
    public void RewriteInline_SeeHrefEmpty_KeepsUrl()
    {
        var xml = "Read <see href=\"https://example.com\"/> for more.";
        Assert.Equal("Read https://example.com for more.", XmlDocParser.RewriteInline(xml));
    }

    [Fact]
    public void RewriteInline_CollapsesWhitespace()
    {
        var xml = "Text\n   with    multiple\tspaces.";
        Assert.Equal("Text with multiple spaces.", XmlDocParser.RewriteInline(xml));
    }
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test --filter XmlDocParserTests`
Expected: compile error, `XmlDocParser` does not exist.

- [ ] **Step 3: Implement XmlDocParser.RewriteInline**

```csharp
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace NuSpec.AI.Tool.Analysis;

internal static class XmlDocParser
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Rewrites a fragment of doc-comment text:
    /// strips XML element wrappers, replaces inline reference tags with their bare textual form,
    /// and normalizes whitespace.
    /// </summary>
    public static string RewriteInline(string xml)
    {
        // Wrap in a synthetic root so XElement parses fragments with multiple top-level nodes.
        var wrapped = "<r>" + xml + "</r>";
        XElement root;
        try
        {
            root = XElement.Parse(wrapped, LoadOptions.PreserveWhitespace);
        }
        catch
        {
            return Normalize(xml);
        }

        var sb = new System.Text.StringBuilder();
        AppendRewritten(root, sb);
        return Normalize(sb.ToString());
    }

    private static void AppendRewritten(XElement element, System.Text.StringBuilder sb)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    sb.Append(text.Value);
                    break;
                case XElement el:
                    AppendElement(el, sb);
                    break;
                case XCData cdata:
                    sb.Append(cdata.Value);
                    break;
            }
        }
    }

    private static void AppendElement(XElement el, System.Text.StringBuilder sb)
    {
        switch (el.Name.LocalName)
        {
            case "see":
            {
                var cref = el.Attribute("cref")?.Value;
                if (cref is not null)
                {
                    sb.Append(StripCref(cref));
                    return;
                }
                var langword = el.Attribute("langword")?.Value;
                if (langword is not null)
                {
                    sb.Append(langword);
                    return;
                }
                var href = el.Attribute("href")?.Value;
                if (href is not null)
                {
                    var inner = el.Value;
                    sb.Append(string.IsNullOrWhiteSpace(inner) ? href : inner);
                    return;
                }
                sb.Append(el.Value);
                return;
            }
            case "paramref":
            case "typeparamref":
                sb.Append(el.Attribute("name")?.Value ?? "");
                return;
            default:
                AppendRewritten(el, sb);
                return;
        }
    }

    private static string StripCref(string cref)
    {
        // Forms: "T:Foo.Bar", "M:Foo.Bar.Baz(System.Int32)", "P:Foo.Bar.Prop", "F:...", "E:...", "!:..."
        var idx = cref.IndexOf(':');
        var stripped = idx >= 0 ? cref[(idx + 1)..] : cref;
        var paren = stripped.IndexOf('(');
        if (paren >= 0) stripped = stripped[..paren];
        return stripped;
    }

    private static string Normalize(string s) => WhitespaceRegex.Replace(s, " ").Trim();
}
```

- [ ] **Step 4: Run tests to verify pass**

Run: `dotnet test --filter XmlDocParserTests`
Expected: 8/8 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/NuSpec.AI.Tool/Analysis/XmlDocParser.cs tests/NuSpec.AI.Tool.Tests/Analysis/XmlDocParserTests.cs
git commit -m "feat(parser): XmlDocParser inline tag rewriting (see/paramref/typeparamref)"
```

---

## Task 3: XmlDocParser: Parse method

**Files:**
- Modify: `src/NuSpec.AI.Tool/Analysis/XmlDocParser.cs`
- Modify: `tests/NuSpec.AI.Tool.Tests/Analysis/XmlDocParserTests.cs`

- [ ] **Step 1: Write the failing tests**

Append to `XmlDocParserTests.cs`:

```csharp
[Fact]
public void Parse_SummaryOnly_ReturnsSummary()
{
    var xml = "<doc><summary>The summary text.</summary></doc>";
    var docs = XmlDocParser.Parse(xml, fullDocs: true);
    Assert.Equal("The summary text.", docs!.Summary);
    Assert.Null(docs.Returns);
    Assert.Null(docs.Params);
}

[Fact]
public void Parse_FullDocs_CapturesAllFields()
{
    var xml = """
        <doc>
            <summary>Summary text.</summary>
            <typeparam name="T">The type.</typeparam>
            <param name="value">The value.</param>
            <param name="other">Another.</param>
            <returns>The result.</returns>
            <remarks>Some remarks.</remarks>
            <example>var x = 1;</example>
            <exception cref="T:System.ArgumentNullException">When null.</exception>
        </doc>
        """;
    var docs = XmlDocParser.Parse(xml, fullDocs: true)!;
    Assert.Equal("Summary text.", docs.Summary);
    Assert.Equal("The type.", docs.TypeParams!["T"]);
    Assert.Equal("The value.", docs.Params!["value"]);
    Assert.Equal("Another.", docs.Params["other"]);
    Assert.Equal("The result.", docs.Returns);
    Assert.Equal("Some remarks.", docs.Remarks);
    Assert.Equal("var x = 1;", docs.Example);
    var ex = Assert.Single(docs.Exceptions!);
    Assert.Equal("System.ArgumentNullException", ex.Type);
    Assert.Equal("When null.", ex.When);
}

[Fact]
public void Parse_FullDocsFalse_OnlyReturnsSummary()
{
    var xml = """
        <doc>
            <summary>Summary.</summary>
            <param name="x">X.</param>
            <returns>R.</returns>
        </doc>
        """;
    var docs = XmlDocParser.Parse(xml, fullDocs: false)!;
    Assert.Equal("Summary.", docs.Summary);
    Assert.Null(docs.Params);
    Assert.Null(docs.Returns);
}

[Fact]
public void Parse_Empty_ReturnsNull()
{
    Assert.Null(XmlDocParser.Parse("", fullDocs: true));
    Assert.Null(XmlDocParser.Parse("   ", fullDocs: true));
}

[Fact]
public void Parse_NoSummaryNoOtherFields_ReturnsNull()
{
    var xml = "<doc></doc>";
    Assert.Null(XmlDocParser.Parse(xml, fullDocs: true));
}

[Fact]
public void Parse_RewritesInlineTagsInAllFields()
{
    var xml = """
        <doc>
            <summary>Use <see cref="T:Foo"/>.</summary>
            <param name="x">The <paramref name="x"/> value.</param>
            <returns><see langword="null"/> on failure.</returns>
        </doc>
        """;
    var docs = XmlDocParser.Parse(xml, fullDocs: true)!;
    Assert.Equal("Use Foo.", docs.Summary);
    Assert.Equal("The x value.", docs.Params!["x"]);
    Assert.Equal("null on failure.", docs.Returns);
}

[Fact]
public void Parse_MalformedXml_ReturnsNull()
{
    Assert.Null(XmlDocParser.Parse("<doc><summary>Unclosed", fullDocs: true));
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test --filter XmlDocParserTests`
Expected: 7 new tests fail to compile (`Parse` doesn't exist).

- [ ] **Step 3: Implement XmlDocParser.Parse**

Add to `XmlDocParser.cs`:

```csharp
public static Models.DocsInfo? Parse(string xml, bool fullDocs)
{
    if (string.IsNullOrWhiteSpace(xml)) return null;

    XElement root;
    try
    {
        root = XElement.Parse(xml, LoadOptions.PreserveWhitespace);
    }
    catch
    {
        return null;
    }

    var summary = ExtractElement(root, "summary");

    if (!fullDocs)
    {
        return summary is null
            ? null
            : new Models.DocsInfo { Summary = summary };
    }

    var typeparams = ExtractNamed(root, "typeparam");
    var paramsMap = ExtractNamed(root, "param");
    var returns = ExtractElement(root, "returns");
    var remarks = ExtractElement(root, "remarks");
    var example = ExtractElement(root, "example");
    var exceptions = ExtractExceptions(root);

    var docs = new Models.DocsInfo
    {
        Summary = summary,
        Params = paramsMap.Count > 0 ? paramsMap : null,
        TypeParams = typeparams.Count > 0 ? typeparams : null,
        Returns = returns,
        Remarks = remarks,
        Example = example,
        Exceptions = exceptions.Count > 0 ? exceptions : null
    };
    return docs.IsEmpty ? null : docs;
}

private static string? ExtractElement(XElement root, string name)
{
    var el = root.Element(name);
    if (el is null) return null;
    var text = ExtractInner(el);
    return string.IsNullOrWhiteSpace(text) ? null : text;
}

private static Dictionary<string, string> ExtractNamed(XElement root, string name)
{
    var result = new Dictionary<string, string>(StringComparer.Ordinal);
    foreach (var el in root.Elements(name))
    {
        var key = el.Attribute("name")?.Value;
        if (string.IsNullOrEmpty(key)) continue;
        var text = ExtractInner(el);
        if (!string.IsNullOrWhiteSpace(text))
            result[key] = text;
    }
    return result;
}

private static List<Models.DocsExceptionInfo> ExtractExceptions(XElement root)
{
    var result = new List<Models.DocsExceptionInfo>();
    foreach (var el in root.Elements("exception"))
    {
        var cref = el.Attribute("cref")?.Value;
        if (string.IsNullOrEmpty(cref)) continue;
        var when = ExtractInner(el);
        result.Add(new Models.DocsExceptionInfo
        {
            Type = StripCref(cref),
            When = string.IsNullOrWhiteSpace(when) ? null : when
        });
    }
    return result;
}

private static string ExtractInner(XElement el)
{
    var sb = new System.Text.StringBuilder();
    AppendRewritten(el, sb);
    return Normalize(sb.ToString());
}
```

- [ ] **Step 4: Run all parser tests**

Run: `dotnet test --filter XmlDocParserTests`
Expected: 15/15 PASS.

- [ ] **Step 5: Commit**

```bash
git add src/NuSpec.AI.Tool/Analysis/XmlDocParser.cs tests/NuSpec.AI.Tool.Tests/Analysis/XmlDocParserTests.cs
git commit -m "feat(parser): XmlDocParser.Parse extracts structured doc fields"
```

---

## Task 4: Wire parser into ApiSurfaceCollector

**Files:**
- Modify: `src/NuSpec.AI.Tool/Analysis/ApiSurfaceCollector.cs`
- Modify: `src/NuSpec.AI.Tool/Models/MemberInfo.cs`
- Modify: `src/NuSpec.AI.Tool/Models/TypeInfo.cs`

- [ ] **Step 1: Add `Docs` to MemberInfo**

In `MemberInfo.cs`, after the `Documentation` property:

```csharp
[JsonPropertyName("docs")]
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public DocsInfo? Docs { get; init; }
```

- [ ] **Step 2: Add `Docs` to TypeInfo**

Same addition in `TypeInfo.cs`, after `Documentation`.

- [ ] **Step 3: Replace `ExtractDocumentation` with delegating implementation**

In `ApiSurfaceCollector.cs`, find `ExtractDocumentation` and replace it. Then update both call sites (type and member) to also extract and attach a `DocsInfo`.

The collector needs the `includeFullDocs` flag. Thread it through the constructor or as a method parameter; pick whichever matches the existing pattern. (Inspect the file; if the collector is currently static, add an instance with a constructor field; if already instance-based, add a constructor param. Show the actual diff in commit.)

Replacement for `ExtractDocumentation`:

```csharp
private string? ExtractDocumentation(ISymbol symbol)
{
    if (TryGetAiDescription(symbol, out var description))
        return description;

    var xml = symbol.GetDocumentationCommentXml();
    var docs = XmlDocParser.Parse(xml ?? "", fullDocs: false);
    return docs?.Summary;
}

private DocsInfo? ExtractDocs(ISymbol symbol)
{
    if (!_includeFullDocs) return null;
    var xml = symbol.GetDocumentationCommentXml();
    return XmlDocParser.Parse(xml ?? "", fullDocs: true);
}
```

When `[AiDescription]` is set, `Documentation` carries the override but `Docs` still gets populated from XML (per the spec: attribute overrides the user-facing summary, structured fields stay). At each TypeInfo and MemberInfo construction site, set `Docs = ExtractDocs(symbol)`.

- [ ] **Step 4: Run existing tests**

Run: `dotnet test`
Expected: all existing tests PASS (no behavior change because `_includeFullDocs` defaults to false where unset; current callers haven't been updated yet, so they pass false implicitly).

- [ ] **Step 5: Commit**

```bash
git add src/NuSpec.AI.Tool/Analysis/ApiSurfaceCollector.cs src/NuSpec.AI.Tool/Models/MemberInfo.cs src/NuSpec.AI.Tool/Models/TypeInfo.cs
git commit -m "feat(collector): delegate XML doc parsing to XmlDocParser, add Docs field"
```

---

## Task 5: Plumb `includeFullDocs` through ProjectAnalyzer + Program

**Files:**
- Modify: `src/NuSpec.AI.Tool/Analysis/ProjectAnalyzer.cs`
- Modify: `src/NuSpec.AI.Tool/Program.cs`
- Modify: `tests/NuSpec.AI.Tool.Tests/Analysis/ApiSurfaceCollectorTests.cs` (or wherever full-stack collector tests live; add one)

- [ ] **Step 1: Write the failing integration test**

Add to an appropriate existing test file (find the one that already builds an in-memory `CSharpCompilation` and analyzes it):

```csharp
[Fact]
public void Collector_FullDocsTrue_PopulatesDocsObject()
{
    var source = """
        namespace Foo;
        /// <summary>A widget.</summary>
        /// <remarks>Use carefully.</remarks>
        public class Widget
        {
            /// <summary>Spin it.</summary>
            /// <param name="speed">RPM.</param>
            /// <returns>Final state.</returns>
            public int Spin(int speed) => speed;
        }
        """;
    var compilation = /* helper that builds CSharpCompilation from source */;

    // Call the collector with includeFullDocs: true
    var types = ApiSurfaceCollector.Collect(compilation, includeFullDocs: true);

    var widget = types.Single(t => t.Name == "Widget");
    Assert.Equal("Use carefully.", widget.Docs!.Remarks);

    var spin = widget.Members.Single(m => m.Name == "Spin");
    Assert.Equal("RPM.", spin.Docs!.Params!["speed"]);
    Assert.Equal("Final state.", spin.Docs.Returns);
}

[Fact]
public void Collector_FullDocsFalse_OmitsDocsObject()
{
    var source = """
        namespace Foo;
        /// <summary>A widget.</summary>
        /// <remarks>Use carefully.</remarks>
        public class Widget {}
        """;
    var compilation = /* same helper */;
    var types = ApiSurfaceCollector.Collect(compilation, includeFullDocs: false);
    var widget = types.Single(t => t.Name == "Widget");
    Assert.Null(widget.Docs);
    Assert.Equal("A widget.", widget.Documentation);
}
```

- [ ] **Step 2: Run tests to verify failure**

Run: `dotnet test`
Expected: new tests fail to compile (collector signature doesn't accept `includeFullDocs` yet).

- [ ] **Step 3: Update collector entry point**

Wherever `ApiSurfaceCollector.Collect` (or equivalent) lives, add `bool includeFullDocs = false` to the public signature. Plumb the flag down.

- [ ] **Step 4: Update ProjectAnalyzer**

In `ProjectAnalyzer.cs`, locate `Analyze`. Add `bool includeFullDocs = false`. Forward to the collector.

- [ ] **Step 5: Add `--full-docs` flag to Program.cs**

In the argument-parsing switch, add:

```csharp
case "--full-docs":
    includeFullDocs = true;
    break;
```

Declare `bool includeFullDocs = false;` near the other parsed-option locals. Pass it into `ProjectAnalyzer.Analyze`. Add a help line:

```
  --full-docs         Capture <param>, <returns>, <remarks>, etc.
                      (default: only <summary>; increases map size)
```

- [ ] **Step 6: Run tests**

Run: `dotnet test`
Expected: all PASS, including the two new collector tests.

- [ ] **Step 7: Commit**

```bash
git add src/NuSpec.AI.Tool/Analysis/ProjectAnalyzer.cs src/NuSpec.AI.Tool/Analysis/ApiSurfaceCollector.cs src/NuSpec.AI.Tool/Program.cs tests/NuSpec.AI.Tool.Tests/
git commit -m "feat(cli): add --full-docs flag, plumb through analyzer + collector"
```

---

## Task 6: Schema version bump 2 → 3

**Files:**
- Modify: `src/NuSpec.AI.Tool/Models/PackageMap.cs`
- Modify: any tests asserting `SchemaVersion == 2`

- [ ] **Step 1: Bump default**

In `PackageMap.cs`:

```csharp
public int SchemaVersion { get; init; } = 3;
```

- [ ] **Step 2: Update existing tests**

Find every `Assert.Equal(2, map.SchemaVersion)` (e.g., in `EndToEndDependencyTests.cs`) and change to `3`.

Run: `grep -rn "SchemaVersion\|schemaVersion" tests/`

- [ ] **Step 3: Run tests**

Run: `dotnet test`
Expected: all PASS.

- [ ] **Step 4: Commit**

```bash
git add src/NuSpec.AI.Tool/Models/PackageMap.cs tests/
git commit -m "feat(schema): bump schemaVersion 2 -> 3 for v3.1 docs object"
```

---

## Task 7: MSBuild plumbing for `NuSpecAiIncludeFullDocs`

**Files:**
- Modify: `src/NuSpec.AI/build/NuSpec.AI.targets`

- [ ] **Step 1: Wire the property to the CLI invocation**

In `NuSpec.AI.targets`, modify the `Exec` line in `NuSpecAiGeneratePackageMaps`:

```xml
<PropertyGroup>
  <_NuSpecAiFullDocsArg Condition="'$(NuSpecAiIncludeFullDocs)' == 'true'"> --full-docs</_NuSpecAiFullDocsArg>
</PropertyGroup>

<Exec Command="dotnet &quot;$(_NuSpecAiToolPath)&quot; &quot;$(MSBuildProjectFullPath)&quot; --output &quot;$(_NuSpecAiOutputDirForCli)&quot; --formats &quot;$(NuSpecAiFormats)&quot;$(_NuSpecAiFullDocsArg)"
      ConsoleToMSBuild="true" />
```

The leading space inside `--full-docs` is intentional (only inserted when the property is on, otherwise the Exec line stays exactly as-is). Place the `<PropertyGroup>` definition inside the `NuSpecAiGeneratePackageMaps` target so it's evaluated at target-execute time.

- [ ] **Step 2: Verify the SampleProject still packs**

Run: from the repo root, `dotnet pack tests/SampleProject/SampleProject.csproj -c Release -o /tmp/v31-sample-test --no-restore` (use whatever local-feed setup the existing dev workflow uses).
Expected: packs successfully; `package-map.json` has `schemaVersion: 3` and no `docs` object (property defaults off).

- [ ] **Step 3: Verify opt-in works**

Re-pack with `-p:NuSpecAiIncludeFullDocs=true`. Expected: emitted `package-map.json` includes `docs` objects on at least one type/member that had richer XML doc comments.

- [ ] **Step 4: Commit**

```bash
git add src/NuSpec.AI/build/NuSpec.AI.targets
git commit -m "feat(msbuild): pass NuSpecAiIncludeFullDocs through as --full-docs"
```

---

## Task 8: Version bump and docs

**Files:**
- Modify: `src/NuSpec.AI/NuSpec.AI.csproj`
- Modify: `README.md`
- Modify: `NUGET_README.md`
- Modify: `CLAUDE.md`

- [ ] **Step 1: Bump version**

In `src/NuSpec.AI/NuSpec.AI.csproj`:

```xml
<Version>3.1.0</Version>
```

- [ ] **Step 2: Update Quick Start version refs**

`README.md` and `NUGET_README.md`: change `Version="3.0.1"` to `Version="3.1.0"` (or whatever the current latest is at plan-execute time).

- [ ] **Step 3: Add a "Richer docs" section to NUGET_README**

Insert after the existing Configuration section, before the Requirements section:

```markdown
### Capture full XML doc comments

By default, NuSpec.AI extracts the `<summary>` element from each type/member's XML doc comment. To also capture `<param>`, `<typeparam>`, `<returns>`, `<remarks>`, `<example>`, and `<exception>`:

\`\`\`xml
<PropertyGroup>
  <NuSpecAiIncludeFullDocs>true</NuSpecAiIncludeFullDocs>
</PropertyGroup>
\`\`\`

This populates a structured `docs` object on each type and member. The `documentation` field continues to carry the bare summary string for back-compat. Note: the `ultra` format ignores this property — it's a positional minimum-tokens format and stays summaries-only by design.

Expect the map to grow ~1.6–2× when enabled, depending on how much `<param>` / `<remarks>` text your library has.
```

- [ ] **Step 4: Update README.md schema reference**

In the schema-reference section of `README.md`, document the new `docs` object: mirror the spec's table of fields. Update schemaVersion example output to `3`.

- [ ] **Step 5: Update CLAUDE.md**

Bump schemaVersion in the Architecture summary to `3`. Add a bullet under Key Design Decisions noting the opt-in `<NuSpecAiIncludeFullDocs>` flag.

- [ ] **Step 6: Final test pass**

Run: `dotnet test NuSpec.AI.slnx`
Expected: all PASS.

- [ ] **Step 7: Commit**

```bash
git add src/NuSpec.AI/NuSpec.AI.csproj README.md NUGET_README.md CLAUDE.md
git commit -m "release: v3.1.0 — opt-in capture of full XML doc comments"
```

---

## Task 9: Real-world verification

**Files:** none; verification only.

- [ ] **Step 1: Re-pack Newtonsoft.Json with `--full-docs`**

Using the same local-feed setup used during planning (NuSpec.AI 3.1.0 in `/tmp/local-feed`), pack Newtonsoft.Json with `-p:NuSpecAiIncludeFullDocs=true`. Confirm:

- `package-map.json` size grows compared to summaries-only.
- At least one method has `docs.params`, `docs.returns`, `docs.remarks` populated.
- The `documentation` field on the same member is unchanged from v3.0 output.
- `schemaVersion` is `3`.

- [ ] **Step 2: Compare ultra format**

Confirm `package-map.ultra` size is unchanged (within rounding) regardless of the property, proving the format ignores `--full-docs`.

- [ ] **Step 3: Record numbers**

Update the Context Window Impact table in NUGET_README with a new "v3.1 full-docs" row for Newtonsoft.Json showing the actual bytes/tokens, so the size tradeoff is visible to readers. Commit.

```bash
git add NUGET_README.md
git commit -m "docs: add v3.1 full-docs measurements to Context Window Impact"
```

---

## Verification

After Task 9:

1. `dotnet build NuSpec.AI.slnx` and `dotnet test NuSpec.AI.slnx` both green.
2. New `XmlDocParser` has thorough unit coverage.
3. SampleProject pack with default property emits a v3 map identical-in-shape to v2 (just a higher `schemaVersion`).
4. SampleProject pack with `NuSpecAiIncludeFullDocs=true` emits `docs` objects.
5. Real-world Newtonsoft.Json pack confirms expected size delta and field population.
6. Documentation in all three readmes reflects the new property and the schema bump.
