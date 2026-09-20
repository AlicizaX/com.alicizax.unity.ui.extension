using UnityEngine.EventSystems;

namespace AlicizaX.UI.NavigationTests
{
    public sealed class IntegrationData : ISimpleViewData
    {
        public int Id;
    }

    public sealed class IntegrationNavigationItem : ViewHolder<IntegrationData>, IRecyclerViewNavigationViewHolder
    {
        public int BoundIndex = -1;
        public int BoundId = -1;
        public int BindCalls;
        public int ClearCalls;
        public int SubmitCalls;
        public int LastSubmittedId = -1;
        public int FocusCalls;
        public bool Focused;
        public bool ConsumeMove;
        public bool ConsumeSubmit;
        public uint Version => CurrentBindingVersion;

        protected override void OnBind(IntegrationData data, int index)
        {
            BoundIndex = index;
            BoundId = data.Id;
            BindCalls++;
            Focused = false;
        }

        protected override void OnClear()
        {
            BoundIndex = -1;
            BoundId = -1;
            Focused = false;
            ClearCalls++;
        }

        public void HandleNavigationFocused(bool focused) { Focused = focused; FocusCalls++; }
        public bool HandleNavigationMove(AxisEventData eventData) => ConsumeMove;
        public bool HandleNavigationSubmit()
        {
            SubmitCalls++;
            LastSubmittedId = BoundId;
            return ConsumeSubmit;
        }
        public bool IsNavigationFocusable(int dataIndex) => true;
    }
}
