using UnityEngine.EventSystems;

namespace AlicizaX.UI
{
    /// <summary>
    /// 导航控制器与导航项之间的内部契约。业务通过继承 <see cref="NavigationViewHolder{TData}"/> 参与导航。
    /// 模板实现本接口 = 该模板可参与导航；具体数据索引是否可聚焦由控制器的过滤器决定。
    /// </summary>
    internal interface IRecyclerViewNavigationViewHolder
    {
        /// <summary>
        /// 控制器推送焦点状态；实现方负责去重。
        /// </summary>
        void SetNavigationFocused(bool focused);

        /// <summary>
        /// 焦点项优先处理方向输入；返回 true 表示已消费，控制器不再移动焦点。
        /// </summary>
        bool HandleNavigationMove(AxisEventData eventData);

        /// <summary>
        /// 焦点项处理提交；返回 false 时控制器回退为业务选中。
        /// </summary>
        bool HandleNavigationSubmit();
    }
}
