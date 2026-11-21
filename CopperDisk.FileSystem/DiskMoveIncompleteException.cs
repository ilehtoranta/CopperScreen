namespace CopperDisk.FileSystem;

/// <summary>The destination completed, but source removal did not finish or could not be confirmed.</summary>
public sealed class DiskMoveIncompleteException(DiskProgress destination, Exception cause,
    string message = "Copy saved, but source removal could not be confirmed. Destination files are retained. Check the source before retrying.")
    : IOException(message + " " + cause.Message, cause)
{
    public DiskProgress Destination { get; } = destination;
}
