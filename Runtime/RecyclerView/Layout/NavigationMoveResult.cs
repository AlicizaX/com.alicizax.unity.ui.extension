namespace AlicizaX.UI
{
    /// <summary>
    /// 列表内一次方向导航的结果。
    /// </summary>
    public enum NavigationMoveResult
    {
        /// <summary>焦点移到了新的数据项。</summary>
        Moved,

        /// <summary>环绕后仍没有可聚焦项：消费输入但不移动。</summary>
        Blocked,

        /// <summary>到达边界：不消费输入，交给 Selectable.navigation 离开列表。</summary>
        Boundary
    }
}
