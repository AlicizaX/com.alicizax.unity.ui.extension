namespace AlicizaX.UI
{
    public enum MovementType
    {
        Elastic,
        Clamped
    }

    public enum ScrollMotionKind
    {
        None,
        Smooth,
        Duration,
        Inertia,
        Wheel
    }

    public enum ScrollStopReason
    {
        Completed,
        Interrupted,
        Disabled,
        Reset
    }

    public enum ListRefreshMode
    {
        Immediate,
        AfterSettle
    }

    public enum ListScrollPolicy
    {
        KeepAnchor,
        ResetScroll
    }

    public struct ListUpdateOptions
    {
        public ListRefreshMode RefreshMode;
        public bool ApplyRefreshMode;
        public ListScrollPolicy ScrollPolicy;
        public bool Relayout;
        public bool Scroll;
    }
}
