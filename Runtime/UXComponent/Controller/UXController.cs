using System;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace UnityEngine.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("UX/UX Controller")]
    public sealed class UXController : MonoBehaviour
    {
        [Serializable]
        public sealed class ControllerDefinition
        {
            [SerializeField] private string _id = string.Empty;
            [SerializeField] private string _name = "Controller";
            [SerializeField] private int _length = 2;
            [SerializeField] private int _defaultIndex;
            [SerializeField] private string _description = string.Empty;
            [NonSerialized] private int _selectedIndex = -1;
            [NonSerialized] private UXController _owner;

            public string Id => _id;

            public string Name
            {
                get => _name;
                set => _name = value;
            }

            public int Length
            {
                get => Mathf.Max(1, _length);
                set => _length = Mathf.Max(1, value);
            }

            public string Description
            {
                get => _description;
                set => _description = value;
            }

            public int DefaultIndex
            {
                get => Mathf.Clamp(_defaultIndex, 0, Length - 1);
                set => _defaultIndex = Mathf.Clamp(value, 0, Length - 1);
            }

            public int SelectedIndex
            {
                get => _selectedIndex;
                set
                {
                    if (_owner == null)
                    {
                        SetSelectedIndexSilently(Mathf.Clamp(value, 0, Length - 1));
                        return;
                    }

                    _owner.SetControllerIndexInternal(this, value);
                }
            }

            internal void EnsureId()
            {
                if (string.IsNullOrWhiteSpace(_id))
                {
                    _id = Guid.NewGuid().ToString("N");
                }
            }

            internal void SetOwner(UXController owner)
            {
                _owner = owner;
            }

            internal void SetSelectedIndexSilently(int selectedIndex)
            {
                _selectedIndex = selectedIndex;
            }
        }

        [SerializeField] private List<ControllerDefinition> _controllers = new List<ControllerDefinition>();
        [SerializeField] private List<UXBinding> _bindings = new List<UXBinding>();

        private readonly Dictionary<string, int> _controllerIdMap = new Dictionary<string, int>();
        private readonly Dictionary<string, int> _controllerNameMap = new Dictionary<string, int>();
        private RuntimeBindingEntry[][] _runtimeEntriesByController = Array.Empty<RuntimeBindingEntry[]>();
        private int[] _runtimeEntryCounts = Array.Empty<int>();
        private bool _runtimeReady;

        private struct RuntimeBindingEntry
        {
            public UXBinding Binding;
            public int EntryIndex;
        }

        public IReadOnlyList<ControllerDefinition> Controllers
        {
            get
            {
                EnsureInitialized();
                return _controllers;
            }
        }

        public IReadOnlyList<UXBinding> Bindings => _bindings;
        public int ControllerCount => _controllers.Count;

        public bool TryGetControllerById(string controllerId, out ControllerDefinition controller)
        {
            controller = null;
            if (string.IsNullOrWhiteSpace(controllerId))
            {
                return false;
            }

            EnsureInitialized();
            if (_controllerIdMap.TryGetValue(controllerId, out int index))
            {
                controller = _controllers[index];
                return true;
            }

            return false;
        }

        internal bool TryGetControllerSlot(string controllerId, out int slot)
        {
            slot = -1;
            if (string.IsNullOrEmpty(controllerId))
            {
                return false;
            }

            EnsureInitialized();
            return _controllerIdMap.TryGetValue(controllerId, out slot);
        }

        public bool TryGetControllerByName(string controllerName, out ControllerDefinition controller)
        {
            controller = null;
            if (string.IsNullOrWhiteSpace(controllerName))
            {
                return false;
            }

            EnsureInitialized();
            if (_controllerNameMap.TryGetValue(controllerName, out int index))
            {
                controller = _controllers[index];
                return true;
            }

            return false;
        }

        public ControllerDefinition GetControllerByName(string controllerName)
        {
            if (string.IsNullOrWhiteSpace(controllerName))
            {
                return null;
            }

            EnsureInitialized();
            if (_controllerNameMap.TryGetValue(controllerName, out int index))
            {
                return _controllers[index];
            }

            return null;
        }

        public ControllerDefinition GetControllerAt(int index)
        {
            EnsureInitialized();

            if (index < 0 || index >= _controllers.Count)
            {
                return null;
            }

            return _controllers[index];
        }

        public int GetControllerIndex(string controllerId)
        {
            return TryGetControllerById(controllerId, out ControllerDefinition controller)
                ? controller.SelectedIndex
                : 0;
        }

        public bool SetControllerIndex(string controllerId, int selectedIndex)
        {
            if (!TryGetControllerById(controllerId, out ControllerDefinition controller))
            {
                return false;
            }

            return SetControllerIndexInternal(controller, selectedIndex);
        }

        public bool SetControllerIndexByName(string controllerName, int selectedIndex)
        {
            if (!TryGetControllerByName(controllerName, out ControllerDefinition controller))
            {
                return false;
            }

            return SetControllerIndexInternal(controller, selectedIndex);
        }

        public void ResetAllControllers()
        {
            EnsureInitialized();

            for (int i = 0; i < _controllers.Count; i++)
            {
                ControllerDefinition controller = _controllers[i];
                if (controller != null)
                {
                    SetControllerIndexInternal(controller, controller.DefaultIndex, true);
                }
            }
        }

        internal bool HasBinding(UXBinding binding)
        {
            return binding != null && _bindings.Contains(binding);
        }

        internal void RegisterBinding(UXBinding binding)
        {
            if (binding == null)
            {
                return;
            }

            if (!_bindings.Contains(binding))
            {
                _bindings.Add(binding);
                _runtimeReady = false;
                if (Application.isPlaying)
                {
                    RebuildRuntimeEntries();
                    ApplyCurrentStateToBinding(binding);
                }
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    EditorUtility.SetDirty(this);
                }
#endif
            }
        }

        internal void UnregisterBinding(UXBinding binding)
        {
            if (binding == null)
            {
                return;
            }

            _bindings.Remove(binding);
            _runtimeReady = false;
#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                EditorUtility.SetDirty(this);
            }
