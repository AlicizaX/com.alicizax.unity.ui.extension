using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlicizaX.UI
{
    [AddComponentMenu("UI/Recycler Navigation")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RecyclerView))]
    public sealed class RecyclerNavigationBridge : Selectable, ISelectHandler, IMoveHandler, ISubmitHandler
    {
        [SerializeField] private MoveDirection defaultEntryDirection = MoveDirection.Down;
        [SerializeField, Min(0)] private int visibleStepBuffer;

        private RecyclerView recyclerView;

        public int VisibleStepBuffer
        {
            get => visibleStepBuffer;
            set => visibleStepBuffer = Mathf.Max(0, value);
        }

        protected override void Awake()
        {
            base.Awake();
            transition = Transition.None;
            Navigation navigationConfig = navigation;
            navigationConfig.mode = Navigation.Mode.None;
            navigation = navigationConfig;
#if !UX_NAVIGATION
            interactable = false;
            enabled = false;
            return;
#endif
            recyclerView = GetComponent<RecyclerView>();
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
#if INPUTSYSTEM_SUPPORT && UX_NAVIGATION
            UXNavigationRuntime.NotifySelection(gameObject);
#endif
            TryEnter(defaultEntryDirection);
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (!TryEnter(eventData.moveDir))
            {
                base.OnMove(eventData);
            }
        }

        public void OnSubmit(BaseEventData eventData)
        {
            TryEnter(defaultEntryDirection);
        }

        private bool TryEnter(MoveDirection direction)
        {
#if UX_NAVIGATION
            recyclerView ??= GetComponent<RecyclerView>();
            return recyclerView != null && recyclerView.TryFocusEntry(direction);
#else
            return false;
#endif
        }
    }
}
