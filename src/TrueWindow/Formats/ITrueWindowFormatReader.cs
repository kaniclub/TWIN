using TrueWindow.Models;

namespace TrueWindow.Formats;

// ----------------------------
// 定義ファイル reader 共通インターフェース
// - format バージョンごとの読み分けに使う
// ----------------------------
internal interface ITrueWindowFormatReader
{
    // 読み込み対象の format か判定する
    bool CanRead(string formatVersion);

    // JSON から対象定義へ変換する
    TrueWindowDefinitionSet Read(string rawJson);
}
