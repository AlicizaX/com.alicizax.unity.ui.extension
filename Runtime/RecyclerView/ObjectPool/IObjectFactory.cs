namespace AlicizaX.UI
{
    public interface IObjectFactory<T> where T : class
    {
        T Create();

        void Destroy(T obj);

        void Reset(T obj);

        bool Validate(T obj);
    }
}
