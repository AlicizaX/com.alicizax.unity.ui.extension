namespace AlicizaX.UI
{
    public interface IScroller
    {
        float Position { get; set; }

        void ScrollTo(float position, bool smooth = false);
    }
}
