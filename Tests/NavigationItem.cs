namespace AlicizaX.UI.NavigationTests
{
    public sealed class NavigationData : IGroupViewData
    {
        public int TemplateId { get; set; }
        public bool Expanded { get; set; }
        public int Type { get; set; }
    }

    public sealed class NavigationItem : NavigationViewHolder<NavigationData>
    {
        protected override void OnBind(NavigationData data, int index) { }
    }
}
