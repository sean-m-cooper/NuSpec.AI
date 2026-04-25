namespace NuSpec.AI.Tool.ProjectMetadata;

public sealed class DeclaredPackageReference
{
    public required string Id { get; init; }
    public required bool IsPrivateAssetsAll { get; init; }
}
