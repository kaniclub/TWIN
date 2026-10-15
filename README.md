# TrueWindow

現在のバージョン：**1.0.5**。変更点は[更新履歴](CHANGELOG.md)を参照してください。

TrueWindow は、**窓を追加する MOD ではありません**。  
TrueWindow は、**TrueWindow 対応の家具に「外の景色を映す機能」を追加するための機能追加 MOD** です。

対応した家具を室内に設置すると、その家具のガラス部分に **屋外の景色がリアルタイムで描画** されます。  
つまり TrueWindow 本体だけでは新しい窓家具は増えず、**TrueWindow 対応家具を追加する MOD と組み合わせて使う** ことを前提にしています。

## できること

- TrueWindow 対応家具のガラス部分に、屋外の景色をリアルタイム描画
- 室内から外を見ているような見た目を実現
- 家具の表示設定で、ロケーションごとに見え方を調整
- 表示元として、現在のロケーション表示・任意画像・組み込み画像を切り替え可能
- 組み込み画像として、**夜空・山岳・海洋** の画像を選択可能
- Content Patcher の家具追加と組み合わせて、TrueWindow 対応家具を増やせる
- 家具ごとに予約色・初期表示設定・家具の表示設定可否などを定義できる
- 家具が持つ発光の描画有無を定義できる

## 重要な前提

- TrueWindow は **機能 MOD** です。窓そのものを追加する MOD ではありません。
- 現在、**TrueWindow が対応しているのは家具のみ** です。
- 建物、壁、壁紙、タイルマップそのものを TrueWindow 化する機能は、現時点では含まれていません。
- TrueWindow 対応家具を追加したい場合は、基本的に **Content Patcher の家具追加** と組み合わせます。

## 必要環境

- SMAPI
- Content Patcher
- Generic Mod Config Menu（任意）

## 設定一覧

TrueWindow には、**本体設定** と **家具の表示設定** があります。

### 1. 本体設定

本体設定は `config.json` または GMCM から変更できます。

#### RenderThrottleFrames
- 外景を再描画する最小フレーム間隔です。
- 値を大きくすると再描画回数が減り、負荷を抑えやすくなります。

#### IndoorProbeIntervalTicks
- 現在の屋内に TrueWindow 家具が無い場合、再探索する間隔です。
- 値を大きくすると探索頻度が下がります。

#### MaxRenderScale
- 外景レンダリングで許可する最大ズーム倍率です。
- 主に内部制御用の上限値です。

#### MinEditableZoom
- 家具の表示設定で変更できるズームの最小値です。

#### MaxEditableZoom
- 家具の表示設定で変更できるズームの最大値です。

### 2. 家具の表示設定

TrueWindow 対応家具を右クリックすると、ロケーション単位の表示設定を変更できます。  
この設定はセーブデータに保存されます。

#### DisplaySource
- 表示元を選択します。
- 次の候補から切り替えできます。
  - **Location**: 現在の外景をリアルタイム表示します。
  - **Mountain**: 組み込みの山岳画像を表示します。
  - **Ocean**: 組み込みの海洋画像を表示します。
  - **Night Sky**: 組み込みの夜空画像を表示します。
  - **Specified Image**: Content Pack 側で `SpecifiedImage` に設定した画像を表示します。

#### OffsetX
- 横方向の表示オフセットです。

#### OffsetY
- 縦方向の表示オフセットです。

#### Zoom
- 外景または画像の拡大率です。

#### ParallaxStrength
- プレイヤーの位置に応じた視差の強さです。
- 固定画像を表示している場合も、見え方の調整に使えます。

## TrueWindow 対応家具を追加する考え方

TrueWindow 対応家具を追加するには、基本的に次の2つが必要です。

1. **家具データ側で、その家具を TrueWindow 対象として識別できること**
2. **家具テクスチャ側で、ガラスとして置換したい部分に予約色を塗っておくこと**

TrueWindow は家具の context tag を見て対象判定しています。

### 必須タグ

- `truewindow`

このタグが付いている家具だけが TrueWindow 対象になります。

### 任意タグ

- `truewindow_id:任意のID`

このタグがある場合、その ID を TrueWindow 定義の参照先として使います。  
このタグが無い場合は、家具の `ItemId` がそのまま TrueWindow 定義の ID として使われます。

## ガラス部分の作り方

家具画像のうち、**外景を表示したい部分を予約色で塗る** 必要があります。

既定の予約色は次の色です。

- `#FF00FF`

この予約色で塗られたピクセルだけが、外景表示領域として扱われます。  
窓枠や飾り線まで予約色にしてしまうと、その部分も外景に置き換わるためご注意ください。

---

## 最小構成

もっとも小さい構成は、**家具に `truewindow` タグを付けて、ガラス部分を既定予約色 `#FF00FF` で塗るだけ** です。  
この場合、追加の TrueWindow 定義ファイルは不要です。

### 最小構成の考え方

- 家具に `truewindow` タグを付ける
- ガラス部分を `#FF00FF` で塗る
- `truewindow_id` は省略可能
- `truewindow.json` は省略可能
- 表示初期値は TrueWindow 側の既定値が使われる

### ディレクトリ構成例

```text
[CP] YourName.MinimalTrueWindow/
├─ manifest.json
├─ content.json
└─ assets/
   └─ minimal_window.png
```

### 最小構成の例

#### `content.json`

