using System.Text;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using NuSpec.AI.Tool.Models;

namespace NuSpec.AI.Tool.Analysis;

public static class XmlDocParser
{
    private static readonly Regex WhitespaceRegex = new(@"\s+", RegexOptions.Compiled);

    /// <summary>
    /// Rewrites a fragment of doc-comment text:
    /// strips XML element wrappers, replaces inline reference tags with their bare textual form,
    /// and normalizes whitespace.
    /// </summary>
    public static string RewriteInline(string xml)
    {
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

        var sb = new StringBuilder();
        AppendRewritten(root, sb);
        return Normalize(sb.ToString());
    }

    private static void AppendRewritten(XElement element, StringBuilder sb)
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
            }
        }
    }

    private static void AppendElement(XElement el, StringBuilder sb)
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

    public static DocsInfo? Parse(string xml, bool fullDocs)
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
                : new DocsInfo { Summary = summary };
        }

        var typeparams = ExtractNamed(root, "typeparam");
        var paramsMap = ExtractNamed(root, "param");
        var returns = ExtractElement(root, "returns");
        var remarks = ExtractElement(root, "remarks");
        var example = ExtractElement(root, "example");
        var exceptions = ExtractExceptions(root);

        var docs = new DocsInfo
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

    private static List<DocsExceptionInfo> ExtractExceptions(XElement root)
    {
        var result = new List<DocsExceptionInfo>();
        foreach (var el in root.Elements("exception"))
        {
            var cref = el.Attribute("cref")?.Value;
            if (string.IsNullOrEmpty(cref)) continue;
            var when = ExtractInner(el);
            result.Add(new DocsExceptionInfo
            {
                Type = StripCref(cref),
                When = string.IsNullOrWhiteSpace(when) ? null : when
            });
        }
        return result;
    }

    private static string ExtractInner(XElement el)
    {
        var sb = new StringBuilder();
        AppendRewritten(el, sb);
        return Normalize(sb.ToString());
    }
}
