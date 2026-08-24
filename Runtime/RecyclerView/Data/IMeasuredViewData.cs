namespace AlicizaX.UI
{
    public interface IMeasuredViewData : ISimpleViewData
    {
        bool TryGetItemLength(out float length);
    }
}
