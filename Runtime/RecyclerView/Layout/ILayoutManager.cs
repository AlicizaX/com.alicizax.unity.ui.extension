using UnityEngine;

namespace AlicizaX.UI
{
    public interface ILayoutManager
    {
        void UpdateLayout();

        void Layout(ViewHolder viewHolder, int index);

        void SetContentSize();

        Vector2 CalculateContentSize();

        Vector2 CalculatePosition(int index);

        Vector2 CalculateContentOffset();

        Vector2 CalculateViewportOffset();

        int GetStartIndex();

        int GetEndIndex();

        float IndexToPosition(int index);

        int PositionToIndex(float position);

        float GetItemStartPosition(int index);

        float GetItemLength(int index);

        int GetSnapIndex(float position);

        void DoItemAnimation();

        bool IsFullVisibleStart(int index);

        bool IsFullInvisibleStart(int index);

        bool IsFullVisibleEnd(int index);

        bool IsFullInvisibleEnd(int index);

        bool IsVisible(int index);
    }
}
