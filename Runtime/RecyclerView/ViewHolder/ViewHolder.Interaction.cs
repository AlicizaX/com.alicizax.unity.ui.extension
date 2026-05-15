using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlicizaX.UI
{
    public abstract partial class ViewHolder :
        IPointerClickHandler,
        ISubmitHandler,
        ICancelHandler
    {
        [SerializeField]
        private ItemInteractionFlags itemInteractionFlags = ItemInteractionFlags.None;

        private IItemInteractionHost interactionHost;
        private ItemInteractionFlags activeInteractionFlags;
        private Scroller parentScroller;
#if INPUTSYSTEM_SUPPORT && UX_NAVIGATION
        private UnityEngine.UI.UXNavigationScope registeredNavigationScope;
#endif

        public ItemInteractionFlags ItemInteractionFlags
        {
            get => itemInteractionFlags;
            set => itemInteractionFlags = value;
        }

        internal void BindInteractionHost(IItemInteractionHost host)
        {
            interactionHost = host;
            activeInteractionFlags = host?.InteractionFlags ?? itemInteractionFlags;
            parentScroller = RecyclerView != null ? RecyclerView.Scroller : null;
            EnsureFocusAnchor();
            RegisterNavigationScopeIfNeeded();

            InvalidateNavigationScope();
        }

        internal void ClearInteractionHost()
        {
            interactionHost = null;
            activeInteractionFlags = ItemInteractionFlags.None;
            parentScroller = null;
            UnregisterNavigationScope();

            InvalidateNavigationScope();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if ((activeInteractionFlags & ItemInteractionFlags.PointerClick) != 0)
            {
                interactionHost?.HandlePointerClick(eventData);
            }
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);

            if ((activeInteractionFlags & ItemInteractionFlags.PointerEnter) != 0)
            {
                interactionHost?.HandlePointerEnter(eventData);
            }
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            base.OnPointerExit(eventData);

            if ((activeInteractionFlags & ItemInteractionFlags.PointerExit) != 0)
            {
                interactionHost?.HandlePointerExit(eventData);
            }
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);

#if INPUTSYSTEM_SUPPORT
#if UX_NAVIGATION
            UXNavigationRuntime.NotifySelection(gameObject);
#endif
#endif
            if ((activeInteractionFlags & ItemInteractionFlags.Select) != 0)
            {
                interactionHost?.HandleSelect(eventData);
            }
        }

        public override void OnDeselect(BaseEventData eventData)
        {
            base.OnDeselect(eventData);

            if ((activeInteractionFlags & ItemInteractionFlags.Deselect) != 0)
            {
                interactionHost?.HandleDeselect(eventData);
            }
        }

        public override void OnMove(AxisEventData eventData)
        {
            if ((activeInteractionFlags & ItemInteractionFlags.Move) != 0)
            {
                interactionHost?.HandleMove(eventData);
                eventData.Use();
            }
        }


        public void OnSubmit(BaseEventData eventData)
        {
            if ((activeInteractionFlags & ItemInteractionFlags.Submit) != 0)
            {
                interactionHost?.HandleSubmit(eventData);
            }
        }

        public void OnCancel(BaseEventData eventData)
        {
            if ((activeInteractionFlags & ItemInteractionFlags.Cancel) != 0)
            {
                interactionHost?.HandleCancel(eventData);
            }
        }

        private void EnsureFocusAnchor()
        {
            if (focusAnchor != null)
            {
                return;
            }

            focusAnchor = this;
        }

        private bool TryGetInteractionFocusTarget(out GameObject target)
        {
            target = null;
            EnsureFocusAnchor();
            if (IsSelectableFocusable(focusAnchor))
            {
                target = focusAnchor.gameObject;
                return target != null;
            }

            return false;
        }

        private static bool RequiresSelection(ItemInteractionFlags interactionFlags)
        {
#if !UX_NAVIGATION
            return false;
#else
            const ItemInteractionFlags selectionFlags =
                ItemInteractionFlags.Select |
                ItemInteractionFlags.Deselect |
                ItemInteractionFlags.Move |
                ItemInteractionFlags.Submit |
                ItemInteractionFlags.Cancel;

            return (interactionFlags & selectionFlags) != 0;
#endif
        }

        internal bool SupportsNavigationFocus()
        {
#if !UX_NAVIGATION
            return false;
#else
            return RequiresSelection(activeInteractionFlags);
#endif
        }

        [System.Diagnostics.Conditional("INPUTSYSTEM_SUPPORT")]
        private void InvalidateNavigationScope()
        {
#if INPUTSYSTEM_SUPPORT && UX_NAVIGATION
            var scope = GetComponentInParent<UnityEngine.UI.UXNavigationScope>(true);
            scope?.InvalidateSelectableCache();
#endif
        }

        private void RegisterNavigationScopeIfNeeded()
        {
#if INPUTSYSTEM_SUPPORT && UX_NAVIGATION
            if (!SupportsNavigationFocus() || focusAnchor == null)
            {
                UnregisterNavigationScope();
                return;
            }

            var scope = GetComponentInParent<UnityEngine.UI.UXNavigationScope>(true);
            if (scope == registeredNavigationScope)
            {
                return;
            }

            UnregisterNavigationScope();
            if (scope != null && scope.RegisterSelectable(focusAnchor))
            {
                registeredNavigationScope = scope;
            }
#endif
        }

        private void UnregisterNavigationScope()
        {
#if INPUTSYSTEM_SUPPORT && UX_NAVIGATION
            if (registeredNavigationScope == null || focusAnchor == null)
            {
                registeredNavigationScope = null;
                return;
            }

            registeredNavigationScope.UnregisterSelectable(focusAnchor);
            registeredNavigationScope = null;
#endif
        }
    }
}
