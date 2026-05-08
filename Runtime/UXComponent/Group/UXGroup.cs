using UnityEngine;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    [AddComponentMenu("UI/UXGroup", 31)]
    [DisallowMultipleComponent]
    public class UXGroup : UIBehaviour
    {
        [SerializeField] private bool m_AllowSwitchOff = false;

        [SerializeField]
        private UXToggle[] m_Toggles = new UXToggle[0];

        [SerializeField]
        private UXToggle m_DefaultToggle;

        private int m_ToggleCount;
        private UXToggle m_CurrentToggle;
        private int m_CurrentIndex = -1;

        public bool allowSwitchOff
        {
            get { return m_AllowSwitchOff; }
            set
            {
                m_AllowSwitchOff = value;
                EnsureValidState();
            }
        }

        public UXToggle defaultToggle
        {
            get { return m_DefaultToggle; }
            set
            {
                m_DefaultToggle = ContainsToggle(value) ? value : null;
                EnsureValidState();
            }
        }

        protected UXGroup()
        {
        }

        protected override void Start()
        {
            EnsureValidState();
            base.Start();
        }

        protected override void OnEnable()
        {
            EnsureValidState();
            base.OnEnable();
        }

        public void NotifyToggleOn(UXToggle toggle, bool sendCallback = true)
        {
            EnsureStorage();
            int index = IndexOfToggle(toggle);
            if (index < 0)
                return;

            m_CurrentToggle = toggle;
            m_CurrentIndex = index;

            for (int i = 0; i < m_ToggleCount; i++)
            {
                UXToggle item = m_Toggles[i];
                if (item == null || item == toggle)
                    continue;

                if (sendCallback)
                    item.isOn = false;
                else
                    item.SetIsOnWithoutNotify(false);
            }
        }

        public void UnregisterToggle(UXToggle toggle)
        {
            EnsureStorage();
            int index = IndexOfToggle(toggle);
            if (index < 0)
                return;

            RemoveAt(index);

            if (m_DefaultToggle == toggle)
                m_DefaultToggle = null;

            if (m_CurrentToggle == toggle)
            {
                m_CurrentToggle = null;
                m_CurrentIndex = -1;
            }

            EnsureSingleSelection();
        }

        public void RegisterToggle(UXToggle toggle)
        {
            EnsureStorage();
            if (toggle == null || ContainsToggle(toggle))
                return;

            EnsureCapacity(m_ToggleCount + 1);
            m_Toggles[m_ToggleCount] = toggle;
            m_ToggleCount++;

            if (toggle.isOn)
                NotifyToggleOn(toggle);
            else
                EnsureSingleSelection();
        }

        public bool ContainsToggle(UXToggle toggle)
        {
            EnsureStorage();
            return IndexOfToggle(toggle) >= 0;
        }

        public void EnsureValidState()
        {
            EnsureStorage();
            CompactNulls();
            SyncToggleGroups();
            EnsureDefaultToggle();
            EnsureSingleSelection();
        }

        public bool AnyTogglesOn()
        {
            return FindFirstActiveIndex() >= 0;
        }

        public UXToggle GetFirstActiveToggle()
        {
            int index = FindFirstActiveIndex();
            return index >= 0 ? m_Toggles[index] : null;
        }

        public void SetAllTogglesOff(bool sendCallback = true)
        {
            bool oldAllowSwitchOff = m_AllowSwitchOff;
            m_AllowSwitchOff = true;

            for (int i = 0; i < m_ToggleCount; i++)
            {
                UXToggle toggle = m_Toggles[i];
                if (toggle == null)
                    continue;

                if (sendCallback)
                    toggle.isOn = false;
                else
                    toggle.SetIsOnWithoutNotify(false);
            }

            m_CurrentToggle = null;
            m_CurrentIndex = -1;
            m_AllowSwitchOff = oldAllowSwitchOff;
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
            return index >= 0 && index < m_ToggleCount ? m_Toggles[index] : null;
        }

        internal int ToggleCount
        {
            get { return m_ToggleCount; }
        }

        private void EnsureStorage()
        {
            if (m_Toggles == null)
                m_Toggles = new UXToggle[0];

            if (m_ToggleCount == 0)
            {
                int count = 0;
                for (int i = 0; i < m_Toggles.Length; i++)
                {
                    if (m_Toggles[i] != null)
                        count = i + 1;
                }

                m_ToggleCount = count;
            }

            if (m_ToggleCount > m_Toggles.Length)
                m_ToggleCount = m_Toggles.Length;
        }

        private void CompactNulls()
        {
            int write = 0;
            for (int read = 0; read < m_Toggles.Length; read++)
            {
                UXToggle toggle = m_Toggles[read];
                if (toggle == null)
                    continue;

                m_Toggles[write] = toggle;
                write++;
            }

            for (int i = write; i < m_Toggles.Length; i++)
                m_Toggles[i] = null;

            m_ToggleCount = write;
        }

        private void SyncToggleGroups()
        {
            for (int i = 0; i < m_ToggleCount; i++)
            {
                UXToggle toggle = m_Toggles[i];
                if (toggle != null && toggle.group != this)
                    toggle.SetToggleGroupInternal(this, true);
            }
        }

        private void EnsureDefaultToggle()
        {
            if (m_ToggleCount == 0)
            {
                m_DefaultToggle = null;
                return;
            }

            if (m_DefaultToggle != null && IndexOfToggle(m_DefaultToggle) >= 0)
                return;

            if (!m_AllowSwitchOff)
                m_DefaultToggle = m_Toggles[0];
            else
                m_DefaultToggle = null;
        }

        private void EnsureSingleSelection()
        {
            int selectedIndex = FindSelectedIndex();
            if (selectedIndex < 0 && !m_AllowSwitchOff && m_ToggleCount > 0)
            {
                selectedIndex = GetDefaultIndex();
                if (selectedIndex < 0)
                    selectedIndex = 0;

                UXToggle toggle = m_Toggles[selectedIndex];
                if (toggle != null)
                    toggle.isOn = true;
            }

            if (selectedIndex >= 0)
            {
                m_CurrentToggle = m_Toggles[selectedIndex];
                m_CurrentIndex = selectedIndex;

                for (int i = 0; i < m_ToggleCount; i++)
                {
                    UXToggle toggle = m_Toggles[i];
                    if (toggle != null && i != selectedIndex && toggle.isOn)
                        toggle.SetIsOnWithoutNotify(false);
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
            if (defaultIndex >= 0 && m_Toggles[defaultIndex].isOn)
                return defaultIndex;

            return FindFirstActiveIndex();
        }

        private int FindFirstActiveIndex()
        {
            for (int i = 0; i < m_ToggleCount; i++)
            {
                UXToggle toggle = m_Toggles[i];
                if (toggle != null && toggle.isOn)
                    return i;
            }

            return -1;
        }

        private int GetDefaultIndex()
        {
            if (m_DefaultToggle == null)
                return -1;

            return IndexOfToggle(m_DefaultToggle);
        }

        private int IndexOfToggle(UXToggle toggle)
        {
            if (toggle == null || m_Toggles == null)
                return -1;

            for (int i = 0; i < m_ToggleCount; i++)
            {
                if (m_Toggles[i] == toggle)
                    return i;
            }

            return -1;
        }

        private void EnsureCapacity(int capacity)
        {
            if (m_Toggles == null)
                m_Toggles = new UXToggle[capacity < 4 ? 4 : capacity];

            if (m_Toggles.Length >= capacity)
                return;

            int newCapacity = m_Toggles.Length == 0 ? 4 : m_Toggles.Length * 2;
            while (newCapacity < capacity)
                newCapacity *= 2;

            UXToggle[] newToggles = new UXToggle[newCapacity];
            for (int i = 0; i < m_ToggleCount; i++)
                newToggles[i] = m_Toggles[i];

            m_Toggles = newToggles;
        }

        private void RemoveAt(int index)
        {
            int lastIndex = m_ToggleCount - 1;
            for (int i = index; i < lastIndex; i++)
                m_Toggles[i] = m_Toggles[i + 1];

            if (lastIndex >= 0)
                m_Toggles[lastIndex] = null;

            m_ToggleCount = lastIndex;
        }

        private void SelectAdjacent(bool forward)
        {
            if (m_ToggleCount == 0)
                return;

            int index = ResolveCurrentIndex();
            if (index < 0)
                index = forward ? -1 : 0;

            for (int step = 0; step < m_ToggleCount; step++)
            {
                index = forward ? index + 1 : index - 1;
                if (index >= m_ToggleCount)
                    index = 0;
                else if (index < 0)
                    index = m_ToggleCount - 1;

                UXToggle toggle = m_Toggles[index];
                if (toggle == null || !toggle.IsActive() || !toggle.IsInteractable())
                    continue;

                toggle.isOn = true;
                return;
            }
        }

        private int ResolveCurrentIndex()
        {
            if (m_CurrentIndex >= 0 && m_CurrentIndex < m_ToggleCount && m_Toggles[m_CurrentIndex] == m_CurrentToggle)
                return m_CurrentIndex;

            m_CurrentIndex = FindFirstActiveIndex();
            m_CurrentToggle = m_CurrentIndex >= 0 ? m_Toggles[m_CurrentIndex] : null;
            return m_CurrentIndex;
        }
    }
}
