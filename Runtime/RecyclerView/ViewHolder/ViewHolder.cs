using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlicizaX.UI
{
    public abstract partial class ViewHolder : MonoBehaviour
    {
        private RectTransform rectTransform;
        private Selectable focusAnchor;
        private readonly List<Selectable> selectableCache = new();
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
            selectableCache.Clear();
            GetComponentsInChildren(true, selectableCache);
            interactionCacheReady = true;
        }

        internal bool TryGetFocusTarget(out GameObject target)
        {
            if (TryGetInteractionFocusTarget(out target))
            {
                return true;
            }

            Selectable selectable = IsSelectableFocusable(focusAnchor) ? focusAnchor : null;
            if (selectable == null)
            {
                for (int i = 0; i < selectableCache.Count; i++)
                {
                    if (IsSelectableFocusable(selectableCache[i]))
                    {
                        selectable = selectableCache[i];
                        break;
                    }
                }
            }

            if (selectable != null)
            {
                target = selectable.gameObject;
                return true;
            }

            if (!SupportsNavigationFocus())
            {
                target = null;
                return false;
            }

            if (ExecuteEvents.CanHandleEvent<IMoveHandler>(gameObject) ||
                ExecuteEvents.CanHandleEvent<ISelectHandler>(gameObject) ||
                ExecuteEvents.CanHandleEvent<ISubmitHandler>(gameObject))
            {
                target = gameObject;
                return true;
            }

            target = null;
            return false;
        }

        protected internal virtual void OnRecycled()
        {
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
