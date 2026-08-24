namespace AlicizaX.UI
{
    public interface IAdapter
    {
        int GetItemCount();

        int GetRealCount();

        bool UsesMultipleTemplates { get; }

        int GetTemplateId(int index);

        bool TryGetDeclaredLength(int index, out float length);

        void OnBindViewHolder(ViewHolder viewHolder, int index);

        void OnRecycleViewHolder(ViewHolder viewHolder);

        void SetChoiceIndex(int index);

        void NotifyDataChanged();
    }
}
