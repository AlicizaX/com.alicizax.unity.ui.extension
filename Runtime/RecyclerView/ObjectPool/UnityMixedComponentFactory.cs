namespace AlicizaX.UI
{
    using UnityEngine;

    public class UnityMixedComponentFactory<T> : IMixedObjectFactory<T> where T : Component
    {
        protected T template;
        protected T[] templates;
        protected Transform parent;

        public UnityMixedComponentFactory(T template, Transform parent)
        {
            this.template = template;
            this.parent = parent;
        }

        public UnityMixedComponentFactory(T[] templates, Transform parent)
        {
            this.templates = templates;
            this.parent = parent;
        }

        public T Create(int templateId)
        {
            T source = GetTemplate(templateId);
            if (source == null)
            {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                Log.Error("Mixed object template was not found.");
#endif
                return null;
            }

            T obj = Object.Instantiate(source, parent);
            if (obj.transform is RectTransform rectTransform)
            {
                rectTransform.anchoredPosition3D = Vector3.zero;
                rectTransform.localRotation = Quaternion.identity;
                rectTransform.localScale = Vector3.one;
            }
            else
            {
                obj.transform.localPosition = Vector3.zero;
                obj.transform.localRotation = Quaternion.identity;
                obj.transform.localScale = Vector3.one;
            }

            return obj;
        }

        public void Destroy(int templateId, T obj)
        {
            Object.Destroy(obj.gameObject);
        }

        public void Reset(int templateId, T obj)
        {
            if (obj is ViewHolder viewHolder)
            {
                viewHolder.SetPooledVisible(false);
                return;
            }

            obj.gameObject.SetActive(false);
        }

        public bool Validate(int templateId, T obj)
        {
            return true;
        }

        private T GetTemplate(int templateId)
        {
            if (templates != null && templateId >= 0 && templateId < templates.Length)
            {
                return templates[templateId];
            }

            return template;
        }
    }

}
