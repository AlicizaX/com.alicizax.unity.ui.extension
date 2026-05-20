#if INPUTSYSTEM_SUPPORT && UX_NAVIGATION
using UnityEngine.EventSystems;

namespace AlicizaX.UI
{
    /// <summary>
    /// RecyclerView 导航项接口。模板 ViewHolder 实现该接口后才会参与手柄/键盘导航。
    /// </summary>
    public interface IRecyclerViewNavigationViewHolder
    {
        /// <summary>
        /// 设置该 ViewHolder 当前是否为 RecyclerView 的导航焦点，由业务自行更新高亮表现。
        /// </summary>
        void HandleNavigationFocused(bool focused);

        /// <summary>
        /// 在 RecyclerView 移动焦点前，给当前聚焦的 ViewHolder 优先处理方向输入的机会。
        /// 返回 true 表示输入已处理，RecyclerView 不再移动焦点。
        /// </summary>
        bool HandleNavigationMove(AxisEventData eventData);
    }
}
#endif
