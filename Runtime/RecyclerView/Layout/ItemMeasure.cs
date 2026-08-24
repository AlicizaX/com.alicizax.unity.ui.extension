using UnityEngine;
using UnityEngine.UI;

namespace AlicizaX.UI
{
    internal static class ItemMeasure
    {
        public static bool IsStretchedOnAxis(RectTransform rect, Direction direction)
        {
            if (rect == null)
            {
                return false;
            }

            return direction == Direction.Horizontal
                ? !Mathf.Approximately(rect.anchorMin.x, rect.anchorMax.x)
                : !Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y);
        }

        public static float ReadAxisLength(RectTransform rect, Direction direction)
        {
            if (rect == null)
            {
                return 0f;
            }

            Vector2 size = rect.rect.size;
            float length = direction == Direction.Horizontal ? size.x : size.y;
            if (length > 0f)
            {
                return length;
            }

            int axis = direction == Direction.Horizontal ? 0 : 1;
            return Mathf.Max(LayoutUtility.GetPreferredSize(rect, axis), 0f);
        }

        public static float MeasureBound(RectTransform rect, Direction direction)
        {
            if (rect == null)
            {
                return 0f;
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            return ReadAxisLength(rect, direction);
        }
    }
}
