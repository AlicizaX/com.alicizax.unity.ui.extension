namespace AlicizaX.UI
{
    public interface IAdapter
    {
        int GetItemCount();

        int GetRealCount();

        /// <summary>
        /// 数据版本号。每次数据或绑定内容变化都自增，用于让按数据派生的缓存（如变长偏移表）失效。
        /// </summary>
        int DataRevision { get; }

        int GetTemplateId(int index);

        bool TryGetDeclaredLength(int index, out float length);

        void OnBindViewHolder(ViewHolder viewHolder, int index);

        void SetChoiceIndex(int index);

        void NotifyDataChanged();
    }
}
