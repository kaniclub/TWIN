namespace TrueWindow.Models;

// ----------------------------
// 読み込み済み TrueWindow 定義セット
// ----------------------------
internal sealed class TrueWindowDefinitionSet
{
    public List<TrueWindowTargetDefinition> FurnitureTargets { get; } = new();

    public List<TrueWindowMapTargetDefinition> MapTargets { get; } = new();
}
