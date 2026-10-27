using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

namespace AlicizaX.UI.NavigationTests
{
    /// <summary>
    /// 测试用最小 RecyclerView 搭建器：content + 模板 + 布局管理器，并暴露内部绑定入口。
    /// </summary>
    internal sealed class RecyclerViewFixture : IDisposable
    {
        private static readonly MethodInfo SetAdapterMethod =
            typeof(RecyclerView).GetMethod("SetAdapter", BindingFlags.Instance | BindingFlags.NonPublic);

        private readonly GameObject root;
        private readonly RectTransform content;
        private readonly List<ViewHolder> templates = new();

        public RecyclerViewFixture(Vector2 viewportSize, LayoutManager layoutManager)
        {
            root = new GameObject("recycler-fixture", typeof(RectTransform));
            root.SetActive(false);
            root.GetComponent<RectTransform>().sizeDelta = viewportSize;
            content = new GameObject("content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.transform.SetParent(root.transform, false);
            View = root.AddComponent<RecyclerView>();
            SetField(View, "content", content);
            SetField(View, "direction", Direction.Vertical);
            SetField(View, "layoutManager", layoutManager);
            // 保持 inactive 直到 Bind：Awake 的模板校验必须在模板注册完成后执行。
        }

        public RecyclerView View { get; }

        public RectTransform Content => content;

        public GameObject Root => root;

        public T CreateTemplate<T>(Vector2 size) where T : ViewHolder
        {
            GameObject templateRoot = new GameObject("template", typeof(RectTransform));
            templateRoot.transform.SetParent(content, false);
            templateRoot.GetComponent<RectTransform>().sizeDelta = size;
            T template = templateRoot.AddComponent<T>();
            templateRoot.SetActive(false);
            templates.Add(template);
            return template;
        }

        public Scroller AddScroller()
        {
            Scroller scroller = root.AddComponent<Scroller>();
            SetField(View, "scroller", scroller);
            return scroller;
        }

        public T Bind<T>(T adapter) where T : IAdapter
        {
            SetField(View, "templates", templates.ToArray());
            root.SetActive(true);
            SetAdapterMethod.Invoke(View, new object[] { adapter });
            return adapter;
        }

        public void Dispose()
        {
            if (root != null)
            {
                Object.DestroyImmediate(root);
            }
        }

        public static void SetField(object target, string name, object value)
        {
            target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);
        }

        public static void CallPrivate(object target, string name, params object[] args)
        {
            target.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);
        }
    }
}
