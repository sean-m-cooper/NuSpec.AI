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
