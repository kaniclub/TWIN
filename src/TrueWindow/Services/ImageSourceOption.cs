namespace TrueWindow.Services;

internal sealed record ImageSourceOption(
    string SourceId,
    string DisplayName,
    string? FilePath,
    string? GameAssetName,
    bool IsBuiltIn);
