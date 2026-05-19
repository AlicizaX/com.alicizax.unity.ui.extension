namespace AlicizaX.UI
{
    public interface IMixedObjectFactory<T> where T : class
    {
        T Create(int templateId);

        void Destroy(int templateId, T obj);

        void Reset(int templateId, T obj);

        bool Validate(int templateId, T obj);
    }
}
