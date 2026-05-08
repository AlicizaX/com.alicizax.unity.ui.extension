using UnityEngine.EventSystems;

namespace AlicizaX.UI
{
    internal interface IItemInteractionHost
    {
        ItemInteractionFlags InteractionFlags { get; }

        void HandlePointerClick(PointerEventData eventData);

        void HandlePointerEnter(PointerEventData eventData);

        void HandlePointerExit(PointerEventData eventData);

        void HandleSelect(BaseEventData eventData);

        void HandleDeselect(BaseEventData eventData);

        void HandleMove(AxisEventData eventData);

        void HandleBeginDrag(PointerEventData eventData);

        void HandleDrag(PointerEventData eventData);

        void HandleEndDrag(PointerEventData eventData);

        void HandleSubmit(BaseEventData eventData);

        void HandleCancel(BaseEventData eventData);
    }
}
