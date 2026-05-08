// TabbedInspector.cs
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UnityEditor.Utils
{
    /// <summary>
    /// 复用的 tab 管理器（非 Editor），供任意 Editor 组合使用。
    /// 用法：在 Editor 的 OnEnable 创建一个实例并 RegisterTab / AppendToTab，
    /// 在 OnInspectorGUI 调用 DrawTabs()。
    /// </summary>
    public class TabbedInspector
    {
        public class Tab
        {
            public string title;
            public string iconName;
            public List<Action> callbacks = new List<Action>();

            public Tab(string t, string icon)
            {
                title = t;
                iconName = icon;
            }
        }

        List<Tab> _tabs = new List<Tab>();
        int _currentTabIndex = 0;
        string _prefsKey; // 用于保存每类 inspector 的选择（可选）

        public TabbedInspector(string prefsKey = null)
        {
            _prefsKey = prefsKey;
            if (!string.IsNullOrEmpty(_prefsKey))
            {
                _currentTabIndex = EditorPrefs.GetInt(_prefsKey, 0);
            }
        }

        public void RegisterTab(string title, string iconName, Action drawCallback)
        {
            if (string.IsNullOrEmpty(title)) return;
            var tab = _tabs.Find(t => t.title == title);
            if (tab == null)
            {
                tab = new Tab(title, iconName);
                _tabs.Add(tab);
            }
            else
            {
                tab.iconName = iconName;
                tab.callbacks.Clear();
            }

            if (drawCallback != null)
                tab.callbacks.Add(drawCallback);
        }

        public void AppendToTab(string title, Action drawCallback, bool last = true)
        {
            if (string.IsNullOrEmpty(title) || drawCallback == null) return;
            var tab = _tabs.Find(t => t.title == title);
            if (tab == null)
            {
                tab = new Tab(title, "d_DefaultAsset Icon");
                _tabs.Add(tab);
            }

            if (!tab.callbacks.Contains(drawCallback))
            {
                if (last) tab.callbacks.Add(drawCallback);
                else tab.callbacks.Insert(0, drawCallback);
            }
        }

        public void RemoveCallbackFromTab(string title, Action drawCallback)
        {
            if (string.IsNullOrEmpty(title) || drawCallback == null) return;
            var tab = _tabs.Find(t => t.title == title);
            if (tab == null) return;
            tab.callbacks.RemoveAll(cb => cb == drawCallback);
        }

        public void UnregisterTab(string title)
        {
            if (string.IsNullOrEmpty(title)) return;
            _tabs.RemoveAll(t => t.title == title);
            if (_tabs.Count == 0) _currentTabIndex = 0;
            else if (_currentTabIndex >= _tabs.Count) _currentTabIndex = Mathf.Max(0, _tabs.Count - 1);
        }

        public void EnsureDefaultTab(string title, string iconName, Action defaultCallback)
        {
            var tab = _tabs.Find(t => t.title == title);
            if (tab == null)
            {
                tab = new Tab(title, iconName);
                _tabs.Insert(0, tab);
            }

            if (!tab.callbacks.Contains(defaultCallback))
                tab.callbacks.Insert(0, defaultCallback);
        }

        public void DrawTabs()
        {
            if (_tabs == null || _tabs.Count == 0)
            {
                // nothing to draw
                return;
            }

            EditorGUILayout.BeginHorizontal();

            for (int i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                bool isActive = (i == _currentTabIndex);
                var style = new GUIStyle(EditorStyles.toolbarButton) { fixedHeight = 25, fontSize = 11, fontStyle = isActive ? FontStyle.Bold : FontStyle.Normal };

                var icon = EditorGUIUtility.IconContent(tab.iconName)?.image;
                var content = new GUIContent(icon, tab.title);
                if (GUILayout.Button(content, style))
                {
                    _currentTabIndex = i;
                    SaveIndex();
                }

                if (isActive)
                {
                    // 一条下划线表示选中
                    Rect rect = GUILayoutUtility.GetLastRect();
                    EditorGUI.DrawRect(new Rect(rect.x, rect.yMax - 2, rect.width, 2), new Color(0.1f, 0.5f, 0.9f));
                }
            }

            EditorGUILayout.EndHorizontal();
            GUILayout.Space(4);

            // draw callbacks for current tab
            var callbacks = _tabs[_currentTabIndex].callbacks;
            if (callbacks != null)
            {
                foreach (var cb in callbacks)
                {
                    try
                    {
                        cb?.Invoke();
                    }
                    catch (ExitGUIException)
                    {
                        throw;
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
        }

        void SaveIndex()
        {
            if (!string.IsNullOrEmpty(_prefsKey))
                EditorPrefs.SetInt(_prefsKey, _currentTabIndex);
        }
    }
}
