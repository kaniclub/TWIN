using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using StardewModdingAPI;
using StardewValley;
using StardewValley.Menus;
using TrueWindow.Models;
using TrueWindow.Services;

// ----------------------------
// TrueWindow 設定メニュー
// ----------------------------
namespace TrueWindow.Menus;

internal sealed class TrueWindowSettingsMenu : IClickableMenu
{
    private readonly ModConfig config;
    private readonly ITranslationHelper translation;
    private readonly Action<TrueWindowRuntimeSettings> onChanged;
    private readonly Action onCancel;
    private readonly TrueWindowRuntimeSettings editing;
    private readonly List<ImageSourceOption> imageOptions;
    private readonly List<RowButton> rows = new();
    private readonly ClickableTextureComponent okButton;
    private readonly ClickableTextureComponent cancelButton;
    private RowButton? heldRow;
    private bool heldLeftSide;
    private long holdStartTick;
    private long lastRepeatTick;
    private static Point? rememberedPosition;
    private bool isDragging;
    private Point dragOffset;

    private Rectangle DragArea => new(this.xPositionOnScreen + 12, this.yPositionOnScreen + 8, this.width - 24, 48);

    // 設定メニューを初期化する
    public TrueWindowSettingsMenu(
        TrueWindowRuntimeSettings editing,
        ModConfig config,
        ITranslationHelper translation,
        List<ImageSourceOption> imageOptions,
        Action<TrueWindowRuntimeSettings> onChanged,
        Action onCancel)
        : base(0, 0, 0, 0, showUpperRightCloseButton: false)
    {
        this.config = config;
        this.translation = translation;
        this.editing = editing;
        this.imageOptions = imageOptions;
        this.onChanged = onChanged;
        this.onCancel = onCancel;

        const int rowHeight = 52;
        const int headerHeight = 62;
        const int footerHeight = 76;
        const int desiredWidth = 620;
        const int leftPadding = 34;
        const int rightPadding = 30;

        int maxWidth = Math.Min(desiredWidth, Game1.uiViewport.Width - 96);
        int contentHeight = headerHeight + footerHeight;

        this.rows.Add(this.CreateChoiceRow(
            this.T("menu.row.display_source"),
            this.GetDisplaySourceText,
            this.SelectPreviousDisplaySource,
            this.SelectNextDisplaySource,
            repeatOnHold: false));

        this.rows.Add(this.CreateNumberRow(this.T("menu.row.offset_x"), () => this.editing.ViewSettings.OffsetX, value => this.editing.ViewSettings.OffsetX = value, 16, repeatOnHold: true));
        this.rows.Add(this.CreateNumberRow(this.T("menu.row.offset_y"), () => this.editing.ViewSettings.OffsetY, value => this.editing.ViewSettings.OffsetY = value, 16, repeatOnHold: true));
        this.rows.Add(this.CreateFloatRow(this.T("menu.row.zoom"), () => this.editing.ViewSettings.Zoom, value => this.editing.ViewSettings.Zoom = Math.Clamp(value, this.config.MinEditableZoom, this.config.MaxEditableZoom), 0.1f, repeatOnHold: true));
        this.rows.Add(this.CreateFloatRow(this.T("menu.row.parallax"), () => this.editing.ViewSettings.ParallaxStrength, value => this.editing.ViewSettings.ParallaxStrength = Math.Clamp(value, 0f, 1f), 0.05f, repeatOnHold: true));

        contentHeight += this.rows.Count * rowHeight;
        this.width = maxWidth;
        this.height = Math.Min(contentHeight, Game1.uiViewport.Height - 96);
        this.xPositionOnScreen = (Game1.uiViewport.Width - this.width) / 2;
        this.yPositionOnScreen = Math.Max(48, (Game1.uiViewport.Height - this.height) / 2 + 12);

        int labelX = this.xPositionOnScreen + leftPadding;
        int top = this.yPositionOnScreen + headerHeight + 6;
        int valueX = this.xPositionOnScreen + this.width - rightPadding - 156;
        int leftButtonX = this.xPositionOnScreen + this.width - rightPadding - 84;
        int rightButtonX = this.xPositionOnScreen + this.width - rightPadding - 36;

        for (int i = 0; i < this.rows.Count; i++)
        {
            int rowTop = top + (rowHeight * i);
            this.rows[i].ApplyLayout(labelX, rowTop, valueX, rowTop, leftButtonX, rowTop - 6, rightButtonX, rowTop - 6);
        }

        this.okButton = CreateFooterButton(this.xPositionOnScreen + this.width - 144, this.yPositionOnScreen + this.height - 72, new Rectangle(128, 256, 64, 64));
        this.cancelButton = CreateFooterButton(this.xPositionOnScreen + this.width - 80, this.yPositionOnScreen + this.height - 72, new Rectangle(192, 256, 64, 64));
        Point initialPosition = rememberedPosition ?? new Point(this.xPositionOnScreen, this.yPositionOnScreen);
        this.MoveTo(initialPosition.X, initialPosition.Y);
    }

