using System.Collections.Generic;

namespace AlicizaX.UI
{
    public class MixedAdapter<TData> : Adapter<TData> where TData : IMixedViewData
    {
        public MixedAdapter(RecyclerView recyclerView) : base(recyclerView)
        {
        }

        public MixedAdapter(RecyclerView recyclerView, List<TData> list) : base(recyclerView, list)
        {
        }

        public override string GetViewName(int index)
        {
            return index >= 0 && list != null && index < list.Count
                ? list[index].TemplateName
                : string.Empty;
        }
    }
}
