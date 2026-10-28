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

        int GetStartIndex();

        int GetEndIndex();

        float IndexToPosition(int index);

        int PositionToIndex(float position);

        float GetItemStartPosition(int index);

        float GetItemLength(int index);

        int GetSnapIndex(float position);

        void DoItemAnimation();
    }
}