    // クリック入力を処理する
    public override void receiveLeftClick(int x, int y, bool playSound = true)
    {
        base.receiveLeftClick(x, y, playSound);

        if (this.DragArea.Contains(x, y))
        {
            this.ClearHold();
            this.isDragging = true;
            this.dragOffset = new Point(x - this.xPositionOnScreen, y - this.yPositionOnScreen);
            return;
        }

        this.isDragging = false;
        foreach (RowButton row in this.rows)
        {
            if (row.LeftButton.containsPoint(x, y))
            {
                this.BeginHold(row, isLeftSide: true);
                this.ApplyRowStep(row, isLeftSide: true);
                return;
            }

            if (row.RightButton.containsPoint(x, y))
            {
                this.BeginHold(row, isLeftSide: false);
                this.ApplyRowStep(row, isLeftSide: false);
                return;
            }
        }

        this.ClearHold();

        if (this.okButton.containsPoint(x, y))
        {
            Game1.exitActiveMenu();
            Game1.playSound("smallSelect");
            return;
        }

        if (this.cancelButton.containsPoint(x, y))
        {
            this.onCancel();
            Game1.exitActiveMenu();
            Game1.playSound("bigDeSelect");
        }
    }

    // 長押し入力を処理する
    public override void leftClickHeld(int x, int y)
    {
        base.leftClickHeld(x, y);

        if (this.isDragging)
        {
            this.MoveTo(x - this.dragOffset.X, y - this.dragOffset.Y);
            return;
        }

        if (this.heldRow is null)
        {
            return;
        }

        ClickableTextureComponent activeButton = this.heldLeftSide ? this.heldRow.LeftButton : this.heldRow.RightButton;
        if (!activeButton.containsPoint(x, y))
        {
            this.ClearHold();
            return;
        }

        if (!this.heldRow.RepeatOnHold)
        {
            return;
        }

        long currentTick = Game1.ticks;
        if (currentTick - this.holdStartTick < 20 || currentTick - this.lastRepeatTick < 3)
        {
            return;
        }

        this.lastRepeatTick = currentTick;
        this.ApplyRowStep(this.heldRow, this.heldLeftSide);
    }

    // 長押し状態を解除する
    public override void releaseLeftClick(int x, int y)
    {
        base.releaseLeftClick(x, y);
        if (this.isDragging)
        {
            this.MoveTo(x - this.dragOffset.X, y - this.dragOffset.Y);
            this.isDragging = false;
        }
        this.ClearHold();
    }

    // 表示位置とクリック判定を同じ量だけ動かし、画面外への移動を防ぐ。
    private void MoveTo(int x, int y)
    {
        x = Math.Clamp(x, 0, Math.Max(0, Game1.uiViewport.Width - this.width));
        y = Math.Clamp(y, 0, Math.Max(0, Game1.uiViewport.Height - this.height));
        int dx = x - this.xPositionOnScreen;
        int dy = y - this.yPositionOnScreen;
        this.xPositionOnScreen = x;
        this.yPositionOnScreen = y;
        foreach (RowButton row in this.rows)
            row.ApplyLayout(row.LabelX + dx, row.LabelY + dy, row.ValueX + dx, row.ValueY + dy,
                row.LeftButton.bounds.X + dx, row.LeftButton.bounds.Y + dy,
                row.RightButton.bounds.X + dx, row.RightButton.bounds.Y + dy);
        this.okButton.bounds.Offset(dx, dy);
        this.cancelButton.bounds.Offset(dx, dy);
        rememberedPosition = new Point(x, y);
    }

    public override void gameWindowSizeChanged(Rectangle oldBounds, Rectangle newBounds)
    {
        this.isDragging = false;
        this.ClearHold();
        this.MoveTo(this.xPositionOnScreen, this.yPositionOnScreen);
    }

    // キー入力を処理する
    public override void receiveKeyPress(Keys key)
    {
        if (Game1.options.doesInputListContain(Game1.options.menuButton, key) && this.readyToClose())
        {
            this.onCancel();
            Game1.exitActiveMenu();
            return;
        }

        base.receiveKeyPress(key);
    }

