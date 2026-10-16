using StardewModdingAPI;
using TrueWindow.Models;

// ----------------------------
// ロケーション共有の表示設定を扱うサービス
// ----------------------------
namespace TrueWindow.Services;

internal sealed class TrueWindowLocationDisplayService
{
    // ロケーション表示の選択肢ID
    internal const string LocationOptionSourceId = "location";

    // 組み込み画像の選択肢ID接頭辞
    private const string BuiltInImagePrefix = "builtin:";

    // 常時選択可能な組み込み画像
    private static readonly (string SourceId, string TranslationKey, string GameAssetName)[] BuiltInImageOptions =
    {
        (BuiltInImagePrefix + "stardewPanorama.png", "menu.value.mountain", "LooseSprites/stardewPanorama"),
        (BuiltInImagePrefix + "Cloudy_Ocean_BG.png", "menu.value.ocean", "LooseSprites/Cloudy_Ocean_BG"),
        (BuiltInImagePrefix + "Cloudy_Ocean_BG_Night.png", "menu.value.night_sky", "LooseSprites/Cloudy_Ocean_BG_Night"),
    };

    private readonly IModHelper helper;
    private readonly TrueWindowInstanceRepository instanceRepository;
    private readonly RegisteredImageCatalog registeredImages;

    public TrueWindowLocationDisplayService(IModHelper helper, TrueWindowInstanceRepository instanceRepository, RegisteredImageCatalog registeredImages)
    {
        this.helper = helper;
        this.instanceRepository = instanceRepository;
        this.registeredImages = registeredImages;
    }

    // ロケーション共有設定を取得または生成する
    public TrueWindowRuntimeSettings GetOrCreateRuntimeSettings(string locationName, TrueWindowDisplayDefaults defaults, string? preferredSpecifiedImageSourceId)
    {
        return this.instanceRepository.GetOrCreate(locationName, () => new TrueWindowRuntimeSettings
        {
            ViewSettings = defaults.ViewSettings.Clone(),
            DisplaySourceMode = string.IsNullOrWhiteSpace(preferredSpecifiedImageSourceId)
                ? TrueWindowDisplaySourceMode.Location
                : TrueWindowDisplaySourceMode.SpecifiedImage,
            SelectedImageSourceId = preferredSpecifiedImageSourceId,
        });
    }

    // ロケーション共有設定を更新する
    public void UpdateRuntimeSettings(string locationName, TrueWindowRuntimeSettings settings)
    {
        this.instanceRepository.Update(locationName, settings);
    }

    // メニュー表示用に設定内容を正規化する
    public void NormalizeRuntimeSettingsSelection(TrueWindowRuntimeSettings runtimeSettings, List<ImageSourceOption> imageOptions, string? preferredSpecifiedImageSourceId)
    {
        TrueWindowResolvedDisplaySource resolved = this.ResolveDisplaySource(runtimeSettings, imageOptions, preferredSpecifiedImageSourceId);
        runtimeSettings.DisplaySourceMode = resolved.Mode;
        runtimeSettings.SelectedImageSourceId = resolved.Mode == TrueWindowDisplaySourceMode.SpecifiedImage
            ? resolved.SelectedImageSourceId
            : null;
    }

