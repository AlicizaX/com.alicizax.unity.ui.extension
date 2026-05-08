using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    public class UXDraggable:MonoBehaviour, IDragHandler, IBeginDragHandler, IEndDragHandler
    {
        public UnityEvent<PointerEventData> onDrag;
        public UnityEvent<PointerEventData> onBeginDrag;
        public UnityEvent<PointerEventData> onEndDrag;

        void IDragHandler.OnDrag(PointerEventData eventData)
        {
            onDrag?.Invoke(eventData);
        }

        void IBeginDragHandler.OnBeginDrag(PointerEventData eventData)
        {
            onBeginDrag?.Invoke(eventData);
        }

        void IEndDragHandler.OnEndDrag(PointerEventData eventData)
        {
            onEndDrag?.Invoke(eventData);
        }
    }
}
