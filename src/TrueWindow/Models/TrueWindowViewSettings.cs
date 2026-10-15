namespace TrueWindow.Models;

// ----------------------------
// 外景の見え方設定
// ----------------------------
internal sealed class TrueWindowViewSettings
{
    public int OffsetX { get; set; }
    public int OffsetY { get; set; }
    public float Zoom { get; set; } = 1.0f;
    public float ParallaxStrength { get; set; } = 0.2f;

    // 設定内容を複製する
    public TrueWindowViewSettings Clone()
    {
        return new TrueWindowViewSettings
        {
            OffsetX = this.OffsetX,
            OffsetY = this.OffsetY,
            Zoom = this.Zoom,
            ParallaxStrength = this.ParallaxStrength,
        };
    }
}
