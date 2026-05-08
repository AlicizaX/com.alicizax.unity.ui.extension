namespace AlicizaX.UI
{
    public interface IAdapter
    {
        int GetItemCount();

        int GetRealCount();

        string GetViewName(int index);

        void OnBindViewHolder(ViewHolder viewHolder, int index);

        void OnRecycleViewHolder(ViewHolder viewHolder);

        void NotifyDataChanged();
    }
}
