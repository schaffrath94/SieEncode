using System.Xml.Linq;

namespace RFiDGear.Builder.RfPrj;

public static class RfPrjSemanticComparer
{
    private static readonly HashSet<string> PathElements = new(StringComparer.Ordinal)
    {
        "DesfireDataFilePath", "DesfireReadDataFilePath"
    };

    public static RfPrjComparison Compare(RfPrjDocument expected, RfPrjDocument actual)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(actual);
        if (expected.ManifestVersion != actual.ManifestVersion) return new(false, -1, "ManifestVersion");
        if (expected.Tasks.Count != actual.Tasks.Count) return new(false, -1, "TaskCount");
        for (var index = 0; index < expected.Tasks.Count; index++)
        {
            var left = Normalize(expected.Tasks[index]);
            var right = Normalize(actual.Tasks[index]);
            if (XNode.DeepEquals(left, right)) continue;
            var differingElement = FindDifference(left, right);
            return new(false, index, differingElement);
        }
        return new(true, -1, "");
    }

    private static XElement Normalize(XElement source)
    {
        var clone = new XElement(source);
        foreach (var node in clone.DescendantNodes().OfType<XText>().Where(text => string.IsNullOrWhiteSpace(text.Value)).ToArray()) node.Remove();
        foreach (var element in clone.DescendantsAndSelf().Where(element => PathElements.Contains(element.Name.LocalName)))
            if (!string.IsNullOrWhiteSpace(element.Value)) element.Value = Path.GetFileName(element.Value);
        return clone;
    }

    private static string FindDifference(XElement left, XElement right)
    {
        var leftElements = left.DescendantsAndSelf().ToArray();
        var rightElements = right.DescendantsAndSelf().ToArray();
        var count = Math.Min(leftElements.Length, rightElements.Length);
        for (var index = 0; index < count; index++)
        {
            if (leftElements[index].Name != rightElements[index].Name) return $"XML:{leftElements[index].Name.LocalName}/{rightElements[index].Name.LocalName}";
            if (!leftElements[index].HasElements && !rightElements[index].HasElements && leftElements[index].Value != rightElements[index].Value)
                return leftElements[index].Name.LocalName;
        }
        return "XMLStructure";
    }
}

public sealed record RfPrjComparison(bool Equivalent, int TaskPosition, string DifferingElement);
