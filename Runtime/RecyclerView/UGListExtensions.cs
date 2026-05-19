using System;
using UnityEngine;

namespace AlicizaX.UI
{
    /// <summary>
    /// 提供 UGList 的常用扩展方法。
    /// </summary>
    public static class UGListExtensions
    {
        /// <summary>
        /// 将列表滚动到指定索引，并按给定对齐方式定位。
        /// </summary>
        /// <typeparam name="TData">列表数据类型。</typeparam>
        /// <typeparam name="TAdapter">适配器类型。</typeparam>
        /// <param name="ugList">目标列表实例。</param>
        /// <param name="index">目标数据索引。</param>
        /// <param name="alignment">滚动完成后的对齐方式。</param>
        /// <param name="offset">在对齐基础上的额外偏移量。</param>
        /// <param name="smooth">是否使用平滑滚动。</param>
        /// <param name="duration">平滑滚动时长，单位为秒。</param>
        public static void ScrollTo<TData, TAdapter>(
            this UGListBase<TData, TAdapter> ugList,
            int index,
            ScrollAlignment alignment = ScrollAlignment.Start,
            float offset = 0f,
            bool smooth = false,
            float duration = 0.3f)
            where TAdapter : Adapter<TData>
            where TData : class, ISimpleViewData
        {
            if (ugList?.RecyclerView == null)
            {
                Log.Warning("UGList or RecyclerView is null");
                return;
            }

            ugList.ScrollTo(index, alignment, offset, smooth, duration);
        }

        /// <summary>
        /// 将列表滚动到指定索引，并使目标项靠近起始端。
        /// </summary>
        /// <typeparam name="TData">列表数据类型。</typeparam>
        /// <typeparam name="TAdapter">适配器类型。</typeparam>
        /// <param name="ugList">目标列表实例。</param>
        /// <param name="index">目标数据索引。</param>
        /// <param name="offset">在起始对齐基础上的额外偏移量。</param>
        /// <param name="smooth">是否使用平滑滚动。</param>
        /// <param name="duration">平滑滚动时长，单位为秒。</param>
        public static void ScrollToStart<TData, TAdapter>(
            this UGListBase<TData, TAdapter> ugList,
            int index,
            float offset = 0f,
            bool smooth = false,
            float duration = 0.3f)
            where TAdapter : Adapter<TData>
            where TData : class, ISimpleViewData
        {
            ugList.ScrollToStart(index, offset, smooth, duration);
        }

        /// <summary>
        /// 将列表滚动到指定索引，并使目标项居中显示。
        /// </summary>
        /// <typeparam name="TData">列表数据类型。</typeparam>
        /// <typeparam name="TAdapter">适配器类型。</typeparam>
        /// <param name="ugList">目标列表实例。</param>
        /// <param name="index">目标数据索引。</param>
        /// <param name="offset">在居中对齐基础上的额外偏移量。</param>
        /// <param name="smooth">是否使用平滑滚动。</param>
        /// <param name="duration">平滑滚动时长，单位为秒。</param>
        public static void ScrollToCenter<TData, TAdapter>(
            this UGListBase<TData, TAdapter> ugList,
            int index,
            float offset = 0f,
            bool smooth = false,
            float duration = 0.3f)
            where TAdapter : Adapter<TData>
            where TData : class, ISimpleViewData
        {
            ugList.ScrollToCenter(index, offset, smooth, duration);
        }

        /// <summary>
        /// 将列表滚动到指定索引，并使目标项靠近末端。
        /// </summary>
        /// <typeparam name="TData">列表数据类型。</typeparam>
        /// <typeparam name="TAdapter">适配器类型。</typeparam>
        /// <param name="ugList">目标列表实例。</param>
        /// <param name="index">目标数据索引。</param>
        /// <param name="offset">在末端对齐基础上的额外偏移量。</param>
        /// <param name="smooth">是否使用平滑滚动。</param>
        /// <param name="duration">平滑滚动时长，单位为秒。</param>
        public static void ScrollToEnd<TData, TAdapter>(
            this UGListBase<TData, TAdapter> ugList,
            int index,
            float offset = 0f,
            bool smooth = false,
            float duration = 0.3f)
            where TAdapter : Adapter<TData>
            where TData : class, ISimpleViewData
        {
            ugList.ScrollToEnd(index, offset, smooth, duration);
        }
    }
}
