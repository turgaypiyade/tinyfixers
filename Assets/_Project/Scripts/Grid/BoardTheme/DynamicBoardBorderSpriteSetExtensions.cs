public static class DynamicBoardBorderSpriteSetExtensions
{
    public static void ApplySpriteSet(this DynamicBoardBorder border, BorderSpriteSet set)
    {
        if (border == null || set == null)
            return;

        set.ApplyTo(border);
    }
}
