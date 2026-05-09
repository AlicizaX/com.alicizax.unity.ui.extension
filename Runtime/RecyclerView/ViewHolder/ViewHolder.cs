using UnityEngine;
using UnityEngine.UI;

namespace AlicizaX.UI
{
    public abstract partial class ViewHolder : UXSelectable
    {
        private RectTransform rectTransform;
        private Selectable focusAnchor;
        private bool interactionCacheReady;

        internal IItemRender CachedItemRender;
        internal string CachedItemRenderViewName;

        public RectTransform RectTransform
        {
            get
            {
                if (rectTransform == null)
                {
                    rectTransform = GetComponent<RectTransform>();
                }

                return rectTransform;
            }
        }

        public string Name { get; internal set; }

        public int Index { get; internal set; }

        public int DataIndex { get; internal set; } = -1;

        public RecyclerView RecyclerView { get; internal set; }

        public uint BindingVersion { get; private set; }

        public Vector2 SizeDelta => RectTransform.sizeDelta;

        protected override void Awake()
        {
            base.Awake();

            Navigation disabledNavigation = navigation;
            disabledNavigation.mode = Navigation.Mode.None;
            navigation = disabledNavigation;
        }

        internal uint AdvanceBindingVersion()
        {
            BindingVersion = BindingVersion == uint.MaxValue ? 1u : BindingVersion + 1u;
            return BindingVersion;
        }

        internal void RefreshInteractionCache()
        {
            if (interactionCacheReady)
            {
                return;
            }

            focusAnchor = GetComponent<Selectable>();
            interactionCacheReady = true;
        }

        internal bool TryGetFocusTarget(out GameObject target)
        {
            if (!SupportsNavigationFocus())
            {
                target = null;
                return false;
            }

            return TryGetInteractionFocusTarget(out target);
        }

        protected internal virtual void OnRecycled()
        {
            UnregisterNavigationScope();
            AdvanceBindingVersion();
            Name = string.Empty;
            Index = -1;
            DataIndex = -1;
            RecyclerView = null;
        }

        private static bool IsSelectableFocusable(Selectable selectable)
        {
            return selectable != null &&
                   selectable.IsActive() &&
                   selectable.IsInteractable();
        }
    }
}
