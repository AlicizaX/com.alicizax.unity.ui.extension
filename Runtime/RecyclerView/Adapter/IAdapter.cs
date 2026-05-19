namespace AlicizaX.UI
{
    public interface IAdapter
    {
        int GetItemCount();

        int GetRealCount();

        int GetTemplateId(int index);

        void OnBindViewHolder(ViewHolder viewHolder, int index);

        void OnRecycleViewHolder(ViewHolder viewHolder);

        void SetChoiceIndex(int index);

        void NotifyDataChanged();
    }
}
