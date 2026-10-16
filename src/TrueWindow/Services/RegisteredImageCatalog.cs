namespace TrueWindow.Services;

// 他MODの画像は起動時に登録し、描画中はファイルを探索しない。
internal sealed class RegisteredImageCatalog
{
    private readonly List<ImageSourceOption> options = new();
    public IReadOnlyList<ImageSourceOption> Options => options;

    public void Register(string modId, string imageId, string displayName, string filePath)
    {
        if (string.IsNullOrWhiteSpace(modId) || string.IsNullOrWhiteSpace(imageId)
            || string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Image registration requires a mod ID, image ID and display name.");
        if (!Path.IsPathFullyQualified(filePath))
            throw new ArgumentException("Image file path must be absolute.", nameof(filePath));

        // 保存するIDにインストール先を含めず、移動・再導入後も選択を維持する。
        string sourceId = $"mod:{modId}:{imageId.Replace('\\', '/')}";
        ImageSourceOption option = new(sourceId, displayName, Path.GetFullPath(filePath), null, false);
        int index = options.FindIndex(existing => string.Equals(existing.SourceId, sourceId, StringComparison.OrdinalIgnoreCase));
        if (index >= 0) options[index] = option;
        else options.Add(option);
    }
}
