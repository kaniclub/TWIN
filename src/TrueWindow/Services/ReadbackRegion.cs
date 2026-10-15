namespace TrueWindow.Services;

internal readonly record struct ReadbackRegion(int X, int Y, int Width, int Height)
{
    public static ReadbackRegion FromWorld(int left, int top, int right, int bottom,
        int roomWidth, int roomHeight, int targetWidth, int targetHeight)
    {
        int x = Math.Clamp((int)MathF.Floor(left / (float)Math.Max(1, roomWidth) * targetWidth), 0, targetWidth - 1);
        int y = Math.Clamp((int)MathF.Floor(top / (float)Math.Max(1, roomHeight) * targetHeight), 0, targetHeight - 1);
        int endX = Math.Clamp((int)MathF.Ceiling(right / (float)Math.Max(1, roomWidth) * targetWidth) + 1, x + 1, targetWidth);
        int endY = Math.Clamp((int)MathF.Ceiling(bottom / (float)Math.Max(1, roomHeight) * targetHeight) + 1, y + 1, targetHeight);
        return new(x, y, endX - x, endY - y);
    }
}
