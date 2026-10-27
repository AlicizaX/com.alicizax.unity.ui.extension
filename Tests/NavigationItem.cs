using UnityEngine.EventSystems;

namespace AlicizaX.UI.NavigationTests
{
    public sealed class NavigationData : IGroupViewData
    {
        public int TemplateId { get; set; }
        public bool Expanded { get; set; }
        public int Type { get; set; }
    }

    public sealed class NavigationItem : ViewHolder<NavigationData>, IRecyclerViewNavigationViewHolder
    {
        public int ExcludedIndex = -1;
        public bool ExcludeAll;
        protected override void OnBind(NavigationData data, int index) { }
        public void HandleNavigationFocused(bool focused) { }
        public bool HandleNavigationMove(AxisEventData eventData) => false;
        public bool HandleNavigationSubmit() => false;
        public bool IsNavigationFocusable(int dataIndex) => !ExcludeAll && dataIndex != ExcludedIndex;
    }
}
