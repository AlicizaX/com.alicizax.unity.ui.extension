using UnityEngine.EventSystems;

namespace AlicizaX.UI.NavigationTests
{
    public sealed class IntegrationData : ISimpleViewData
    {
        public int Id;
    }

    public sealed class IntegrationNavigationItem : NavigationViewHolder<IntegrationData>
    {
        public int BoundIndex = -1;
        public int BoundId = -1;
        public int BindCalls;
        public int ClearCalls;
        public int SubmitCalls;
        public int LastSubmittedId = -1;
        public int FocusCalls;
        public bool Focused;
        public bool NotifiedFocused;                       // 只由框架回调写入，模拟不随绑定重置的视觉
        public bool ConsumeMove;
        public bool ConsumeSubmit;
        public uint Version => CurrentBindingVersion;
        public int CurrentIndexForTest => CurrentIndex;

        protected override void OnBind(IntegrationData data, int index)
        {
            BoundIndex = index;
            BoundId = data.Id;
            BindCalls++;
            Focused = false;                 // 新绑定不持有焦点：按约定在 OnBind 里按未聚焦重画
        }

        protected override void OnClear()
        {
            BoundIndex = -1;
            BoundId = -1;
            Focused = false;
            ClearCalls++;
        }

        protected override void OnNavigationFocused(bool focused) { Focused = focused; NotifiedFocused = focused; FocusCalls++; }
        public override bool HandleNavigationMove(AxisEventData eventData) => ConsumeMove;
        public override bool HandleNavigationSubmit()
        {
            SubmitCalls++;
            LastSubmittedId = BoundId;
            return ConsumeSubmit;
        }
    }
}
