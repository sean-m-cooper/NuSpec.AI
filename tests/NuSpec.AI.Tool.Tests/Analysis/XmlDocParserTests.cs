using NuSpec.AI.Tool.Analysis;
using NuSpec.AI.Tool.Models;

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
}
