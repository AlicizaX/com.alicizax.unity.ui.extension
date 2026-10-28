using System;
using System.Collections.Generic;
using UnityEngine;

namespace AlicizaX.UI
{
    /// <summary>
    /// 列表项对象池。入栈只有 <see cref="Free"/> 一条路径（含 <see cref="Warm"/>），出栈只有
    /// <see cref="Allocate"/>，因此预热实例与回收实例的后置状态完全一致。
    /// 重复入池由池自身持有的成员集合识别，不依赖 <see cref="ViewHolder"/> 上的隐藏状态。
    /// </summary>
    internal sealed class ViewHolderPool
    {
        private const int DefaultMaxSizePerTemplate = 10;

        private readonly ViewHolderFactory factory;
        private readonly Stack<ViewHolder>[] freeStacks;
        private readonly int[] maxSizeByTemplate;
        private readonly int[] activeByTemplate;
        private readonly int[] peakActiveByTemplate;
        private readonly HashSet<ViewHolder> pooledInstances;
        private int hitCount;
        private int missCount;
        private int destroyCount;
        private bool disposed;

        public ViewHolderPool(ViewHolderFactory factory, int templateCount)
        {
            this.factory = factory;

            int count = Mathf.Max(1, templateCount);
            freeStacks = new Stack<ViewHolder>[count];
            maxSizeByTemplate = new int[count];
            activeByTemplate = new int[count];
            peakActiveByTemplate = new int[count];
            pooledInstances = new HashSet<ViewHolder>(count * DefaultMaxSizePerTemplate);
            for (int i = 0; i < count; i++)
            {
                freeStacks[i] = new Stack<ViewHolder>(DefaultMaxSizePerTemplate);
                maxSizeByTemplate[i] = DefaultMaxSizePerTemplate;
            }
        }

        public int HitCount => hitCount;

        public int MissCount => missCount;

        public int DestroyCount => destroyCount;

        public int TemplateCapacity => freeStacks.Length;

        public ViewHolder Allocate(int templateId)
        {
            Debug.Assert(IsValidTemplate(templateId), "ViewHolderPool.Allocate: template id out of range.");

            Stack<ViewHolder> stack = freeStacks[templateId];
            while (stack.Count > 0)
            {
                ViewHolder holder = stack.Pop();
                pooledInstances.Remove(holder);
                if (holder == null)
                {
                    // 实例在池内被外部销毁：计数上界仍在，丢弃即可。
                    destroyCount++;
                    continue;
                }

                hitCount++;
                TrackAllocate(templateId);
                return holder;
            }

            missCount++;
            ViewHolder created = factory.Create(templateId);
            if (created != null)
            {
                TrackAllocate(templateId);
            }

            return created;
        }

        public void Free(int templateId, ViewHolder holder)
        {
            Return(templateId, holder, true);
        }

        /// <summary>
        /// 唯一的入池实现。预热创建的实例从未被 Allocate 过，因此不能计入"活跃数"，否则活跃计数会被预热抹平。
        /// </summary>
        private void Return(int templateId, ViewHolder holder, bool trackActive)
        {
            if (holder == null)
            {
                return;
            }

            if (!pooledInstances.Add(holder))
            {
                // 重复入池：再折一次会把同一实例放进栈两次，绝不允许。宁可炸掉，也不产生两份所有权。
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                throw new InvalidOperationException("ViewHolderPool.Free: the same view holder was released twice.");
#else
                return;
#endif
            }

            if (trackActive)
            {
                TrackFree(templateId);
            }

            if (disposed || maxSizeByTemplate[templateId] <= 0 ||
                freeStacks[templateId].Count >= maxSizeByTemplate[templateId])
            {
                pooledInstances.Remove(holder);
                factory.Destroy(holder);
                destroyCount++;
                return;
            }

            factory.Reset(holder);
            freeStacks[templateId].Push(holder);
        }

        /// <summary>
        /// 预热：走 <see cref="Free"/> 的同一条入栈路径，保证 Reset/成员集合的后置条件一致。
        /// </summary>
        public void Warm(int templateId, int count)
        {
            if (disposed || count <= 0 || !IsValidTemplate(templateId))
            {
                return;
            }

            int maxSize = maxSizeByTemplate[templateId];
            if (maxSize <= 0)
            {
                return;
            }

            if (count > maxSize)
            {
                count = maxSize;
            }

            Stack<ViewHolder> stack = freeStacks[templateId];
            while (stack.Count < count)
            {
                ViewHolder created = factory.Create(templateId);
                if (created == null)
                {
                    return;
                }

                // 与 Free 同一条入栈路径（Reset + 成员集合），但不计入活跃数。
                Return(templateId, created, false);
            }
        }

        /// <summary>
        /// 把 <paramref name="templateId"/> 的池上限抬到 <paramref name="value"/>。<c>0</c> 表示不池化。
        /// </summary>
        public void EnsureCapacity(int templateId, int value)
        {
            if (value <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Pool capacity must be greater than zero.");
            }

            if (!IsValidTemplate(templateId))
            {
                throw new ArgumentOutOfRangeException(nameof(templateId));
            }

            if (value > maxSizeByTemplate[templateId])
            {
                maxSizeByTemplate[templateId] = value;
            }
        }

        public int GetMaxSize(int templateId)
        {
            return IsValidTemplate(templateId) ? maxSizeByTemplate[templateId] : 0;
        }

        /// <summary>
        /// 设置池上限；<c>0</c> 表示该模板不保留空闲实例（回收即销毁）。
        /// </summary>
        public void SetMaxSize(int templateId, int value)
        {
            if (value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), "Pool capacity cannot be negative.");
            }

            if (!IsValidTemplate(templateId))
            {
                throw new ArgumentOutOfRangeException(nameof(templateId));
            }

            maxSizeByTemplate[templateId] = value;
            if (value == 0)
            {
                TrimTemplate(templateId);
            }
        }

        public int GetActiveCount(int templateId)
        {
            return IsValidTemplate(templateId) ? activeByTemplate[templateId] : 0;
        }

        public int GetPeakActiveCount(int templateId)
        {
            return IsValidTemplate(templateId) ? peakActiveByTemplate[templateId] : 0;
        }

        public void TrimInactive()
        {
            for (int templateId = 0; templateId < freeStacks.Length; templateId++)
            {
                TrimTemplate(templateId);
            }
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            TrimInactive();
            pooledInstances.Clear();
        }

        private void TrimTemplate(int templateId)
        {
            Stack<ViewHolder> stack = freeStacks[templateId];
            while (stack.Count > 0)
            {
                ViewHolder holder = stack.Pop();
                pooledInstances.Remove(holder);
                if (holder == null)
                {
                    continue;
                }

                factory.Destroy(holder);
                destroyCount++;
            }
        }

        private void TrackAllocate(int templateId)
        {
            int active = activeByTemplate[templateId] + 1;
            activeByTemplate[templateId] = active;
            if (active > peakActiveByTemplate[templateId])
            {
                peakActiveByTemplate[templateId] = active;
            }
        }

        private void TrackFree(int templateId)
        {
            // Warm 走 Free 时并没有对应的 Allocate，因此这里必须夹紧到 0。
            int active = activeByTemplate[templateId];
            if (active > 0)
            {
                activeByTemplate[templateId] = active - 1;
            }
        }

        private bool IsValidTemplate(int templateId)
        {
            return templateId >= 0 && templateId < freeStacks.Length;
        }
    }
}