    // 画面を描画する
    public override void draw(SpriteBatch b)
    {
        IClickableMenu.drawTextureBox(b, this.xPositionOnScreen, this.yPositionOnScreen, this.width, this.height, Color.White * 0.88f);

        Utility.drawTextWithShadow(b, this.T("menu.title"), Game1.dialogueFont, new Vector2(this.xPositionOnScreen + 24, this.yPositionOnScreen + 12), Game1.textColor);
        for (int i = 0; i < 3; i++)
            b.Draw(Game1.staminaRect, new Rectangle(this.xPositionOnScreen + this.width - 48, this.yPositionOnScreen + 22 + i * 6, 20, 2), Game1.textColor * 0.6f);

        string? hoverValue = null;
        foreach (RowButton row in this.rows)
        {
            Utility.drawTextWithShadow(b, row.Label, Game1.smallFont, new Vector2(row.LabelX, row.LabelY), Game1.textColor);

            float valueRight = row.LeftButton.bounds.X - 18f;
            float valueLeft = row.LabelX + Game1.smallFont.MeasureString(row.Label).X + 24f;
            string originalText = row.GetValueText();
            string valueText = row.FitValueText(originalText, Math.Max(20f, valueRight - valueLeft));
            float valueWidth = Game1.smallFont.MeasureString(valueText).X;
            float valueX = valueRight - valueWidth;

            Utility.drawTextWithShadow(b, valueText, Game1.smallFont, new Vector2(valueX, row.ValueY), Game1.textColor);
            if (valueText != originalText
                && new Rectangle((int)valueX, row.ValueY, (int)valueWidth, Game1.smallFont.LineSpacing)
                    .Contains(Game1.getMouseX(), Game1.getMouseY()))
                hoverValue = originalText;

            row.LeftButton.draw(b, Color.White, 0.89f);
            row.RightButton.draw(b, Color.White, 0.89f);
        }

        this.okButton.draw(b, Color.White, 0.89f);
        this.cancelButton.draw(b, Color.White, 0.89f);
        if (hoverValue is not null) IClickableMenu.drawHoverText(b, hoverValue, Game1.smallFont);
        else if (!this.isDragging && this.DragArea.Contains(Game1.getMouseX(), Game1.getMouseY()))
            IClickableMenu.drawHoverText(b, this.T("menu.drag_hint"), Game1.smallFont);
        base.drawMouse(b);
    }

    // 行の値変更を適用する
    private void ApplyRowStep(RowButton row, bool isLeftSide)
    {
        if (isLeftSide)
        {
            row.OnLeft();
        }
        else
        {
            row.OnRight();
        }

        this.onChanged(this.editing.Clone());
        Game1.playSound("drumkit6");
    }

    private string T(string key)
    {
        return this.translation.Get(key).ToString();
    }

    // 表示元の文言を返す
    private string GetDisplaySourceText()
    {
        ImageSourceOption current = this.GetCurrentImageOption();
        return current.SourceId == TrueWindowLocationDisplayService.LocationOptionSourceId
            ? this.T("menu.value.location")
            : current.DisplayName;
    }

    // 前の表示元を選ぶ
    private void SelectPreviousDisplaySource()
    {
        this.MoveDisplaySource(-1);
    }

    // 次の表示元を選ぶ
    private void SelectNextDisplaySource()
    {
        this.MoveDisplaySource(1);
    }

    // 表示元選択を移動する
    private void MoveDisplaySource(int delta)
    {
        if (this.imageOptions.Count == 0)
        {
            this.editing.DisplaySourceMode = TrueWindowDisplaySourceMode.Location;
            this.editing.SelectedImageSourceId = null;
            return;
        }

        int currentIndex = this.GetCurrentImageOptionIndex();
        int nextIndex = (currentIndex + delta) % this.imageOptions.Count;
        if (nextIndex < 0)
        {
            nextIndex += this.imageOptions.Count;
        }

        ImageSourceOption selected = this.imageOptions[nextIndex];
        if (selected.SourceId == TrueWindowLocationDisplayService.LocationOptionSourceId)
        {
            this.editing.DisplaySourceMode = TrueWindowDisplaySourceMode.Location;
            this.editing.SelectedImageSourceId = null;
            return;
        }

        this.editing.DisplaySourceMode = TrueWindowDisplaySourceMode.SpecifiedImage;
        this.editing.SelectedImageSourceId = selected.SourceId;
    }

    // 現在の表示元候補の添字を返す
    private int GetCurrentImageOptionIndex()
    {
        string? selectedSourceId = this.ResolveSelectedSourceId();
        int index = this.imageOptions.FindIndex(option => string.Equals(option.SourceId, selectedSourceId, StringComparison.OrdinalIgnoreCase));
        return index >= 0 ? index : 0;
    }

    // 現在の表示元候補を返す
    private ImageSourceOption GetCurrentImageOption()
    {
        int index = this.GetCurrentImageOptionIndex();
        if (index < 0 || index >= this.imageOptions.Count)
        {
            return new ImageSourceOption(TrueWindowLocationDisplayService.LocationOptionSourceId, this.T("menu.value.location"), null, null, false);
        }

        return this.imageOptions[index];
    }

