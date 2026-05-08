namespace AlicizaX.UI
{
    public readonly struct RecyclerNavigationOptions
    {
        public static readonly RecyclerNavigationOptions Clamped = new(false, false, ScrollAlignment.Center);
        public static readonly RecyclerNavigationOptions Circular = new(true, false, ScrollAlignment.Center);

        public RecyclerNavigationOptions(bool wrap, bool smoothScroll, ScrollAlignment alignment, int visibleStepBuffer = -1)
        {
            Wrap = wrap;
            SmoothScroll = smoothScroll;
            Alignment = alignment;
            VisibleStepBuffer = visibleStepBuffer;
        }

        public bool Wrap { get; }

        public bool SmoothScroll { get; }

        public ScrollAlignment Alignment { get; }

        public int VisibleStepBuffer { get; }
    }
}
