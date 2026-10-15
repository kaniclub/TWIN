namespace TrueWindow;

// ----------------------------
// MOD 全体の設定
// ----------------------------
internal sealed class ModConfig
{
    // 外景を再描画する最小フレーム間隔
    public int RenderThrottleFrames { get; set; } = 3;

    // 外景レンダリングで許可する最大ズーム倍率
    public float MaxRenderScale { get; set; } = 2.0f;

    // 設定メニューで許可するズーム範囲
    public float MinEditableZoom { get; set; } = 0.5f;
    public float MaxEditableZoom { get; set; } = 2.0f;

    // 屋内に TW 家具が無いときの再探索間隔
    public int IndoorProbeIntervalTicks { get; set; } = 15;

    // ----------------------------
    // 設定内容を複製する
    // ----------------------------
    public ModConfig Clone()
    {
        return new ModConfig
        {
            RenderThrottleFrames = this.RenderThrottleFrames,
            MaxRenderScale = this.MaxRenderScale,
            MinEditableZoom = this.MinEditableZoom,
            MaxEditableZoom = this.MaxEditableZoom,
            IndoorProbeIntervalTicks = this.IndoorProbeIntervalTicks,
        };
    }
}
