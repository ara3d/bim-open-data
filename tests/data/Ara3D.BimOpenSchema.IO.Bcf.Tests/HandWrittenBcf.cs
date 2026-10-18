using System.IO.Compression;
using System.Text;

namespace Ara3D.BimOpenSchema.IO.Bcf.Tests;

/// <summary>A BCF container assembled from XML text, for files other tools write in layouts this
/// library does not (BCF 2.1).</summary>
internal static class HandWrittenBcf
{
    public static MemoryStream Zip(params (string Name, string Xml)[] files)
    {
        var stream = new MemoryStream();
        using (var zip = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
            foreach (var (name, xml) in files)
            {
                using var entry = new StreamWriter(zip.CreateEntry(name).Open(), new UTF8Encoding(false));
                entry.Write(xml);
            }
        stream.Position = 0;
        return stream;
    }

    public const string TopicGuid = "11111111-2222-3333-4444-555555555555";
    public const string ViewpointFile = "viewpoint.bcfv";

    /// <summary>A BCF 2.1 viewpoint: a perspective camera with a field of view and no aspect ratio.</summary>
    public const string Viewpoint21 = """
        <?xml version="1.0" encoding="UTF-8"?>
        <VisualizationInfo Guid="aaaaaaaa-2222-3333-4444-555555555555">
          <PerspectiveCamera>
            <CameraViewPoint><X>1</X><Y>2</Y><Z>3</Z></CameraViewPoint>
            <CameraDirection><X>0</X><Y>1</Y><Z>0</Z></CameraDirection>
            <CameraUpVector><X>0</X><Y>0</Y><Z>1</Z></CameraUpVector>
            <FieldOfView>47.5</FieldOfView>
          </PerspectiveCamera>
        </VisualizationInfo>
        """;

    /// <summary>A BCF 2.1 markup: header files directly under Header, comments beside Topic, the
    /// viewpoint listed beside Topic. The second comment has no text.</summary>
    public const string Markup21 = """
        <?xml version="1.0" encoding="UTF-8"?>
        <Markup>
          <Header>
            <File IfcProject="0J$yPqHBD0JRK3SaFB4e2l">
              <Filename>duplex.ifc</Filename>
              <Date>2026-09-01T08:30:00Z</Date>
            </File>
          </Header>
          <Topic Guid="11111111-2222-3333-4444-555555555555" TopicType="Issue" TopicStatus="Open">
            <Title>Wall is too thin</Title>
            <CreationDate>2026-10-18T12:00:00Z</CreationDate>
            <CreationAuthor>ann</CreationAuthor>
          </Topic>
          <Comment Guid="c1111111-2222-3333-4444-555555555555">
            <Date>2026-10-18T12:30:00Z</Date>
            <Author>bob</Author>
            <Comment>Agreed</Comment>
          </Comment>
          <Comment Guid="c2222222-2222-3333-4444-555555555555">
            <Date>2026-10-18T13:00:00Z</Date>
            <Author>cy</Author>
          </Comment>
          <Viewpoints Guid="aaaaaaaa-2222-3333-4444-555555555555">
            <Viewpoint>viewpoint.bcfv</Viewpoint>
          </Viewpoints>
        </Markup>
        """;

    public static MemoryStream Container21()
        => Zip(($"{TopicGuid}/markup.bcf", Markup21), ($"{TopicGuid}/{ViewpointFile}", Viewpoint21));
}