#endif
        }

        private void Reset()
        {
            if (_controllers.Count == 0)
            {
                _controllers.Add(new ControllerDefinition());
            }

            RebuildMaps();
        }

        private void Awake()
        {
            EnsureInitialized();

            for (int i = 0; i < _bindings.Count; i++)
            {
                if (_bindings[i] != null)
                {
                    _bindings[i].RebuildRuntime(this);
                }
            }

            RebuildRuntimeEntries();
            ResetAllControllers();
        }

        private void OnValidate()
        {
            RebuildMaps();
            CleanupBindings();
        }

        private void EnsureInitialized()
        {
            if (_controllerIdMap.Count == 0 && _controllerNameMap.Count == 0)
            {
                RebuildMaps();
            }
        }

        private void RebuildMaps()
        {
            _controllerIdMap.Clear();
            _controllerNameMap.Clear();

#if UNITY_EDITOR
            var usedNames = new HashSet<string>(StringComparer.Ordinal);
#endif

            for (int i = 0; i < _controllers.Count; i++)
            {
                ControllerDefinition controller = _controllers[i];
                if (controller == null)
                {
                    continue;
                }

                controller.EnsureId();
                controller.SetOwner(this);
                controller.SetSelectedIndexSilently(Mathf.Clamp(controller.SelectedIndex, -1, controller.Length - 1));
                controller.DefaultIndex = controller.DefaultIndex;

#if UNITY_EDITOR
                if (string.IsNullOrWhiteSpace(controller.Name))
                {
                    controller.Name = $"Controller{i + 1}";
                }

                if (!usedNames.Add(controller.Name))
                {
                    controller.Name = $"{controller.Name}_{i + 1}";
                    usedNames.Add(controller.Name);
                }
#endif

                _controllerIdMap[controller.Id] = i;
                _controllerNameMap[controller.Name] = i;
            }

            _runtimeReady = false;
        }

        private void CleanupBindings()
        {
            for (int i = _bindings.Count - 1; i >= 0; i--)
            {
                if (_bindings[i] == null)
                {
                    _bindings.RemoveAt(i);
                }
            }
        }

        private bool SetControllerIndexInternal(ControllerDefinition controller, int selectedIndex, bool force = false)
        {
            if (controller == null)
            {
                return false;
            }

            selectedIndex = Mathf.Clamp(selectedIndex, 0, controller.Length - 1);
            if (!force && controller.SelectedIndex == selectedIndex)
            {
                return false;
            }

            controller.SetSelectedIndexSilently(selectedIndex);
            if (TryGetControllerSlot(controller.Id, out int slot))
            {
                NotifyBindings(slot, selectedIndex);
            }
            return true;
        }

        private void NotifyBindings(int controllerSlot, int selectedIndex)
        {
            if (!_runtimeReady)
            {
                RebuildRuntimeEntries();
            }

            if ((uint)controllerSlot >= (uint)_runtimeEntriesByController.Length)
            {
                return;
            }

            RuntimeBindingEntry[] entries = _runtimeEntriesByController[controllerSlot];
            int count = _runtimeEntryCounts[controllerSlot];
            for (int i = 0; i < count; i++)
            {
                UXBinding binding = entries[i].Binding;
                if (binding != null)
                {
                    binding.ApplyRuntimeEntry(entries[i].EntryIndex, selectedIndex);
                }
            }
        }

        private void RebuildRuntimeEntries()
        {
            int controllerCount = _controllers.Count;
            if (_runtimeEntriesByController.Length != controllerCount)
            {
                _runtimeEntriesByController = new RuntimeBindingEntry[controllerCount][];
                _runtimeEntryCounts = new int[controllerCount];
            }

            for (int i = 0; i < controllerCount; i++)
            {
                _runtimeEntryCounts[i] = 0;
            }

            for (int bindingIndex = 0; bindingIndex < _bindings.Count; bindingIndex++)
            {
                UXBinding binding = _bindings[bindingIndex];
                if (binding == null)
                {
                    continue;
                }

                binding.RebuildRuntime(this);

                for (int entryIndex = 0; entryIndex < binding.RuntimeEntryCount; entryIndex++)
                {
                    int controllerSlot = binding.GetRuntimeControllerSlot(entryIndex);
                    if ((uint)controllerSlot >= (uint)controllerCount || !binding.IsRuntimeEntrySupported(entryIndex))
                    {
                        continue;
                    }

                    int nextIndex = _runtimeEntryCounts[controllerSlot];
                    RuntimeBindingEntry[] entries = _runtimeEntriesByController[controllerSlot];
                    if (entries == null || nextIndex >= entries.Length)
                    {
                        int nextLength = entries == null ? 4 : entries.Length << 1;
                        RuntimeBindingEntry[] nextEntries = new RuntimeBindingEntry[nextLength];
                        if (entries != null)
                        {
                            Array.Copy(entries, nextEntries, entries.Length);
                        }

                        entries = nextEntries;
                        _runtimeEntriesByController[controllerSlot] = entries;
                    }

                    entries[nextIndex].Binding = binding;
                    entries[nextIndex].EntryIndex = entryIndex;
                    _runtimeEntryCounts[controllerSlot] = nextIndex + 1;
                }
            }

            _runtimeReady = true;
        }

        private void ApplyCurrentStateToBinding(UXBinding binding)
        {
            if (binding == null)
            {
                return;
            }

            for (int entryIndex = 0; entryIndex < binding.RuntimeEntryCount; entryIndex++)
            {
                int controllerSlot = binding.GetRuntimeControllerSlot(entryIndex);
                if ((uint)controllerSlot >= (uint)_controllers.Count)
                {
                    continue;
                }

                ControllerDefinition controller = _controllers[controllerSlot];
                if (controller != null)
                {
                    binding.ApplyRuntimeEntry(entryIndex, controller.SelectedIndex);
                }
            }
        }
    }
}
