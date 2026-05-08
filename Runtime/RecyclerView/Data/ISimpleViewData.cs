namespace AlicizaX.UI
{
    public interface ISimpleViewData
    {
    }

    public interface IMixedViewData : ISimpleViewData
    {
        string TemplateName { get; set; }
    }

    public interface IGroupViewData : IMixedViewData
    {
        bool Expanded { get; set; }
        int Type { get; set; }
    }
}
