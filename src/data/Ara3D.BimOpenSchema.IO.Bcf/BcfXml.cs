using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;

namespace Ara3D.BimOpenSchema.IO.Bcf;

/// <summary>The XML files of a BCF 3.0 container, as UTF-8 bytes without a byte-order mark and
/// with "\n" line breaks on every platform. Elements follow the order the XSDs fix.</summary>
internal static partial class BcfXml
{
    private static readonly XmlWriterSettings Settings = new()
    {
        Encoding = new UTF8Encoding(false),
        Indent = true,
        IndentChars = "  ",
        NewLineChars = "\n",
        NewLineHandling = NewLineHandling.Replace,
    };

    public static byte[] Version()
        => Write(w =>
        {
            w.WriteStartElement("Version");
            w.WriteAttributeString("VersionId", "3.0");
            w.WriteEndElement();
        });

    /// <summary>Lists every topic type, status, and priority the topics use, so a reader that
    /// checks values against the project's lists accepts them.</summary>
    public static byte[] Extensions(IReadOnlyList<BcfTopic> topics)
        => Write(w =>
        {
            w.WriteStartElement("Extensions");
            List(w, "TopicTypes", "TopicType", topics.Select(t => t.Type));
            List(w, "TopicStatuses", "TopicStatus", topics.Select(t => t.Status));
            List(w, "Priorities", "Priority", topics.Select(t => t.Priority));
            w.WriteEndElement();
        });

    public static byte[] Markup(BcfTopic topic, string author, DateTimeOffset created)
        => Write(w =>
        {
            w.WriteStartElement("Markup");
            w.WriteStartElement("Topic");
            w.WriteAttributeString("Guid", topic.Guid);
            w.WriteAttributeString("TopicType", topic.Type);
            w.WriteAttributeString("TopicStatus", topic.Status);
            w.WriteElementString("Title", topic.Title);
            Optional(w, "Priority", topic.Priority);
            w.WriteElementString("CreationDate", Date(created));
            w.WriteElementString("CreationAuthor", author);
            Optional(w, "Description", topic.Description);
            if (topic.HasViewpoint)
            {
                w.WriteStartElement("Viewpoints");
                w.WriteStartElement("ViewPoint");
                w.WriteAttributeString("Guid", topic.ViewpointGuid);
                w.WriteElementString("Viewpoint", BcfTopic.ViewpointFile);
                w.WriteEndElement();
                w.WriteEndElement();
            }
            w.WriteEndElement();
            w.WriteEndElement();
        });

    /// <summary>Selects the topic's elements, shows the rest of the model, and places the camera.
    /// A GlobalId in IFC's 22-character form is a component's IfcGuid; any other id (a Revit
    /// UniqueId, say) is its AuthoringToolId, since BCF rejects it as an IfcGuid.</summary>
    public static byte[] Visinfo(BcfTopic topic)
    {
        var camera = topic.Camera ?? throw new InvalidOperationException($"Topic '{topic.Title}' has no camera");
        return Write(w =>
        {
            w.WriteStartElement("VisualizationInfo");
            w.WriteAttributeString("Guid", topic.ViewpointGuid);
            w.WriteStartElement("Components");
            w.WriteStartElement("Selection");
            foreach (var id in topic.GlobalIds)
            {
                w.WriteStartElement("Component");
                if (IfcGuid().IsMatch(id))
                    w.WriteAttributeString("IfcGuid", id);
                else
                    w.WriteElementString("AuthoringToolId", id);
                w.WriteEndElement();
            }
            w.WriteEndElement();
            w.WriteStartElement("Visibility");
            w.WriteAttributeString("DefaultVisibility", "true");
            w.WriteEndElement();
            w.WriteEndElement();
            w.WriteStartElement("PerspectiveCamera");
            Vector(w, "CameraViewPoint", camera.ViewPoint);
            Vector(w, "CameraDirection", camera.Direction);
            Vector(w, "CameraUpVector", camera.Up);
            w.WriteElementString("FieldOfView", Number(camera.FieldOfView));
            w.WriteElementString("AspectRatio", Number(camera.AspectRatio));
            w.WriteEndElement();
            w.WriteEndElement();
        });
    }

    [GeneratedRegex("^[0-9A-Za-z_$]{22}$")]
    private static partial Regex IfcGuid();

    private static byte[] Write(Action<XmlWriter> body)
    {
        using var stream = new MemoryStream();
        using (var writer = XmlWriter.Create(stream, Settings))
        {
            writer.WriteStartDocument();
            body(writer);
            writer.WriteEndDocument();
        }
        return stream.ToArray();
    }

    private static void List(XmlWriter w, string list, string item, IEnumerable<string> values)
    {
        var distinct = values.Where(v => v.Length > 0).Distinct().ToList();
        if (distinct.Count == 0)
            return;
        w.WriteStartElement(list);
        foreach (var v in distinct)
            w.WriteElementString(item, v);
        w.WriteEndElement();
    }

    private static void Optional(XmlWriter w, string name, string value)
    {
        if (value.Length > 0)
            w.WriteElementString(name, value);
    }

    private static void Vector(XmlWriter w, string name, Vector3 v)
    {
        w.WriteStartElement(name);
        w.WriteElementString("X", Number(v.X));
        w.WriteElementString("Y", Number(v.Y));
        w.WriteElementString("Z", Number(v.Z));
        w.WriteEndElement();
    }

    /// <summary>Six decimals (a micrometre), with no negative zero, so output is stable.</summary>
    private static string Number(double value)
    {
        var rounded = Math.Round(value, 6);
        return (rounded == 0 ? 0 : rounded).ToString("0.######", CultureInfo.InvariantCulture);
    }

    private static string Date(DateTimeOffset date)
        => date.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
}
