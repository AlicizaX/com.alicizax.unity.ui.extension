using System.Collections.Generic;
using UnityEngine;

namespace AlicizaX.UI
{
    /// <summary>
    /// Loop 列表的有界虚拟地址空间工具。
    /// 布局 itemCount 为 realCount 的有限倍数，绑定仍按 realIndex 取模。
    /// </summary>
    internal static class LoopVirtualRange
    {
        /// <summary>
        /// 布局可寻址的最大虚拟项数上限（避免 index*stride 超出 float 安全区）。
        /// </summary>
        public const int MaxVirtualItemCount = 50000;

        public const int MinLoopCycles = 3;

        /// <summary>
        /// 虚拟布局项数量：realCount 的奇数倍（便于中段锚点），且不超过上限。
        /// 小列表最多 21 圈；装不下 2 圈时退化为 realCount。
        /// </summary>
        public static int ComputeVirtualItemCount(int realCount)
        {
            if (realCount <= 0)
            {
                return 0;
            }

            // 装不下 2 圈时不做多圈虚拟（避免 virtualCount 与 realCount 不对齐导致数据不可达）
            int maxCycles = MaxVirtualItemCount / realCount;
            if (maxCycles <= 1)
            {
                return realCount;
            }

            int cycles = Mathf.Min(maxCycles, 21);
            if ((cycles & 1) == 0)
            {
                cycles--;
            }

            if (cycles < MinLoopCycles && maxCycles >= MinLoopCycles)
            {
                cycles = MinLoopCycles;
            }

            cycles = Mathf.Max(1, cycles);
            return realCount * cycles;
        }

        public static int GetMiddleAnchorLayoutIndex(int realIndex, int realCount, int virtualCount)
        {
            if (realCount <= 0 || virtualCount <= 0)
            {
                return 0;
            }

            realIndex %= realCount;
            if (realIndex < 0)
            {
                realIndex += realCount;
            }

            if (virtualCount <= realCount)
            {
                return Mathf.Clamp(realIndex, 0, virtualCount - 1);
            }

            int cycles = virtualCount / realCount;
            int middleCycle = cycles / 2;
            return Mathf.Clamp(middleCycle * realCount + realIndex, 0, virtualCount - 1);
        }

        public static bool ShouldReanchor(int layoutIndex, int realCount, int virtualCount)
        {
            if (realCount <= 0 || virtualCount <= realCount)
            {
                return false;
            }

            return layoutIndex < realCount || layoutIndex >= virtualCount - realCount;
        }
    }

    /// <summary>
    /// 循环列表适配器：有界虚拟 itemCount + 绑定取模。
    /// </summary>
    public class LoopAdapter<T> : Adapter<T> where T : class, ISimpleViewData
    {
        public LoopAdapter(RecyclerView recyclerView) : base(recyclerView)
        {
        }

        public LoopAdapter(RecyclerView recyclerView, List<T> list) : base(recyclerView, list)
        {
        }

        public override int GetItemCount()
        {
            int realCount = GetRealCount();
            return realCount <= 0 ? 0 : LoopVirtualRange.ComputeVirtualItemCount(realCount);
        }

        public override int GetRealCount()
        {
            return list == null ? 0 : list.Count;
        }

        public override void OnBindViewHolder(ViewHolder viewHolder, int index)
        {
            int realCount = GetRealCount();
            if (realCount <= 0)
            {
                return;
            }

            int dataIndex = index % realCount;
            if (dataIndex < 0)
            {
                dataIndex += realCount;
            }

            base.OnBindViewHolder(viewHolder, dataIndex);
        }
    }
}
