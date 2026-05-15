using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlicizaX.UI
{
    public class ScrollbarEx : MonoBehaviour, IBeginDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private RectTransform handle;
        private Scrollbar scrollbar;

        public Action OnDragEnd;

        public bool IsDragging => dragging;

        private bool dragging;
        private bool hovering;
        private float targetHandleScale = 1f;

        private void Awake()
        {
            scrollbar = GetComponent<Scrollbar>();
            handle = scrollbar.handleRect;
            targetHandleScale = GetCurrentHandleScale();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            dragging = true;
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            dragging = false;
            if (!hovering)
            {
                SetHandleScale(1f);
            }

            OnDragEnd?.Invoke();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            hovering = true;
            SetHandleScale(2f);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            hovering = false;
            if (!dragging)
            {
                SetHandleScale(1f);
            }
        }

        private void SetHandleScale(float target)
        {
            if (handle == null || Mathf.Approximately(targetHandleScale, target))
            {
                return;
            }

            targetHandleScale = target;
            bool vertical = IsVerticalScrollbar();
#if PRIMETWEEN_SUPPORT
            if (vertical)
            {
                PrimeTween.Tween.ScaleX(handle, target, 0.2f);
            }
            else
            {
                PrimeTween.Tween.ScaleY(handle, target, 0.2f);
            }
#else
            Vector3 scale = handle.localScale;
            if (vertical)
            {
                scale.x = target;
            }
            else
            {
                scale.y = target;
            }

            handle.localScale = scale;
#endif
        }

        private float GetCurrentHandleScale()
        {
            if (handle == null)
            {
                return 1f;
            }

            return IsVerticalScrollbar() ? handle.localScale.x : handle.localScale.y;
        }

        private bool IsVerticalScrollbar()
        {
            return scrollbar != null &&
                   (scrollbar.direction == Scrollbar.Direction.TopToBottom ||
                    scrollbar.direction == Scrollbar.Direction.BottomToTop);
        }
    }
}
