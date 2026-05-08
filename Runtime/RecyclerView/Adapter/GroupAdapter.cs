using System;
using System.Collections.Generic;

namespace AlicizaX.UI
{
    public class GroupAdapter<TData> : Adapter<TData> where TData : IGroupViewData, new()
    {
        private readonly List<TData> showList = new();
        private readonly string groupViewName;
        private readonly Dictionary<int, TData> groupsByType = new();

        public GroupAdapter(RecyclerView recyclerView, string groupViewName) : base(recyclerView)
        {
            this.groupViewName = groupViewName;
        }

        public GroupAdapter(RecyclerView recyclerView, List<TData> list) : base(recyclerView, list)
        {
        }

        public GroupAdapter(RecyclerView recyclerView) : base(recyclerView)
        {
        }

        public override int GetItemCount()
        {
            return showList.Count;
        }

        public override int GetRealCount()
        {
            return showList.Count;
        }

        public override string GetViewName(int index)
        {
            return index >= 0 && index < showList.Count
                ? showList[index].TemplateName
                : string.Empty;
        }

        public override void NotifyDataChanged()
        {
            if (string.IsNullOrEmpty(groupViewName))
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Log.Error("GroupAdapter requires a non-empty groupViewName.");
#endif
                return;
            }

            if (list == null)
            {
                showList.Clear();
                groupsByType.Clear();
                base.NotifyDataChanged();
                return;
            }

            for (int i = 0; i < list.Count; i++)
            {
                CreateGroup(list[i].Type);
            }

            for (int i = 0; i < showList.Count; i++)
            {
                TData group = showList[i];
                if (group.TemplateName != groupViewName)
                {
                    continue;
                }

                CollapseInternal(i);
                if (group.Expanded)
                {
                    ExpandInternal(i);
                    i += CountItemsForType(group.Type);
                }
            }

            RemoveEmptyGroups();
            base.NotifyDataChanged();
        }

        public override void SetList(List<TData> list)
        {
            showList.Clear();
            groupsByType.Clear();
            base.SetList(list);
        }

        private void CreateGroup(int type)
        {
            if (groupsByType.ContainsKey(type))
            {
                return;
            }

            TData groupData = new TData
            {
                TemplateName = groupViewName,
                Type = type
            };
            groupsByType[type] = groupData;
            showList.Add(groupData);
        }

        public void Expand(int index)
        {
            SetExpanded(index, true);
        }

        public void Collapse(int index)
        {
            SetExpanded(index, false);
        }

        public bool SetExpanded(int index, bool expanded)
        {
            if (!TryGetDisplayData(index, out TData data) || !IsGroupIndex(index))
            {
                return false;
            }

            data.Expanded = expanded;
            NotifyDataChanged();
            return true;
        }

        public bool IsGroupIndex(int index)
        {
            return index >= 0 &&
                   index < showList.Count &&
                   string.Equals(showList[index].TemplateName, groupViewName, StringComparison.Ordinal);
        }

        public bool TryGetDisplayData(int index, out TData data)
        {
            if (list == null || index < 0 || index >= showList.Count)
            {
                data = default;
                return false;
            }

            data = showList[index];
            return true;
        }

        private void ExpandInternal(int index)
        {
            if (list == null || index < 0 || index >= showList.Count)
            {
                return;
            }

            int type = showList[index].Type;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Type == type)
                {
                    showList.Insert(index + 1, list[i]);
                    index++;
                }
            }
        }

        private void CollapseInternal(int index)
        {
            if (index < 0 || index >= showList.Count)
            {
                return;
            }

            int type = showList[index].Type;
            int removeCount = 0;
            for (int i = index + 1; i < showList.Count; i++)
            {
                if (showList[i].TemplateName == groupViewName || showList[i].Type != type)
                {
                    break;
                }

                removeCount++;
            }

            if (removeCount > 0)
            {
                showList.RemoveRange(index + 1, removeCount);
            }
        }

        protected override bool TryGetBindData(int index, out TData data)
        {
            if (index < 0 || index >= showList.Count)
            {
                data = default;
                return false;
            }

            data = showList[index];
            return true;
        }

        public void Activate(int index)
        {
            if (index < 0 || index >= showList.Count)
            {
                return;
            }

            TData data = showList[index];
            if (IsGroupIndex(index))
            {
                SetExpanded(index, !data.Expanded);
                return;
            }

            SetChoiceIndex(index);
        }

        private int CountItemsForType(int type)
        {
            if (list == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Type == type)
                {
                    count++;
                }
            }

            return count;
        }

        private void RemoveEmptyGroups()
        {
            for (int i = showList.Count - 1; i >= 0; i--)
            {
                TData group = showList[i];
                if (group.TemplateName != groupViewName)
                {
                    continue;
                }

                if (CountItemsForType(group.Type) == 0)
                {
                    showList.RemoveAt(i);
                }
            }
        }
    }
}
