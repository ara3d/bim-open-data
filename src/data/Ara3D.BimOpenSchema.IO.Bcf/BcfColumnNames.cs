namespace Ara3D.BimOpenSchema.IO.Bcf;

/// <summary>The names of the issues-table columns beyond <see cref="IssueColumns"/>, shared by
/// <see cref="BcfIssueTable.ToBcfIssues"/> (which reads them) and <see cref="BcfReader.ToTable"/>
/// (which writes them). The camera columns are a BCF perspective camera in model coordinates:
/// position, view direction, up vector, vertical field of view in degrees, and width over height.</summary>
public static class BcfColumnNames
{
    public const string Comment = "Comment";
    /// <summary>The topic's author and creation date (<see cref="BcfReader.ToTable"/> writes them;
    /// <see cref="BcfIssueTable.ToBcfIssues"/> ignores them).</summary>
    public const string Author = "Author";
    public const string CreationDate = "CreationDate";
    /// <summary>The author and date of the comment in <see cref="Comment"/> (read by
    /// <see cref="BcfIssueTable.ToBcfIssues"/>, written by <see cref="BcfReader.CommentsTable"/>).</summary>
    public const string CommentAuthor = "CommentAuthor";
    public const string CommentDate = "CommentDate";
    public const string TopicGuid = "TopicGuid";
    public const string Comments = "Comments";
    public const string CameraX = "CameraX";
    public const string CameraY = "CameraY";
    public const string CameraZ = "CameraZ";
    public const string DirectionX = "DirectionX";
    public const string DirectionY = "DirectionY";
    public const string DirectionZ = "DirectionZ";
    public const string UpX = "UpX";
    public const string UpY = "UpY";
    public const string UpZ = "UpZ";
    public const string FieldOfView = "FieldOfView";
    public const string AspectRatio = "AspectRatio";
}
