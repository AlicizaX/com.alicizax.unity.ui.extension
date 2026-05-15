using UnityEngine;
using UnityEngine.EventSystems;

namespace AlicizaX.UI
{
    public sealed class RecyclerNavigationController
    {
        private readonly RecyclerView recyclerView;
        private RecyclerNavigationBridge navigationBridge;
        private bool navigationBridgeLookupDone;

        public RecyclerNavigationController(RecyclerView recyclerView)
        {
            this.recyclerView = recyclerView;
        }

        public bool TryMove(ViewHolder currentHolder, MoveDirection direction, RecyclerNavigationOptions options)
        {
#if !UX_NAVIGATION
            return false;
#else
            if (recyclerView == null ||
                recyclerView.RecyclerViewAdapter == null ||
                currentHolder == null)
            {
                return false;
            }

            int itemCount = recyclerView.RecyclerViewAdapter.GetItemCount();
            int realCount = recyclerView.RecyclerViewAdapter.GetRealCount();
            if (itemCount <= 0 || realCount <= 0)
            {
                return false;
            }

            int step = GetStep(direction);
            if (step == 0)
            {
                return false;
            }

            bool isLoopSource = itemCount != realCount;
            bool allowWrap = isLoopSource && options.Wrap;
            int originalIndex = currentHolder.DataIndex;
            int currentIndex = currentHolder.DataIndex;
            int nextIndex = currentIndex;
            int stepAbs = Mathf.Abs(step);
            int maxAttempts = allowWrap ? (realCount + stepAbs - 1) / stepAbs : itemCount;
            ScrollAlignment alignment = ResolveAlignment(direction, options.Alignment);
            int visibleStepBuffer = ResolveVisibleStepBuffer(options);
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                nextIndex += step;
                if (allowWrap)
                {
                    nextIndex = WrapIndex(nextIndex, realCount);
                }
                else
                {
                    nextIndex = Mathf.Clamp(nextIndex, 0, itemCount - 1);
                    if (nextIndex == currentIndex)
                    {
                        break;
                    }
                }

                bool preserveViewportPosition = ShouldPreserveViewportPosition(
                    direction,
                    nextIndex,
                    step,
                    itemCount,
                    allowWrap,
                    visibleStepBuffer);

                if (preserveViewportPosition
                    ? TryFocusIndexPreservingViewportPosition(nextIndex, originalIndex, options.SmoothScroll, alignment)
                    : recyclerView.TryFocusIndex(nextIndex, options.SmoothScroll, alignment))
                {
                    return true;
                }

                currentIndex = nextIndex;
            }

            recyclerView.TryFocusIndex(originalIndex, false, options.Alignment);
            return false;
#endif
        }

        private int GetStep(MoveDirection direction)
        {
            int unit = Mathf.Max(1, recyclerView.LayoutManager != null ? recyclerView.LayoutManager.Unit : 1);
            bool vertical = recyclerView.Direction == Direction.Vertical;

            return direction switch
            {
                MoveDirection.Left => vertical ? -1 : -unit,
                MoveDirection.Right => vertical ? 1 : unit,
                MoveDirection.Up => vertical ? -unit : -1,
                MoveDirection.Down => vertical ? unit : 1,
                _ => 0
            };
        }

        private static int WrapIndex(int index, int count)
        {
            int wrapped = index % count;
            return wrapped < 0 ? wrapped + count : wrapped;
        }

        private int ResolveVisibleStepBuffer(RecyclerNavigationOptions options)
        {
            if (options.VisibleStepBuffer >= 0)
            {
                return options.VisibleStepBuffer;
            }

            return NavigationBridge != null ? NavigationBridge.VisibleStepBuffer : 0;
        }

        private bool ShouldPreserveViewportPosition(
            MoveDirection direction,
            int targetIndex,
            int step,
            int itemCount,
            bool allowWrap,
            int visibleStepBuffer)
        {
            if (recyclerView == null ||
                recyclerView.LayoutManager == null ||
                allowWrap ||
                visibleStepBuffer <= 0 ||
                !IsPrimaryAxisMove(direction))
            {
                return false;
            }

            int stepMagnitude = Mathf.Abs(step);
            if (stepMagnitude <= 0 || !TryGetVisibleIndexBounds(true, out int visibleStart, out int visibleEnd))
            {
                return false;
            }

            if (step > 0)
            {
                if (targetIndex + stepMagnitude > itemCount - 1)
                {
                    return false;
                }

                return (visibleEnd - targetIndex) / stepMagnitude < visibleStepBuffer;
            }

            if (targetIndex - stepMagnitude < 0)
            {
                return false;
            }

            return (targetIndex - visibleStart) / stepMagnitude < visibleStepBuffer;
        }

