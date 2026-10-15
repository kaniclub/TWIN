using StardewModdingAPI;
using TrueWindow.Models;

// ----------------------------
// 実行時設定の保存サービス
// ----------------------------
namespace TrueWindow.Services;

internal sealed class TrueWindowInstanceRepository
{
    private const string SaveKey = "true-window.runtime";

    private readonly IModHelper helper;
    private readonly Dictionary<string, TrueWindowLocationData> locations = new(StringComparer.OrdinalIgnoreCase);

    // リポジトリを初期化する
    public TrueWindowInstanceRepository(IModHelper helper)
    {
        this.helper = helper;
    }

    // 保存データを読み込む
    public void Load()
    {
        this.locations.Clear();

        TrueWindowSaveData data = this.helper.Data.ReadSaveData<TrueWindowSaveData>(SaveKey) ?? new TrueWindowSaveData();
        foreach (TrueWindowLocationData location in data.Locations)
        {
            if (string.IsNullOrWhiteSpace(location.LocationName))
            {
                continue;
            }

            this.locations[location.LocationName] = CloneLocation(location);
        }
    }

    // 保存データを書き出す
    public void Save()
    {
        TrueWindowSaveData data = new()
        {
            Locations = this.locations.Values
                .OrderBy(static p => p.LocationName, StringComparer.OrdinalIgnoreCase)
                .Select(CloneLocation)
                .ToList(),
        };

        this.helper.Data.WriteSaveData(SaveKey, data);
    }

    // ロケーション設定を取得し、無ければ既定値から作成する
    public TrueWindowRuntimeSettings GetOrCreate(string locationName, Func<TrueWindowRuntimeSettings> createSettings)
    {
        if (this.locations.TryGetValue(locationName, out TrueWindowLocationData? existing))
        {
            return existing.RuntimeSettings;
        }

        TrueWindowRuntimeSettings created = createSettings();
        this.locations[locationName] = new TrueWindowLocationData
        {
            LocationName = locationName,
            RuntimeSettings = created,
        };

        return created;
    }

    // ロケーション設定を更新する
    public void Update(string locationName, TrueWindowRuntimeSettings settings)
    {
        this.locations[locationName] = new TrueWindowLocationData
        {
            LocationName = locationName,
            RuntimeSettings = settings.Clone(),
        };
    }

    // ロケーション設定を複製する
    private static TrueWindowLocationData CloneLocation(TrueWindowLocationData source)
    {
        return new TrueWindowLocationData
        {
            LocationName = source.LocationName,
            RuntimeSettings = source.RuntimeSettings.Clone(),
        };
    }
}