    // 編集中設定の選択中ソースIDを解決する
    private string ResolveSelectedSourceId()
    {
        if (this.editing.DisplaySourceMode == TrueWindowDisplaySourceMode.Location)
        {
            return TrueWindowLocationDisplayService.LocationOptionSourceId;
        }

        if (this.editing.DisplaySourceMode == TrueWindowDisplaySourceMode.SpecifiedImage && !string.IsNullOrWhiteSpace(this.editing.SelectedImageSourceId))
        {
            return this.editing.SelectedImageSourceId;
        }

        return TrueWindowLocationDisplayService.LocationOptionSourceId;
    }

    // 長押し状態を開始する
    private void BeginHold(RowButton row, bool isLeftSide)
    {
        this.heldRow = row;
        this.heldLeftSide = isLeftSide;
        this.holdStartTick = Game1.ticks;
        this.lastRepeatTick = Game1.ticks;
    }

    // 長押し状態を解除する
    private void ClearHold()
    {
        this.heldRow = null;
    }

    // 整数用の設定行を作成する
    private RowButton CreateNumberRow(string label, Func<int> getter, Action<int> setter, int delta, bool repeatOnHold)
    {
        return new RowButton(label, () => getter().ToString(), () => setter(getter() - delta), () => setter(getter() + delta), repeatOnHold);
    }

    // 小数用の設定行を作成する
    private RowButton CreateFloatRow(string label, Func<float> getter, Action<float> setter, float delta, bool repeatOnHold)
    {
        return new RowButton(label, () => getter().ToString("0.00"), () => setter(getter() - delta), () => setter(getter() + delta), repeatOnHold);
    }

    // 選択肢用の設定行を作成する
    private RowButton CreateChoiceRow(string label, Func<string> getter, Action onLeft, Action onRight, bool repeatOnHold)
    {
        return new RowButton(label, getter, onLeft, onRight, repeatOnHold);
    }

    // フッターボタンを作成する
    private static ClickableTextureComponent CreateFooterButton(int x, int y, Rectangle sourceRect)
    {
        return new ClickableTextureComponent(new Rectangle(x, y, 64, 64), Game1.mouseCursors, sourceRect, 1f);
    }

    private sealed class RowButton
    {
        private string? lastValueText;
        private string fittedValueText = "";
        private float lastValueWidth;

        public string FitValueText(string text, float maxWidth)
        {
            if (text == lastValueText && maxWidth == lastValueWidth) return fittedValueText;
            lastValueText = text;
            lastValueWidth = maxWidth;
            fittedValueText = text;
            if (Game1.smallFont.MeasureString(text).X > maxWidth)
            {
                int length = text.Length;
                do
                {
                    length--;
                    // UTF-16のサロゲートペアを途中で切らない。
                    if (length > 0 && char.IsHighSurrogate(text[length - 1])) length--;
                    fittedValueText = text[..length] + "...";
                }
                while (length > 0 && Game1.smallFont.MeasureString(fittedValueText).X > maxWidth);
            }
            return fittedValueText;
        }

        public RowButton(string label, Func<string> getValueText, Action onLeft, Action onRight, bool repeatOnHold)
        {
            this.Label = label;
            this.GetValueText = getValueText;
            this.OnLeft = onLeft;
            this.OnRight = onRight;
            this.RepeatOnHold = repeatOnHold;
            this.LeftButton = new ClickableTextureComponent(Rectangle.Empty, Game1.mouseCursors, new Rectangle(352, 495, 12, 11), 3f);
            this.RightButton = new ClickableTextureComponent(Rectangle.Empty, Game1.mouseCursors, new Rectangle(365, 495, 12, 11), 3f);
        }

        public string Label { get; }
        public Func<string> GetValueText { get; }
        public Action OnLeft { get; }
        public Action OnRight { get; }
        public bool RepeatOnHold { get; }
        public int LabelX { get; private set; }
        public int LabelY { get; private set; }
        public int ValueX { get; private set; }
        public int ValueY { get; private set; }
        public ClickableTextureComponent LeftButton { get; }
        public ClickableTextureComponent RightButton { get; }

        public void ApplyLayout(int labelX, int labelY, int valueX, int valueY, int leftButtonX, int buttonY, int rightButtonX, int rightButtonY)
        {
            this.LabelX = labelX;
            this.LabelY = labelY;
            this.ValueX = valueX;
            this.ValueY = valueY;
            this.LeftButton.bounds = new Rectangle(leftButtonX, buttonY, 44, 44);
            this.RightButton.bounds = new Rectangle(rightButtonX, rightButtonY, 44, 44);
        }
    }
}
