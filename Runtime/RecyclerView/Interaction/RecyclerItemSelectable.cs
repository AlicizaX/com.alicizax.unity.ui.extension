using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlicizaX.UI
{
    public sealed class RecyclerItemSelectable : Selectable
    {
        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
            Navigation disabledNavigation = navigation;
            disabledNavigation.mode = Navigation.Mode.None;
            navigation = disabledNavigation;
#if !UX_NAVIGATION
            interactable = false;
            enabled = false;
#endif
        }

        public override void OnMove(AxisEventData eventData)
        {
        }
    }
}
