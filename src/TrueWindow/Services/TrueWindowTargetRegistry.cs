using System.Text.Json;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Objects;
using TrueWindow.Formats;
using TrueWindow.Models;

// ----------------------------
// TrueWindow 対象定義の管理サービス
// ----------------------------
namespace TrueWindow.Services;

internal sealed class TrueWindowTargetRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
    };

    private readonly IModHelper helper;
    private readonly List<ITrueWindowFormatReader> readers = new()
    {
        new TrueWindowFormat100Reader(),
    };

    private readonly Dictionary<string, TrueWindowTargetDefinition> byTrueWindowId = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, TrueWindowMapTargetDefinition> mapTargetsById = new(StringComparer.OrdinalIgnoreCase);

    // レジストリを初期化する
    public TrueWindowTargetRegistry(IModHelper helper)
    {
        this.helper = helper;
    }

    // 対象定義を再読み込みする
    public void Reload()
    {
        this.byTrueWindowId.Clear();
        this.mapTargetsById.Clear();

        foreach ((string sourceName, string rawJson, string packDirectory) in this.DiscoverDefinitionSources())
        {
            this.LoadDefinitionSource(sourceName, rawJson, packDirectory);
        }
    }

    // 家具から対象定義を解決する
    public bool TryResolve(Furniture furniture, out TrueWindowTargetDefinition? definition)
    {
        definition = null;

        if (!this.HasTrueWindowTag(furniture))
        {
            return false;
        }

        string presetId = this.GetTrueWindowIdFromContextTags(furniture)
            ?? this.NormalizeItemId(furniture.ItemId);

        this.byTrueWindowId.TryGetValue(presetId, out TrueWindowTargetDefinition? preset);
        definition = TrueWindowTargetDefinition.MergeWithDefaults(presetId, preset);
        return true;
    }


    // 現在ロケーションへ適用できる MAP 対象定義を返す
    public IReadOnlyList<TrueWindowMapTargetDefinition> GetMapTargetsFor(GameLocation location)
    {
        return this.mapTargetsById.Values
            .Where(static target => target.Enabled)
            .Where(target => target.Matches(location))
            .ToList();
    }

    // content pack から定義 JSON を列挙する
    private IEnumerable<(string SourceName, string RawJson, string PackDirectory)> DiscoverDefinitionSources()
    {
        foreach (IModInfo mod in this.helper.ModRegistry.GetAll())
        {
            if (!this.IsSupportedContentPack(mod.Manifest))
            {
                continue;
            }

            string? packDirectory = mod.GetType().GetProperty("DirectoryPath")?.GetValue(mod) as string;
            if (string.IsNullOrWhiteSpace(packDirectory))
            {
                continue;
            }

            string contentJsonPath = Path.Combine(packDirectory, "content.json");
            if (File.Exists(contentJsonPath))
            {
                string rawContent = File.ReadAllText(contentJsonPath);
                ContentPatcherRoot? root = JsonSerializer.Deserialize<ContentPatcherRoot>(rawContent, JsonOptions);
                if (root?.TrueWindow is JsonElement inlineSection && inlineSection.ValueKind != JsonValueKind.Undefined && inlineSection.ValueKind != JsonValueKind.Null)
                {
                    yield return ($"{mod.Manifest.UniqueID}:content.json#TrueWindow", inlineSection.GetRawText(), packDirectory);
                }
            }

            foreach (string file in this.GetFallbackDefinitionFiles(packDirectory))
            {
                yield return ($"{mod.Manifest.UniqueID}:{Path.GetFileName(file)}", File.ReadAllText(file), packDirectory);
            }
        }
    }

    // 既定位置の定義ファイル候補を返す
    private IEnumerable<string> GetFallbackDefinitionFiles(string packDirectory)
    {
        string[] candidates =
        {
            Path.Combine(packDirectory, "truewindow.json"),
            Path.Combine(packDirectory, "truewindow.targets.json"),
            Path.Combine(packDirectory, "assets", "truewindow.json"),
            Path.Combine(packDirectory, "assets", "truewindow.targets.json"),
        };

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                yield return candidate;
            }
        }
    }

    // 対応 Content Pack か判定する
    private bool IsSupportedContentPack(IManifest manifest)
    {
        return string.Equals(manifest.ContentPackFor?.UniqueID, "Pathoschild.ContentPatcher", StringComparison.OrdinalIgnoreCase)
            && manifest.Dependencies.Any(static dependency =>
                string.Equals(dependency.UniqueID, "kaniclub.TrueWindow", StringComparison.OrdinalIgnoreCase));
    }

    // 定義 JSON を読み込む
    private void LoadDefinitionSource(string sourceName, string rawJson, string packDirectory)
    {
        try
        {
            using JsonDocument doc = JsonDocument.Parse(rawJson);
            string formatVersion = doc.RootElement.TryGetProperty("Format", out JsonElement formatElement)
                ? (formatElement.GetString() ?? "1.0.0")
                : "1.0.0";

            ITrueWindowFormatReader? reader = this.readers.FirstOrDefault(p => p.CanRead(formatVersion));
            if (reader is null)
            {
                return;
            }

            TrueWindowDefinitionSet definitionSet = reader.Read(rawJson);

            foreach (TrueWindowTargetDefinition target in definitionSet.FurnitureTargets)
            {
                if (string.IsNullOrWhiteSpace(target.TrueWindowId))
                {
                    continue;
                }

                target.SpecifiedImagePath = this.ResolveSpecifiedImagePath(packDirectory, target.SpecifiedImagePath);
                this.byTrueWindowId[target.TrueWindowId] = target;
            }

            foreach (TrueWindowMapTargetDefinition target in definitionSet.MapTargets)
            {
                if (!this.IsValidMapTarget(target))
                {
                    continue;
                }

                target.SpecifiedImagePath = this.ResolveSpecifiedImagePath(packDirectory, target.SpecifiedImagePath);
                this.mapTargetsById[target.TrueWindowId] = target;
            }
        }
        catch
        {
        }
    }


    // MAP 対象定義として有効か判定する
    private bool IsValidMapTarget(TrueWindowMapTargetDefinition target)
    {
        if (string.IsNullOrWhiteSpace(target.TrueWindowId))
        {
            return false;
        }

        if (target.Area.Width <= 0 || target.Area.Height <= 0)
        {
            return false;
        }

        if (target.SourceLayers.Count == 0)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(target.OverlayDrawAfterLayer))
        {
            return false;
        }

        if (target.LocationNames.Count == 0 && target.MapPaths.Count == 0)
        {
            return false;
        }

        return true;
    }

    // 指定画像のパスを content pack 基準で解決する
    private string? ResolveSpecifiedImagePath(string packDirectory, string? rawPath)
    {
        if (string.IsNullOrWhiteSpace(rawPath))
        {
            return null;
        }

        string normalized = rawPath.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized))
        {
            return File.Exists(normalized) ? normalized : null;
        }

        bool hasDirectoryPart = normalized.Contains(Path.DirectorySeparatorChar) || normalized.Contains(Path.AltDirectorySeparatorChar);
        List<string> candidates = new();

        candidates.Add(Path.GetFullPath(Path.Combine(packDirectory, normalized)));

        if (!hasDirectoryPart)
        {
            candidates.Add(Path.GetFullPath(Path.Combine(packDirectory, "assets", normalized)));
        }

        foreach (string candidate in candidates)
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    // context tag に truewindow があるか判定する
    private bool HasTrueWindowTag(Item item)
    {
        return item.GetContextTags().Any(static tag => string.Equals(tag, "truewindow", StringComparison.OrdinalIgnoreCase));
    }

    // context tag からプリセット ID を取得する
    private string? GetTrueWindowIdFromContextTags(Item item)
    {
        const string prefix = "truewindow_id:";

        foreach (string tag in item.GetContextTags())
        {
            if (tag.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                string value = tag[prefix.Length..].Trim();
                return string.IsNullOrWhiteSpace(value) ? null : value;
            }
        }

        return null;
    }

    // itemId を正規化する
    private string NormalizeItemId(string? itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return "default";
        }

        string trimmed = itemId.Trim();
        return trimmed.StartsWith("(F)", StringComparison.OrdinalIgnoreCase)
            ? trimmed[3..]
            : trimmed;
    }
}
