# TrueWindow Shed Map Test

TrueWindow の MapTargets を試すための最小サンプルです。

## 内容
- `content.json`
  - `Maps/Shed` と `Maps/Shed2` に小さな窓パッチを重ねます。
- `truewindow.targets.json`
  - 上記 2 つのマップに対する `MapTargets` を定義します。
- `assets/Shed_Window_Test.tmx`
  - 13x2 タイルの小さなマップパッチです。
- `assets/map_window_tiles.png`
  - 窓の見た目に使う 2 タイルの簡易タイルシートです。

## 使い方
1. このフォルダを `Mods` に入れます。
2. TrueWindow 本体も有効にします。
3. Shed または Big Shed に入ります。
4. 同じロケーション内に TrueWindow 家具を 1 つ置き、その家具から設定メニューを開きます。
5. `Auto` のままなら `SpecifiedImage` として `stardewPanorama.png` が初期表示候補になります。

## 編集ポイント
- 窓を増やしたい場合:
  - `assets/Shed_Window_Test.tmx` のタイル配置を増やす
  - `truewindow.targets.json` の `Area` を合わせて広げる
- 別レイヤーもマスク対象にしたい場合:
  - `SourceLayers` に追加する
- 窓の設置位置を変えたい場合:
  - `content.json` の `ToArea`
  - `truewindow.targets.json` の `Area`
  を同じ値にそろえて変更する


## 2026-03-25 修正版メモ
このサンプルでは、離れた複数の窓を1つの長い `MapTarget.Area` で扱うと、TrueWindow 側がそれらを1つの連続表示領域として扱ってしまい、期待どおりに確認しづらくなります。
そのため現在は **窓ごとに 1x2 タイルの MapTarget に分割** しています。
