using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

// ----------------------------
// 予約色マスクのキャッシュ
// ----------------------------
namespace TrueWindow.Services;

internal sealed class TrueWindowMaskCache
{
    private readonly Dictionary<string, TrueWindowMaskData> cache = new(StringComparer.OrdinalIgnoreCase);

    // 予約色マスクを取得または作成する
    public TrueWindowMaskData GetOrCreate(string cacheKey, Texture2D texture, Rectangle sourceRect, Color reservedColor)
    {
        if (this.cache.TryGetValue(cacheKey, out TrueWindowMaskData? existing))
        {
            return existing;
        }

        Color[] pixels = new Color[sourceRect.Width * sourceRect.Height];
        texture.GetData(0, sourceRect, pixels, 0, pixels.Length);

        List<TrueWindowMaskRow> rows = new();
        int minX = int.MaxValue;
        int minY = int.MaxValue;
        int maxX = -1;
        int maxY = -1;

        for (int y = 0; y < sourceRect.Height; y++)
        {
            int start = -1;
            for (int x = 0; x < sourceRect.Width; x++)
            {
                bool isMask = pixels[(y * sourceRect.Width) + x] == reservedColor;
                if (isMask && start < 0)
                {
                    start = x;
                }
                else if (!isMask && start >= 0)
                {
                    rows.Add(new TrueWindowMaskRow(y, start, x - start));
                    minX = Math.Min(minX, start);
                    minY = Math.Min(minY, y);
                    maxX = Math.Max(maxX, x - 1);
                    maxY = Math.Max(maxY, y);
                    start = -1;
                }
            }

            if (start >= 0)
            {
                rows.Add(new TrueWindowMaskRow(y, start, sourceRect.Width - start));
                minX = Math.Min(minX, start);
                minY = Math.Min(minY, y);
                maxX = Math.Max(maxX, sourceRect.Width - 1);
                maxY = Math.Max(maxY, y);
            }
        }

        Rectangle bounds = rows.Count > 0
            ? new Rectangle(minX, minY, (maxX - minX) + 1, (maxY - minY) + 1)
            : Rectangle.Empty;

        TrueWindowMaskData created = new(sourceRect.Width, sourceRect.Height, rows, bounds);
        this.cache[cacheKey] = created;
        return created;
    }

    // キャッシュを破棄する
    public void Clear()
    {
        this.cache.Clear();
    }
}

internal sealed class TrueWindowMaskData
{
    // マスク解析結果を初期化する
    public TrueWindowMaskData(int width, int height, IReadOnlyList<TrueWindowMaskRow> rows, Rectangle bounds)
    {
        this.Width = width;
        this.Height = height;
        this.Rows = rows;
        this.Bounds = bounds;
    }

    public int Width { get; }
    public int Height { get; }
    public IReadOnlyList<TrueWindowMaskRow> Rows { get; }
    public Rectangle Bounds { get; }
    public bool HasMask => this.Rows.Count > 0;
}

// 予約色領域を表す
internal readonly record struct TrueWindowMaskRow(int Y, int StartX, int Width);
