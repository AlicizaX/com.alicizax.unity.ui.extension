using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Cysharp.Text;

namespace AlicizaX.UI
{
    public abstract partial class ViewHolder :
        IPointerClickHandler,
        IPointerEnterHandler,
        IPointerExitHandler,
#if UX_NAVIGATION
        ISelectHandler,
        IDeselectHandler,
        IMoveHandler,
#endif
        IBeginDragHandler,
        IDragHandler,
        IEndDragHandler
#if UX_NAVIGATION
        ,
        ISubmitHandler,
        ICancelHandler
#endif
    {
        [SerializeField]
        private ItemInteractionFlags itemInteractionFlags = ItemInteractionFlags.None;

        private IItemInteractionHost interactionHost;
        private ItemInteractionFlags activeInteractionFlags;
        private RecyclerItemSelectable ownedSelectable;
        private Scroller parentScroller;
        private bool missingSelectableLogged;

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

            if (ownedSelectable != null)
            {
                bool requiresSelection = RequiresSelection(activeInteractionFlags);
                ownedSelectable.interactable = requiresSelection;
                ownedSelectable.enabled = requiresSelection;
            }

            InvalidateNavigationScope();
        }

        internal void ClearInteractionHost()
        {
            interactionHost = null;
            activeInteractionFlags = ItemInteractionFlags.None;
            parentScroller = null;

            if (ownedSelectable != null)
            {
                ownedSelectable.interactable = false;
                ownedSelectable.enabled = false;
            }

            InvalidateNavigationScope();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if ((activeInteractionFlags & ItemInteractionFlags.PointerClick) != 0)
            {
                interactionHost?.HandlePointerClick(eventData);
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if ((activeInteractionFlags & ItemInteractionFlags.PointerEnter) != 0)
            {
                interactionHost?.HandlePointerEnter(eventData);
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if ((activeInteractionFlags & ItemInteractionFlags.PointerExit) != 0)
            {
                interactionHost?.HandlePointerExit(eventData);
            }
        }

        public void OnSelect(BaseEventData eventData)
        {
#if UX_NAVIGATION
#if INPUTSYSTEM_SUPPORT
            UXNavigationRuntime.NotifySelection(gameObject);
#endif
            if ((activeInteractionFlags & ItemInteractionFlags.Select) != 0)
            {
                interactionHost?.HandleSelect(eventData);
            }
#endif
        }

        public void OnDeselect(BaseEventData eventData)
        {
#if UX_NAVIGATION
            if ((activeInteractionFlags & ItemInteractionFlags.Deselect) != 0)
            {
                interactionHost?.HandleDeselect(eventData);
            }
#endif
        }

        public void OnMove(AxisEventData eventData)
        {
#if UX_NAVIGATION
            if ((activeInteractionFlags & ItemInteractionFlags.Move) != 0)
            {
                interactionHost?.HandleMove(eventData);
            }
#endif
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if ((activeInteractionFlags & ItemInteractionFlags.BeginDrag) != 0)
            {
                interactionHost?.HandleBeginDrag(eventData);
                return;
            }

            parentScroller?.OnBeginDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if ((activeInteractionFlags & ItemInteractionFlags.Drag) != 0)
            {
                interactionHost?.HandleDrag(eventData);
                return;
            }

            parentScroller?.OnDrag(eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if ((activeInteractionFlags & ItemInteractionFlags.EndDrag) != 0)
            {
                interactionHost?.HandleEndDrag(eventData);
                return;
            }

            parentScroller?.OnEndDrag(eventData);
        }

        public void OnSubmit(BaseEventData eventData)
        {
#if UX_NAVIGATION
            if ((activeInteractionFlags & ItemInteractionFlags.Submit) != 0)
            {
                interactionHost?.HandleSubmit(eventData);
            }
#endif
        }

        public void OnCancel(BaseEventData eventData)
        {
#if UX_NAVIGATION
            if ((activeInteractionFlags & ItemInteractionFlags.Cancel) != 0)
            {
                interactionHost?.HandleCancel(eventData);
            }
#endif
        }

        private void EnsureFocusAnchor()
        {
            if (focusAnchor != null)
            {
                return;
            }

            focusAnchor = GetComponent<Selectable>();
#if !UX_NAVIGATION
            if (focusAnchor is RecyclerItemSelectable)
            {
                focusAnchor = null;
            }
#endif
            if (focusAnchor != null)
            {
                return;
            }

#if UX_NAVIGATION
            ownedSelectable = GetComponent<RecyclerItemSelectable>();
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (ownedSelectable == null && RequiresSelection(activeInteractionFlags) && !missingSelectableLogged)
            {
                missingSelectableLogged = true;
                Log.Error(ZString.Format("RecyclerItemSelectable is missing on '{0}'. Add it in prefab/editor setup.", GetHierarchyPath(transform)));
            }
#endif
            focusAnchor = ownedSelectable;
#endif
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

            if (selectableCache.Count == 0)
            {
                RefreshInteractionCache();
            }

            for (int i = 0; i < selectableCache.Count; i++)
            {
                Selectable candidate = selectableCache[i];
                if (candidate == focusAnchor)
                {
                    continue;
                }

#if !UX_NAVIGATION
                if (candidate is RecyclerItemSelectable)
                {
                    continue;
                }
#endif
                if (IsSelectableFocusable(candidate))
                {
                    target = candidate.gameObject;
                    return target != null;
                }
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private static string GetHierarchyPath(Transform target)
        {
            if (target == null)
            {
                return "<null>";
            }

            string path = target.name;
            Transform parent = target.parent;
            while (parent != null)
            {
                path = ZString.Format("{0}/{1}", parent.name, path);
                parent = parent.parent;
            }

            return path;
        }
#endif

        [System.Diagnostics.Conditional("INPUTSYSTEM_SUPPORT")]
        private void InvalidateNavigationScope()
        {
#if INPUTSYSTEM_SUPPORT && UX_NAVIGATION
            var scope = GetComponentInParent<UnityEngine.UI.UXNavigationScope>(true);
            scope?.InvalidateSelectableCache();
#endif
        }
    }
}