```json
{
  "Format": "2.8.0",
  "Changes": [
    {
      "Action": "EditData",
      "Target": "Data/Furniture",
      "Entries": {
        "YourName_MinimalTrueWindow": {
          "Name": "Minimal TrueWindow Window",
          "Type": "decor",
          "TilesheetSize": "1 2",
          "BoundingBoxSize": "1 2",
          "Rotations": 1,
          "Texture": "Mods/YourName.MinimalTrueWindow/MinimalWindow",
          "TextureSpriteRow": 0,
          "Price": 0,
          "ContextTags": [
            "truewindow"
          ]
        }
      }
    },
    {
      "Action": "Load",
      "Target": "Mods/YourName.MinimalTrueWindow/MinimalWindow",
      "FromFile": "assets/minimal_window.png"
    }
  ]
}
```

> `assets/minimal_window.png` のガラス部分を `#FF00FF` で塗ってください。

---

## 最大構成

より細かく制御したい場合は、`truewindow_id` と `truewindow.json` を使って設定できます。

### 最大構成で設定できる内容

- TrueWindow 定義 ID の明示
- 予約色の変更
- 指定画像の初期設定
- 初期表示位置の調整
- 初期ズームの設定
- 視差強度の設定
- 家具発光の描画有無
- 家具の表示設定の許可 / 禁止

### ディレクトリ構成例

```text
[CP] YourName.FullTrueWindow/
├─ manifest.json
├─ content.json
├─ truewindow.json
└─ assets/
   ├─ full_window.png
   └─ mountain_view.png
```

### 最大構成の例

#### `content.json`

```json
{
  "Format": "2.8.0",
  "Changes": [
    {
      "Action": "EditData",
      "Target": "Data/Furniture",
      "Entries": {
        "YourName_FullTrueWindow": {
          "Name": "Full TrueWindow Window",
          "Type": "decor",
          "TilesheetSize": "2 2",
          "BoundingBoxSize": "2 2",
          "Rotations": 1,
          "Texture": "Mods/YourName.FullTrueWindow/FullWindow",
          "TextureSpriteRow": 0,
          "Price": 0,
          "ContextTags": [
            "truewindow",
            "truewindow_id:full_window_a"
          ]
        }
      }
    },
    {
      "Action": "Load",
      "Target": "Mods/YourName.FullTrueWindow/FullWindow",
      "FromFile": "assets/full_window.png"
    }
  ]
}
```

#### `truewindow.json`

```json
{
  "Format": "1.0.0",
  "Targets": [
    {
      "TrueWindowId": "full_window_a",
      "ReservedColor": "#FF00FF",
      "SpecifiedImage": "assets/mountain_view.png",
      "RenderLightGlows": false,
      "ViewSettings": {
        "OffsetX": 0,
        "OffsetY": 0,
        "Zoom": 1.0,
        "ParallaxStrength": 0.2
      },
      "AllowRightClickConfig": true
    }
  ]
}
```

### `SpecifiedImage` の置き場所について

画像は Content Pack の**ルート**または **`assets` フォルダ**に置けます。  
分かりやすさのため、`assets/ファイル名.png` のように書く形をおすすめします。

例:

- `"SpecifiedImage": "mountain_view.png"`
- `"SpecifiedImage": "assets/mountain_view.png"`

---

## TrueWindow 定義ファイルについて

TrueWindow は、対応する Content Pack から定義を読み込みます。  
定義は次のいずれかの場所に置けます。

- `content.json` 内の `TrueWindow` セクション
- `truewindow.json`
- `assets/truewindow.json`

## 定義項目一覧

### TrueWindowId
- TrueWindow 用の識別子です。
- `truewindow_id:xxx` と対応させるか、タグ未指定時は家具の `ItemId` と合わせて使います。

### ReservedColor
- ガラス部分として扱う予約色です。
- 未指定時は `#FF00FF` です。

### SpecifiedImage
- 初期状態で選択可能にする指定画像です。
- 家具の表示設定の `DisplaySource` で **Specified Image** を選ぶと、この画像を表示できます。
- 相対パスで指定します。
- 例:
  - `mountain_view.png`
  - `assets/mountain_view.png`

### RenderLightGlows
- 家具が持つ発光表現を描画するかどうかです。
- `true` で発光あり、`false` で発光なしです。

### ViewSettings.OffsetX
- 初期の横方向オフセットです。

### ViewSettings.OffsetY
- 初期の縦方向オフセットです。

### ViewSettings.Zoom
- 初期ズームです。

### ViewSettings.ParallaxStrength
- 初期の視差強度です。

### AllowRightClickConfig
- 家具の表示設定を開けるようにするかどうかです。
- `false` にすると、その家具では家具の表示設定を開けません。

## 仕様上の注意

- TrueWindow は **家具のテクスチャの一部を、ランタイムで外景へ差し替える** 方式です。
- そのため、ガラス部分は予約色で明確に分離しておく必要があります。
- 現在の保存設定は **ロケーション単位** です。
- 同じロケーションに複数の TrueWindow 家具がある場合、見え方設定は共有されます。
- 家具の表示設定の既定値は、TrueWindow 定義の `ViewSettings` や `SpecifiedImage` から作られます。

## 現在対応している対象

現在、TrueWindow が対応しているのは **家具のみ** です。

- 家具: 対応
- 建物: 未対応
- 壁タイル: 未対応
- 壁紙: 未対応
- マップタイル直接置換: 未対応

## まとめ

TrueWindow は「窓を追加する MOD」ではなく、**対応家具に外景表示機能を与えるための土台 MOD** です。  
新しい窓を増やしたい場合は、TrueWindow 本体に加えて、**TrueWindow 対応家具を追加する Content Pack** を用意してください。

