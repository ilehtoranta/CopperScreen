namespace CopperScreen;

internal static class CopperStartMetadata
{
	public static string DisplayVersion => "CopperStart " + CopperScreenCopperStartRom.Version;
	public static string GitSha => CopperScreenCopperStartRom.ProducerCommit;
}
