using UnityEngine;
using UnityEngine.EventSystems;

namespace AlicizaX.UI
{
    public class CircleScroller : Scroller
    {
        private Vector2 centerPosition;

        protected override void Awake()
        {
            base.Awake();
            RectTransform rectTransform = GetComponent<RectTransform>();
            Vector2 position = transform.position;
            Vector2 size = rectTransform.sizeDelta;

            if (rectTransform.pivot.x == 0)
            {
                centerPosition.x = position.x + size.x / 2f;
            }
            else if (rectTransform.pivot.x == 0.5f)
            {
                centerPosition.x = position.x;
            }
            else
            {
                centerPosition.x = position.x - size.x / 2f;
            }

            if (rectTransform.pivot.y == 0)
            {
                centerPosition.y = position.y + size.y / 2f;
            }
            else if (rectTransform.pivot.y == 0.5f)
            {
                centerPosition.y = position.y;
            }
            else
            {
                centerPosition.y = position.y - size.y / 2f;
            }
        }

        internal override float GetDelta(PointerEventData eventData)
        {
            float delta;
            if (Mathf.Abs(eventData.delta.x) > Mathf.Abs(eventData.delta.y))
            {
                delta = eventData.position.y > centerPosition.y ? eventData.delta.x : -eventData.delta.x;
            }
            else
            {
                delta = eventData.position.x < centerPosition.x ? eventData.delta.y : -eventData.delta.y;
            }
            return delta * 0.1f;
        }

        protected override bool StartElasticMotion()
        {
            return false;
        }

        public override float ClampPosition(float value)
        {
            return value;
        }

        protected override float GetOverscroll(float pos)
        {
            return 0f;
        }

        protected override bool IsInBounds(float pos)
        {
            return true;
        }

        protected override float GetScrollRate()
        {
            return 1f;
        }
    }
}