    // 実際に使用する表示元を決める
    public TrueWindowResolvedDisplaySource ResolveDisplaySource(
        TrueWindowRuntimeSettings runtimeSettings,
        List<ImageSourceOption> imageOptions,
        string? preferredSpecifiedImageSourceId)
    {
        string? selectedImageSourceId = this.ResolveSelectedImageSourceId(runtimeSettings, imageOptions, preferredSpecifiedImageSourceId);
        if (runtimeSettings.DisplaySourceMode == TrueWindowDisplaySourceMode.Location)
        {
            return new TrueWindowResolvedDisplaySource(TrueWindowDisplaySourceMode.Location, null);
        }

        if (runtimeSettings.DisplaySourceMode == TrueWindowDisplaySourceMode.SpecifiedImage && !string.IsNullOrWhiteSpace(selectedImageSourceId))
        {
            return new TrueWindowResolvedDisplaySource(TrueWindowDisplaySourceMode.SpecifiedImage, selectedImageSourceId);
        }

        if (runtimeSettings.DisplaySourceMode == TrueWindowDisplaySourceMode.Auto && !string.IsNullOrWhiteSpace(selectedImageSourceId))
        {
            return new TrueWindowResolvedDisplaySource(TrueWindowDisplaySourceMode.SpecifiedImage, selectedImageSourceId);
        }

        return new TrueWindowResolvedDisplaySource(TrueWindowDisplaySourceMode.Location, null);
    }

    // 同一ロケーションで共有する指定画像候補一覧を作る
    public List<ImageSourceOption> BuildImageSourceOptions(IEnumerable<TrueWindowDisplayDefaults> defaults)
    {
        List<ImageSourceOption> options = new()
        {
            new(LocationOptionSourceId, this.helper.Translation.Get("menu.value.location").ToString(), null, null, false),
        };

        foreach ((string sourceId, string translationKey, string gameAssetName) in BuiltInImageOptions)
        {
            options.Add(new ImageSourceOption(
                sourceId,
                this.helper.Translation.Get(translationKey).ToString(),
                null,
                gameAssetName,
                true));
        }

        foreach (TrueWindowDisplayDefaults entry in defaults)
        {
            if (string.IsNullOrWhiteSpace(entry.SpecifiedImagePath))
            {
                continue;
            }

            string filePath = entry.SpecifiedImagePath;
            if (options.Any(option => string.Equals(option.SourceId, filePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            options.Add(new ImageSourceOption(
                filePath,
                this.helper.Translation.Get("menu.value.specified_image").ToString(),
                filePath,
                null,
                false));
        }

        options.AddRange(this.registeredImages.Options);
        return options;
    }

    // 同一ロケーションで既定にする指定画像ソースIDを決める
    public string? GetPreferredSpecifiedImageSourceId(IEnumerable<TrueWindowDisplayDefaults> defaults, List<ImageSourceOption> imageOptions)
    {
        string? preferredFilePath = defaults
            .Select(static entry => entry.SpecifiedImagePath)
            .FirstOrDefault(static path => !string.IsNullOrWhiteSpace(path));

        if (string.IsNullOrWhiteSpace(preferredFilePath))
        {
            return null;
        }

        return imageOptions.Any(option => string.Equals(option.SourceId, preferredFilePath, StringComparison.OrdinalIgnoreCase))
            ? preferredFilePath
            : null;
    }

    // 実行時設定から選択すべき画像ソースIDを解決する
    private string? ResolveSelectedImageSourceId(TrueWindowRuntimeSettings runtimeSettings, List<ImageSourceOption> imageOptions, string? preferredSpecifiedImageSourceId)
    {
        string? candidate = runtimeSettings.DisplaySourceMode switch
        {
            TrueWindowDisplaySourceMode.SpecifiedImage => runtimeSettings.SelectedImageSourceId,
            TrueWindowDisplaySourceMode.Auto => runtimeSettings.SelectedImageSourceId ?? preferredSpecifiedImageSourceId,
            _ => null,
        };

        if (!string.IsNullOrWhiteSpace(candidate)
            && imageOptions.Any(option => string.Equals(option.SourceId, candidate, StringComparison.OrdinalIgnoreCase)))
        {
            return candidate;
        }

        if (!string.IsNullOrWhiteSpace(preferredSpecifiedImageSourceId)
            && imageOptions.Any(option => string.Equals(option.SourceId, preferredSpecifiedImageSourceId, StringComparison.OrdinalIgnoreCase)))
        {
            return preferredSpecifiedImageSourceId;
        }

        return null;
    }
}
