using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    [AddComponentMenu("UI/UXGroup", 31)]
    [DisallowMultipleComponent]
    public class UXGroup : UIBehaviour
    {
        [SerializeField] private bool m_AllowSwitchOff = false;

        [SerializeField] private UXToggle[] m_Toggles = Array.Empty<UXToggle>();

        [SerializeField] private UXToggle m_DefaultToggle;

        /// <summary>
        /// 运行时注册表。序列化数组只作为初始来源读取一次，之后所有增删都走 List，
        /// 不再维护 "数组 + 计数 + 压缩" 这套手写容器。
        /// </summary>
        private readonly List<UXToggle> toggles = new();

        private bool storageInitialized;
        private bool validationDirty = true;

        /// <summary>
        /// <see cref="SetAllTogglesOff"/> 期间的临时放行，不修改序列化字段。
        /// </summary>
        private bool suppressSwitchOffGuard;

        private UXToggle requestedDefaultToggle;
        private UXToggle m_CurrentToggle;
        private int m_CurrentIndex = -1;

        public bool allowSwitchOff
        {
            get { return m_AllowSwitchOff; }
            set
            {
                m_AllowSwitchOff = value;
                MarkDirty();
                EnsureValidState();
            }
        }

        public UXToggle defaultToggle
        {
            get { return m_DefaultToggle; }
            set
            {
                // 只记录请求；注册顺序可能晚于赋值，真正的解析放到 EnsureDefaultToggle。
                requestedDefaultToggle = value;
                MarkDirty();
                EnsureValidState();
            }
        }

        internal bool IsSwitchOffAllowed => m_AllowSwitchOff || suppressSwitchOffGuard;

        protected UXGroup()
        {
        }

        protected override void Start()
        {
            // 所有子 Toggle 的 OnEnable 都已完成，这里做整组唯一一次校验。
            EnsureValidState();
            base.Start();
        }

        protected override void OnEnable()
        {
            MarkDirty();
            base.OnEnable();
        }

        internal void MarkDirty()
        {
            validationDirty = true;
        }

        public void NotifyToggleOn(UXToggle toggle, bool sendCallback = true)
        {
            EnsureStorage();
            int index = toggles.IndexOf(toggle);
            if (index < 0)
            {
                return;
            }

            m_CurrentToggle = toggle;
            m_CurrentIndex = index;

            for (int i = 0; i < toggles.Count; i++)
            {
                UXToggle item = toggles[i];
                if (item == null || item == toggle)
                {
                    continue;
                }

                if (sendCallback)
                {
                    item.isOn = false;
                }
                else
                {
                    item.SetIsOnWithoutNotify(false);
                }
            }
        }

        public void UnregisterToggle(UXToggle toggle)
        {
            EnsureStorage();
            if (!toggles.Remove(toggle))
            {
                return;
            }

            if (m_DefaultToggle == toggle)
            {
                m_DefaultToggle = null;
            }

            if (requestedDefaultToggle == toggle)
            {
                requestedDefaultToggle = null;
            }

            if (m_CurrentToggle == toggle)
            {
                m_CurrentToggle = null;
                m_CurrentIndex = -1;
            }

            // 拆卸阶段只改状态，不再顺手选中兄弟节点：销毁顺序不确定，这里触发选中会产生
            // 回调、音效和补间，且目标可能马上被销毁。
            MarkDirty();
        }

        public void RegisterToggle(UXToggle toggle)
        {
            EnsureStorage();
            if (toggle == null || toggles.Contains(toggle))
            {
                return;
            }

            toggles.Add(toggle);
            MarkDirty();

            if (toggle.isOn)
            {
                NotifyToggleOn(toggle);
            }
        }

        public bool ContainsToggle(UXToggle toggle)
        {
            EnsureStorage();
            return toggle != null && toggles.Contains(toggle);
        }

        /// <summary>
        /// 校验整组状态。未变更时直接返回，因此每个 Toggle 的 OnEnable 调用它都不会退化成 O(n²)。
        /// </summary>
        public void EnsureValidState()
        {
            EnsureStorage();
            if (!validationDirty)
            {
                return;
            }

            validationDirty = false;
            SyncToggleGroups();
            EnsureDefaultToggle();
            EnsureSingleSelection();
        }

        internal void ApplyDefaultSelection(bool sendCallback = false)
        {
            EnsureStorage();
            validationDirty = true;
            EnsureValidState();

            if (m_DefaultToggle != null)
            {
                if (sendCallback)
                {
                    m_DefaultToggle.isOn = true;
                }
                else
                {
                    m_DefaultToggle.SetIsOnWithoutNotify(true);
                }

                NotifyToggleOn(m_DefaultToggle, sendCallback);
            }
            else if (IsSwitchOffAllowed)
            {
                SetAllTogglesOff(sendCallback);
            }

            RefreshToggleVisuals();
        }

        internal void RefreshToggleVisuals()
        {
            for (int i = 0; i < toggles.Count; i++)
            {
                UXToggle toggle = toggles[i];
                if (toggle != null)
                {
                    toggle.RefreshVisual();
                }
            }
        }

        public bool AnyTogglesOn()
        {
            EnsureValidState();
            return FindFirstActiveIndex() >= 0;
        }

        /// <summary>
        /// 除 <paramref name="except"/> 之外是否还有选中项。UXToggle 用它判断"关掉自己是不是关掉最后一项"。
        /// </summary>
        internal bool HasOtherActiveToggle(UXToggle except)
        {
            EnsureStorage();
            for (int i = 0; i < toggles.Count; i++)
            {
                UXToggle toggle = toggles[i];
                if (toggle != null && toggle != except && toggle.isOn)
                {
                    return true;
                }
            }

            return false;
        }

        public UXToggle GetFirstActiveToggle()
        {
            EnsureValidState();
            int index = FindFirstActiveIndex();
            return index >= 0 ? toggles[index] : null;
        }

        public void SetAllTogglesOff(bool sendCallback = true)
        {
            try
            {
                suppressSwitchOffGuard = true;
                for (int i = 0; i < toggles.Count; i++)
                {
                    UXToggle toggle = toggles[i];
                    if (toggle == null)
                    {
                        continue;
                    }

                    if (sendCallback)
                    {
                        toggle.isOn = false;
                    }
                    else
                    {
                        toggle.SetIsOnWithoutNotify(false);
                    }
                }
            }
            finally
            {
                suppressSwitchOffGuard = false;
            }

            m_CurrentToggle = null;
            m_CurrentIndex = -1;
            MarkDirty();
        }

        public void Next()
        {
            SelectAdjacent(true);
        }

        public void Previous()
        {
            SelectAdjacent(false);
        }

        internal UXToggle GetToggleAt(int index)
        {
            EnsureStorage();
            return index >= 0 && index < toggles.Count ? toggles[index] : null;
        }

        internal int ToggleCount
        {
            get
            {
                EnsureStorage();
                return toggles.Count;
            }
        }

        private void EnsureStorage()
        {
            if (storageInitialized)
            {
                return;
            }

            storageInitialized = true;
            if (m_Toggles == null)
            {
                return;
            }

            for (int i = 0; i < m_Toggles.Length; i++)
            {
                UXToggle toggle = m_Toggles[i];
                if (toggle != null && !toggles.Contains(toggle))
                {
                    toggles.Add(toggle);
                }
            }
        }

        private void SyncToggleGroups()
        {
            for (int i = 0; i < toggles.Count; i++)
            {
                UXToggle toggle = toggles[i];
                if (toggle != null && toggle.group != this)
                {
                    toggle.SetToggleGroupInternal(this, true);
                }
            }
        }

        private void EnsureDefaultToggle()
        {
            if (toggles.Count == 0)
            {
                m_DefaultToggle = null;
                return;
            }

            if (requestedDefaultToggle != null && toggles.Contains(requestedDefaultToggle))
            {
                m_DefaultToggle = requestedDefaultToggle;
                return;
            }

            if (m_DefaultToggle != null && toggles.Contains(m_DefaultToggle))
            {
                return;
            }

            m_DefaultToggle = IsSwitchOffAllowed ? null : toggles[0];
        }

        private void EnsureSingleSelection()
        {
            int selectedIndex = FindSelectedIndex();
            if (selectedIndex < 0 && !IsSwitchOffAllowed && toggles.Count > 0)
            {
                int defaultIndex = GetDefaultIndex();
                selectedIndex = defaultIndex >= 0 ? defaultIndex : 0;

                UXToggle toggle = toggles[selectedIndex];
                if (toggle != null)
                {
                    toggle.isOn = true;
                }
            }

            if (selectedIndex >= 0)
            {
                m_CurrentToggle = toggles[selectedIndex];
                m_CurrentIndex = selectedIndex;

                for (int i = 0; i < toggles.Count; i++)
                {
                    UXToggle toggle = toggles[i];
                    if (toggle != null && i != selectedIndex && toggle.isOn)
                    {
                        toggle.SetIsOnWithoutNotify(false);
                    }
                }
            }
            else
            {
                m_CurrentToggle = null;
                m_CurrentIndex = -1;
            }
        }

        private int FindSelectedIndex()
        {
            int defaultIndex = GetDefaultIndex();
            if (defaultIndex >= 0 && toggles[defaultIndex].isOn)
            {
                return defaultIndex;
            }

            return FindFirstActiveIndex();
        }

        private int FindFirstActiveIndex()
        {
            for (int i = 0; i < toggles.Count; i++)
            {
                UXToggle toggle = toggles[i];
                if (toggle != null && toggle.isOn)
                {
                    return i;
                }
            }

            return -1;
        }

        private int GetDefaultIndex()
        {
            return m_DefaultToggle == null ? -1 : toggles.IndexOf(m_DefaultToggle);
        }

        private void SelectAdjacent(bool forward)
        {
            if (toggles.Count == 0)
            {
                return;
            }

            int index = ResolveCurrentIndex();
            if (index < 0)
            {
                index = forward ? -1 : 0;
            }

            for (int step = 0; step < toggles.Count; step++)
            {
                index = forward ? index + 1 : index - 1;
                if (index >= toggles.Count)
                {
                    index = 0;
                }
                else if (index < 0)
                {
                    index = toggles.Count - 1;
                }

                UXToggle toggle = toggles[index];
                if (toggle == null || !toggle.IsActive() || !toggle.IsInteractable())
                {
                    continue;
                }

                toggle.isOn = true;
                return;
            }
        }

        private int ResolveCurrentIndex()
        {
            if (m_CurrentIndex >= 0 && m_CurrentIndex < toggles.Count && toggles[m_CurrentIndex] == m_CurrentToggle)
            {
                return m_CurrentIndex;
            }

            m_CurrentIndex = FindFirstActiveIndex();
            m_CurrentToggle = m_CurrentIndex >= 0 ? toggles[m_CurrentIndex] : null;
            return m_CurrentIndex;
        }
    }
}
