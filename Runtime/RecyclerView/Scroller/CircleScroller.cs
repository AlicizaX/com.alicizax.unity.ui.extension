using UnityEngine;
using UnityEngine.EventSystems;

namespace AlicizaX.UI
{
    public class CircleScroller : Scroller
    {
        private RectTransform cachedRect;

        internal override float GetDelta(PointerEventData eventData)
        {
            Vector2 center = GetWorldCenter();
            float delta;
            if (Mathf.Abs(eventData.delta.x) > Mathf.Abs(eventData.delta.y))
            {
                delta = eventData.position.y > center.y ? eventData.delta.x : -eventData.delta.x;
            }
            else
            {
                delta = eventData.position.x < center.x ? eventData.delta.y : -eventData.delta.y;
            }

            return delta * 0.1f;
        }

        private Vector2 GetWorldCenter()
        {
            if (cachedRect == null)
            {
                cachedRect = transform as RectTransform;
            }

            return cachedRect != null ? cachedRect.TransformPoint(cachedRect.rect.center) : transform.position;
        }

        protected override bool StartElasticMotion()
        {
            return false;
        }

        /// <summary>
        /// 环形位置是连续角度，没有边界；周期由布局处理（三角函数、数据索引取模）。
        /// 折回 [0, 360) 会让滚轮目标与插值在 0°/360° 接缝处绕远路反转。
        /// </summary>
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
