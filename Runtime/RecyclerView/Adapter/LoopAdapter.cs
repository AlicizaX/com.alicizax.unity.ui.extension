using System.Collections.Generic;

namespace AlicizaX.UI
{
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
            return GetRealCount() > 0 ? int.MaxValue : 0;
        }

        public override int GetRealCount()
        {
            return list == null ? 0 : list.Count;
        }

        public override void OnBindViewHolder(ViewHolder viewHolder, int index)
        {
            if (list == null || list.Count == 0)
            {
                return;
            }

            index %= list.Count;
            base.OnBindViewHolder(viewHolder, index);
        }
    }
}
