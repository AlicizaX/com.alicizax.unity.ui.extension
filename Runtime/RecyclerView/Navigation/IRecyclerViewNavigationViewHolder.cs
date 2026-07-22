#if INPUTSYSTEM_SUPPORT && UXNAVIGATION_SUPPORT
using UnityEngine.EventSystems;

namespace AlicizaX.UI
{
    /// <summary>
    /// RecyclerView 导航项接口。
    /// 双重门槛：1) 模板实现本接口后才可能参与导航；
    /// 2) <see cref="IsNavigationFocusable"/> 返回 true 时该数据索引才可被聚焦。
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

        /// <summary>
        /// 处理导航提交（手柄确认/键盘 Submit）。
        /// 返回 true 表示提交已由当前 ViewHolder 处理；返回 false 时由导航控制器回退为业务选中。
        /// </summary>
        bool HandleNavigationSubmit();

        /// <summary>
        /// 动态判断指定数据索引当前是否允许被导航聚焦。
        /// 实现接口只表示“该模板可参与导航”；本方法返回 false 时该索引会被跳过。
        /// 离屏项可能在未绑定实例上查询，此时应仅依赖 <paramref name="dataIndex"/> 或共享数据源，不要假设 CurrentData 一定有效。
        /// </summary>
        /// <param name="dataIndex">业务数据索引。</param>
        /// <returns>true 表示可聚焦；false 表示跳过。</returns>
        bool IsNavigationFocusable(int dataIndex);
    }
}
#endif
