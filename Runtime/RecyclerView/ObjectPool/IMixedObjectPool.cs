namespace AlicizaX.UI
{
    using System;

    public interface IMixedObjectPool<T> : IDisposable where T : class
    {
        T Allocate(int templateId);

        void Free(int templateId, T obj);
    }

}
