namespace AlicizaX.UI
{
    using UnityEngine;

    public class UnityComponentFactory<T> : IObjectFactory<T> where T : Component
    {
        private T template;
        private Transform parent;

        public UnityComponentFactory(T template, Transform parent)
        {
            this.template = template;
            this.parent = parent;
        }

        public T Create()
        {
            T obj = Object.Instantiate(template, parent);
            if (obj is ViewHolder viewHolder)
            {
                viewHolder.RefreshInteractionCache();
            }

            return obj;
        }

        public void Destroy(T obj)
        {
            Object.Destroy(obj.gameObject);
        }

        public void Reset(T obj)
        {
            if (obj is ViewHolder viewHolder)
            {
                viewHolder.SetPooledVisible(false);
                return;
            }

            obj.gameObject.SetActive(false);
        }

        public bool Validate(T obj)
        {
            return true;
        }
    }

}