        private bool TryFocusIndexPreservingViewportPosition(
            int index,
            int referenceIndex,
            bool smooth,
            ScrollAlignment fallbackAlignment)
        {
            if (recyclerView == null ||
                recyclerView.RecyclerViewAdapter == null ||
                recyclerView.RecyclerViewAdapter.GetItemCount() <= 0 ||
                recyclerView.LayoutManager == null ||
                recyclerView.Scroller == null ||
                recyclerView.Scroll == ScrollMode.AlwaysDisable ||
                index < 0 ||
                index >= recyclerView.RecyclerViewAdapter.GetItemCount())
            {
                return recyclerView != null && recyclerView.TryFocusIndex(index, smooth, fallbackAlignment);
            }

            if (!recyclerView.TryGetVisibleViewHolder(index, out ViewHolder holder) || !recyclerView.IsFullyVisible(holder))
            {
                return recyclerView.TryFocusIndex(index, smooth, fallbackAlignment);
            }

            if (!recyclerView.TryGetVisibleViewHolder(referenceIndex, out ViewHolder referenceHolder) || !recyclerView.IsFullyVisible(referenceHolder))
            {
                return recyclerView.TryFocusIndex(index, smooth, fallbackAlignment);
            }

            Scroller scroller = recyclerView.Scroller;
            float currentPosition = scroller.Position;
            int referenceLayoutIndex = recyclerView.LayoutManager.GetLayoutIndex(referenceIndex);
            int targetLayoutIndex = recyclerView.LayoutManager.GetLayoutIndex(index);
            float referencePosition = recyclerView.LayoutManager.GetItemStartPosition(referenceLayoutIndex);
            float targetPosition = recyclerView.LayoutManager.GetItemStartPosition(targetLayoutIndex);
            float desiredPosition = scroller.ClampPosition(currentPosition + (targetPosition - referencePosition));

            if (!Mathf.Approximately(currentPosition, desiredPosition))
            {
                scroller.ScrollTo(desiredPosition, smooth);
                if (!smooth)
                {
                    if (!recyclerView.TryGetVisibleViewHolder(index, out holder))
                    {
                        recyclerView.Refresh();
                        recyclerView.TryGetVisibleViewHolder(index, out holder);
                    }

                    if (holder == null || !recyclerView.IsFullyVisible(holder))
                    {
                        return recyclerView.TryFocusIndex(index, false, fallbackAlignment);
                    }
                }
            }

            if (holder == null || !recyclerView.TryResolveFocusTarget(holder, out GameObject target))
            {
                return false;
            }

            recyclerView.ApplyFocus(target);
            recyclerView.UpdateFocusIndex(index);
            recyclerView.UpdateCurrentIndex(index);
            return true;
        }

        private bool TryGetVisibleIndexBounds(bool fullyVisibleOnly, out int minIndex, out int maxIndex)
        {
            minIndex = int.MaxValue;
            maxIndex = int.MinValue;
            if (recyclerView == null)
            {
                return false;
            }

            ViewProvider viewProvider = recyclerView.ViewProvider;
            if (viewProvider == null || viewProvider.VisibleCount == 0)
            {
                return false;
            }

            for (int i = 0; i < viewProvider.VisibleCount; i++)
            {
                ViewHolder holder = viewProvider.GetVisibleViewHolder(i);
                if (holder == null || holder.DataIndex < 0)
                {
                    continue;
                }

                if (fullyVisibleOnly && !recyclerView.IsFullyVisible(holder))
                {
                    continue;
                }

                if (holder.DataIndex < minIndex)
                {
                    minIndex = holder.DataIndex;
                }

                if (holder.DataIndex > maxIndex)
                {
                    maxIndex = holder.DataIndex;
                }
            }

            return minIndex != int.MaxValue && maxIndex != int.MinValue;
        }

        private bool IsPrimaryAxisMove(MoveDirection direction)
        {
            if (recyclerView == null)
            {
                return false;
            }

            return recyclerView.Direction switch
            {
                Direction.Vertical => direction is MoveDirection.Up or MoveDirection.Down,
                Direction.Horizontal => direction is MoveDirection.Left or MoveDirection.Right,
                _ => false
            };
        }

        private RecyclerNavigationBridge NavigationBridge
        {
            get
            {
                if (!navigationBridgeLookupDone)
                {
                    navigationBridge = recyclerView != null ? recyclerView.GetComponent<RecyclerNavigationBridge>() : null;
                    navigationBridgeLookupDone = true;
                }

                return navigationBridge;
            }
        }

        private ScrollAlignment ResolveAlignment(MoveDirection direction, ScrollAlignment fallback)
        {
            if (recyclerView == null || recyclerView.LayoutManager == null)
            {
                return fallback;
            }

            bool vertical = recyclerView.Direction == Direction.Vertical;
            return direction switch
            {
                MoveDirection.Down when vertical => ScrollAlignment.End,
                MoveDirection.Up when vertical => ScrollAlignment.Start,
                MoveDirection.Right when !vertical => ScrollAlignment.End,
                MoveDirection.Left when !vertical => ScrollAlignment.Start,
                _ => fallback
            };
        }
    }
}
