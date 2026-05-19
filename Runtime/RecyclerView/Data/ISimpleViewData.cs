namespace AlicizaX.UI
{
    public interface ISimpleViewData
    {
    }

    public interface IMixedViewData : ISimpleViewData
    {
        /// <summary>
        /// Inspector界面模板顺序下标
        /// </summary>
        int TemplateId { get; set; }
    }

    public interface IGroupViewData : IMixedViewData
    {
        bool Expanded { get; set; }
        int Type { get; set; }
    }
}
