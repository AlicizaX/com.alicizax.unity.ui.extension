using UnityEngine;

namespace AlicizaX.UI
{
    /// <summary>
    /// 列表项实例的创建、回收与复位。池只负责"何时"调用这里，不重复实现实例化细节。
    /// </summary>
    internal sealed class ViewHolderFactory
    {
        private readonly ViewHolder[] templates;
        private readonly Transform parent;

        public ViewHolderFactory(ViewHolder[] templates, Transform parent)
        {
            this.templates = templates;
            this.parent = parent;
        }

        public ViewHolder Create(int templateId)
        {
            ViewHolder source = templates[templateId];
            if (source == null)
            {
                return null;
            }

            ViewHolder holder = Object.Instantiate(source, parent);
            ResetTransform(holder);
            return holder;
        }

        public void Destroy(ViewHolder holder)
        {
            // 编辑器预览与 EditMode 测试里 Object.Destroy 非法，会打错误日志且不销毁。
            if (Application.isPlaying)
            {
                Object.Destroy(holder.gameObject);
            }
            else
            {
                Object.DestroyImmediate(holder.gameObject);
            }
        }

        /// <summary>
        /// 入池前的复位。所有入池路径都经过这里（含预热），因此池内实例的后置状态只有一种。
        /// </summary>
        public void Reset(ViewHolder holder)
        {
            ResetTransform(holder);
            holder.SetPooledVisible(false);
        }

        private static void ResetTransform(ViewHolder holder)
        {
            RectTransform rectTransform = holder.RectTransform;
            rectTransform.anchoredPosition3D = Vector3.zero;
            rectTransform.localRotation = Quaternion.identity;
            rectTransform.localScale = Vector3.one;
        }
    }
}
