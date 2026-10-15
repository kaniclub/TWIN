using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using StardewValley.Objects;
using TrueWindow.Models;

// ----------------------------
// 家具描画処理の補助型
// ----------------------------
namespace TrueWindow.Services;

internal enum LocationProcessingMode
{
    OutdoorSkip,
    IndoorDormant,
    IndoorActive,
}

// 外景スナップショット一式をまとめる
internal readonly record struct SnapshotPixelData(Color[] Pixels, Rectangle Bounds);
internal readonly record struct SnapshotBundle(TrueWindowRenderSnapshot Snapshot, SnapshotPixelData SnapshotPixels);

// ランタイムテクスチャの紐付け情報をまとめる
internal sealed record RuntimeTextureBinding(
    string ItemId,
    string TrueWindowId,
    string OriginalTextureAssetName,
    string RuntimeAssetName);

internal sealed class RuntimeTextureState
{
    // ランタイム atlas の状態を初期化する
    public RuntimeTextureState(
        RuntimeTextureBinding binding,
        Texture2D runtimeTexture,
        Texture2D originalTexture,
        Color[] originalPixels,
        Color[] baseAtlasPixels,
        int originalTextureWidth,
        int originalTextureHeight,
        int atlasWidth,
        int atlasHeight,
        int slotWidth)
    {
        this.Binding = binding;
        this.RuntimeTexture = runtimeTexture;
        this.OriginalTexture = originalTexture;
        this.OriginalPixels = originalPixels;
        this.BaseAtlasPixels = baseAtlasPixels;
        this.WorkingPixels = (Color[])baseAtlasPixels.Clone();
        this.UploadedPixels = (Color[])baseAtlasPixels.Clone();
        this.OriginalTextureWidth = originalTextureWidth;
        this.OriginalTextureHeight = originalTextureHeight;
        this.AtlasWidth = atlasWidth;
        this.AtlasHeight = atlasHeight;
        this.SlotWidth = slotWidth;
    }

    public RuntimeTextureBinding Binding { get; }
    public Texture2D RuntimeTexture { get; }
    public Texture2D OriginalTexture { get; }
    public Color[] OriginalPixels { get; }
    public Color[] BaseAtlasPixels { get; }
    public Color[] WorkingPixels { get; }
    public Color[] UploadedPixels { get; }
    public int OriginalTextureWidth { get; }
    public int OriginalTextureHeight { get; }
    public int AtlasWidth { get; }
    public int AtlasHeight { get; }
    public int SlotWidth { get; }
}

// 家具1件分の描画コンテキストをまとめる
internal sealed record TrueWindowRuntimeContext(
    Furniture Furniture,
    TrueWindowTargetDefinition Definition,
    RuntimeTextureBinding Binding,
    Rectangle SourceRect,
    TrueWindowMaskData Mask,
    Rectangle WorldBounds,
    Color ReservedColor);
