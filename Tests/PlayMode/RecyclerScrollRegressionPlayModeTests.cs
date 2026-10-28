namespace AlicizaX.UI.NavigationTests
{
    /// <summary>
    /// PlayMode 版：此模式才会走 Awake/OnEnable，从而真实验证 Scroller 事件到 RecyclerView 的链路。
    /// </summary>
    public sealed class RecyclerScrollRegressionPlayModeTests : RecyclerScrollRegressionCases
    {
    }
}
