using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityEngine.UI
{
    public enum UXBindingFallbackMode
    {
        KeepCurrent = 0,
        RestoreCapturedDefault = 1,
        UseCustomValue = 2
    }

    public enum UXBindingValueKind
    {
        Boolean = 0,
        Float = 1,
        String = 2,
        Color = 3,
        Vector2 = 4,
        Vector3 = 5,
        ObjectReference = 6
    }

    public enum UXBindingProperty
    {
        GameObjectActive = 0,
        CanvasGroupAlpha = 1,
        CanvasGroupInteractable = 2,
        CanvasGroupBlocksRaycasts = 3,
        GraphicColor = 4,
        GraphicMaterial = 5,
        ImageSprite = 6,
        TextContent = 7,
        TextColor = 8,
        RectTransformAnchoredPosition = 9,
        TransformLocalScale = 10,
        TransformLocalEulerAngles = 11
    }

    [Serializable]
    public sealed class UXBindingValue
    {
        [SerializeField] private bool _boolValue;
        [SerializeField] private float _floatValue;
        [SerializeField] private string _stringValue = string.Empty;
        [SerializeField] private Color _colorValue = Color.white;
        [SerializeField] private Vector2 _vector2Value;
        [SerializeField] private Vector3 _vector3Value;
        [SerializeField] private UnityEngine.Object _objectValue;

        public bool BoolValue
        {
            get => _boolValue;
            set => _boolValue = value;
        }

        public float FloatValue
        {
            get => _floatValue;
            set => _floatValue = value;
        }

        public string StringValue
        {
            get => _stringValue;
            set => _stringValue = value ?? string.Empty;
        }

        public Color ColorValue
        {
            get => _colorValue;
            set => _colorValue = value;
        }

        public Vector2 Vector2Value
        {
            get => _vector2Value;
            set => _vector2Value = value;
        }

        public Vector3 Vector3Value
        {
            get => _vector3Value;
            set => _vector3Value = value;
        }

        public UnityEngine.Object ObjectValue
        {
            get => _objectValue;
            set => _objectValue = value;
        }

        public void CopyFrom(UXBindingValue other)
        {
            if (other == null)
            {
                return;
            }

            _boolValue = other._boolValue;
            _floatValue = other._floatValue;
            _stringValue = other._stringValue;
            _colorValue = other._colorValue;
            _vector2Value = other._vector2Value;
            _vector3Value = other._vector3Value;
            _objectValue = other._objectValue;
        }
    }

    [DisallowMultipleComponent]
    [AddComponentMenu("UX/UX Binding")]
    public sealed class UXBinding : MonoBehaviour
    {
        [Serializable]
        public sealed class BindingEntry
        {
            [Serializable]
            public sealed class IndexedValue
            {
                [SerializeField] private int _index;
                [SerializeField] private UXBindingValue _value = new UXBindingValue();

                public int Index
                {
                    get => Mathf.Max(0, _index);
                    set => _index = Mathf.Max(0, value);
                }

                public UXBindingValue Value => _value;
            }

            [SerializeField] private string _controllerId = string.Empty;
            [SerializeField] private int _controllerIndex;
            [SerializeField] private int _controllerIndexMask = 1;
            [SerializeField] private UXBindingProperty _property = UXBindingProperty.GameObjectActive;
            [SerializeField] private UXBindingValue _value = new UXBindingValue();
            [SerializeField] private List<IndexedValue> _indexedValues = new List<IndexedValue>();
            [SerializeField] private UXBindingFallbackMode _fallbackMode = UXBindingFallbackMode.RestoreCapturedDefault;
            [SerializeField] private UXBindingValue _fallbackValue = new UXBindingValue();
            [HideInInspector] [SerializeField] private UXBindingValue _capturedDefault = new UXBindingValue();
            [HideInInspector] [SerializeField] private bool _hasCapturedDefault;
            [HideInInspector] [SerializeField] private UXBindingProperty _capturedProperty = UXBindingProperty.GameObjectActive;

            public string ControllerId
            {
                get => _controllerId;
                set => _controllerId = value ?? string.Empty;
            }

            public int ControllerIndex
            {
                get => Mathf.Max(0, _controllerIndex);
                set
                {
                    _controllerIndex = Mathf.Max(0, value);
                    _controllerIndexMask = IndexToMask(_controllerIndex);
                }
            }

            public int ControllerIndexMask
            {
                get => NormalizeMask(_controllerIndexMask, _controllerIndex);
                set => _controllerIndexMask = value;
            }

            public UXBindingProperty Property
            {
                get => _property;
                set => _property = value;
            }

            public UXBindingValue Value => _value;
            public List<IndexedValue> IndexedValues => _indexedValues;

            public UXBindingFallbackMode FallbackMode
            {
                get => _fallbackMode;
                set => _fallbackMode = value;
            }

            public UXBindingValue FallbackValue => _fallbackValue;
            public bool HasCapturedDefault => _hasCapturedDefault;

            internal void Normalize()
            {
                _controllerIndex = Mathf.Max(0, _controllerIndex);
                _controllerIndexMask = NormalizeMask(_controllerIndexMask, _controllerIndex);
                if (_indexedValues.Count == 0)
                {
                    EnsureIndexedValue(_controllerIndex).Value.CopyFrom(_value);
                }

                if (_property != UXBindingProperty.GameObjectActive)
                {
                    return;
                }

                if (_fallbackMode == UXBindingFallbackMode.RestoreCapturedDefault)
                {
                    _fallbackMode = UXBindingFallbackMode.UseCustomValue;
                    _fallbackValue.BoolValue = false;
                }
            }

            [NonSerialized] private int _runtimeControllerSlot = -1;
            [NonSerialized] private UXBindingResolvedTarget _runtimeTarget;
            [NonSerialized] private bool _runtimeSupported;

            internal int RuntimeControllerSlot => _runtimeControllerSlot;
            internal bool RuntimeSupported => _runtimeSupported;

            internal void BuildRuntime(GameObject target, UXController controller)
            {
                _runtimeControllerSlot = -1;
                _runtimeSupported = UXBindingPropertyUtility.Resolve(target, _property, out _runtimeTarget);

                if (controller != null && !string.IsNullOrEmpty(_controllerId))
                {
                    controller.TryGetControllerSlot(_controllerId, out _runtimeControllerSlot);
                }

                if (!_runtimeSupported)
                {
                    _hasCapturedDefault = false;
                    _capturedProperty = _property;
                    return;
                }

                if (!_hasCapturedDefault || _capturedProperty != _property)
                {
                    CaptureDefault(in _runtimeTarget);
                }
            }

            internal void CaptureDefault(GameObject target)
            {
                if (!UXBindingPropertyUtility.Resolve(target, _property, out UXBindingResolvedTarget resolvedTarget))
                {
                    _hasCapturedDefault = false;
                    _capturedProperty = _property;
                    return;
                }

                CaptureDefault(in resolvedTarget);
            }

            private void CaptureDefault(in UXBindingResolvedTarget target)
            {
                if (!UXBindingPropertyUtility.CaptureValue(in target, _property, _capturedDefault))
                {
                    _hasCapturedDefault = false;
                    _capturedProperty = _property;
                    return;
                }

                _capturedProperty = _property;
                _hasCapturedDefault = true;
            }

            internal void CaptureCurrentAsValue(GameObject target)
            {
                CaptureCurrentAsValue(target, _controllerIndex);
            }

            internal void CaptureCurrentAsValue(GameObject target, int selectedIndex)
            {
                UXBindingValue value = GetMutableValue(selectedIndex);
                if (!UXBindingPropertyUtility.CaptureValue(target, _property, value))
                {
                    return;
                }

                if (selectedIndex == _controllerIndex)
                {
                    _value.CopyFrom(value);
                }
            }

            internal void CaptureCurrentAsFallback(GameObject target)
            {
                if (!UXBindingPropertyUtility.CaptureValue(target, _property, _fallbackValue))
                {
                    return;
                }
            }

            internal void ResetToCapturedDefault(GameObject target)
            {
                if (target == null)
                {
                    return;
                }

                if (!_hasCapturedDefault || _capturedProperty != _property)
                {
                    CaptureDefault(target);
                }

                UXBindingPropertyUtility.ApplyValue(target, _property, _capturedDefault);
            }

            internal void ApplyRuntime(int selectedIndex)
            {
                if (!_runtimeSupported)
                {
                    return;
                }

                if (!_hasCapturedDefault || _capturedProperty != _property)
                {
                    CaptureDefault(in _runtimeTarget);
                }

                if (_property == UXBindingProperty.GameObjectActive)
                {
                    if (IsSelectedIndexMatched(selectedIndex))
                    {
                        UXBindingPropertyUtility.ApplyValue(in _runtimeTarget, _property, _value);
                        return;
                    }
                }
                else if (TryGetValue(selectedIndex, out UXBindingValue indexedValue))
                {
                    UXBindingPropertyUtility.ApplyValue(in _runtimeTarget, _property, indexedValue);
                    return;
                }

                switch (_fallbackMode)
                {
                    case UXBindingFallbackMode.KeepCurrent:
                        return;
                    case UXBindingFallbackMode.RestoreCapturedDefault:
                        if (_hasCapturedDefault)
                        {
                            UXBindingPropertyUtility.ApplyValue(in _runtimeTarget, _property, _capturedDefault);
                        }
                        return;
                    case UXBindingFallbackMode.UseCustomValue:
                        UXBindingPropertyUtility.ApplyValue(in _runtimeTarget, _property, _fallbackValue);
                        return;
                }
            }

            internal bool IsSelectedIndexMatched(int selectedIndex)
            {
                if (selectedIndex < 0 || selectedIndex >= 31)
                {
                    return false;
                }

                return (ControllerIndexMask & IndexToMask(selectedIndex)) != 0;
            }

            internal bool TryGetValue(int selectedIndex, out UXBindingValue value)
            {
                for (int i = 0; i < _indexedValues.Count; i++)
                {
                    IndexedValue indexedValue = _indexedValues[i];
                    if (indexedValue != null && indexedValue.Index == selectedIndex)
                    {
                        value = indexedValue.Value;
                        return true;
                    }
                }

                if (selectedIndex == _controllerIndex)
                {
                    value = _value;
                    return true;
                }

                value = null;
                return false;
            }

            internal UXBindingValue GetMutableValue(int selectedIndex)
            {
                return EnsureIndexedValue(selectedIndex).Value;
            }

            private IndexedValue EnsureIndexedValue(int selectedIndex)
            {
                selectedIndex = Mathf.Max(0, selectedIndex);
                for (int i = 0; i < _indexedValues.Count; i++)
                {
                    IndexedValue indexedValue = _indexedValues[i];
                    if (indexedValue != null && indexedValue.Index == selectedIndex)
                    {
                        return indexedValue;
                    }
                }

                IndexedValue nextValue = new IndexedValue();
                nextValue.Index = selectedIndex;
                nextValue.Value.CopyFrom(_value);
                _indexedValues.Add(nextValue);
                return nextValue;
            }

            internal static int IndexToMask(int index)
            {
                if (index < 0)
                {
                    return 1;
                }

                if (index >= 31)
                {
                    return 1 << 30;
                }

                return 1 << index;
            }

            private static int NormalizeMask(int mask, int fallbackIndex)
            {
                return mask != 0 ? mask : IndexToMask(fallbackIndex);
            }
        }

        [SerializeField] private UXController _controller;
        [SerializeField] private List<BindingEntry> _entries = new List<BindingEntry>();

        private bool _initialized;

        public UXController Controller => _controller;
        public IReadOnlyList<BindingEntry> Entries => _entries;
        internal int RuntimeEntryCount => _entries.Count;

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            _initialized = true;
            NormalizeEntries();
            EnsureControllerReference();
            RegisterToController();
            BuildRuntimeEntries();
        }

        internal void RebuildRuntime(UXController controller)
        {
            _controller = controller;
            NormalizeEntries();
            BuildRuntimeEntries();
        }

        internal int GetRuntimeControllerSlot(int entryIndex)
        {
            return entryIndex >= 0 && entryIndex < _entries.Count && _entries[entryIndex] != null
                ? _entries[entryIndex].RuntimeControllerSlot
                : -1;
        }

        internal bool IsRuntimeEntrySupported(int entryIndex)
        {
            return entryIndex >= 0 && entryIndex < _entries.Count && _entries[entryIndex] != null && _entries[entryIndex].RuntimeSupported;
        }

        internal void ApplyRuntimeEntry(int entryIndex, int selectedIndex)
        {
            if (entryIndex >= 0 && entryIndex < _entries.Count && _entries[entryIndex] != null)
            {
                _entries[entryIndex].ApplyRuntime(selectedIndex);
            }
        }

        public void SetController(UXController controller)
        {
            if (_controller == controller)
            {
                return;
            }

            if (_controller != null)
            {
                _controller.UnregisterBinding(this);
            }

            _controller = controller;
            RegisterToController();
            BuildRuntimeEntries();
        }

        public void CaptureDefaults()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i] != null)
                {
                    _entries[i].CaptureDefault(gameObject);
                }
            }
        }

        public void ResetToDefaults()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i] != null)
                {
                    _entries[i].ResetToCapturedDefault(gameObject);
                }
            }
        }

        public void PreviewEntry(int entryIndex)
        {
            if (_controller == null || entryIndex < 0 || entryIndex >= _entries.Count)
            {
                return;
            }

            BindingEntry entry = _entries[entryIndex];
            if (entry == null || string.IsNullOrWhiteSpace(entry.ControllerId))
            {
                return;
            }

            _controller.SetControllerIndex(entry.ControllerId, GetFirstSelectedIndex(entry.ControllerIndexMask));
        }

        private static int GetFirstSelectedIndex(int mask)
        {
            for (int i = 0; i < 31; i++)
            {
                if ((mask & BindingEntry.IndexToMask(i)) != 0)
                {
                    return i;
                }
            }

            return 0;
        }

        public void CaptureEntryValue(int entryIndex)
        {
            if (entryIndex < 0 || entryIndex >= _entries.Count || _entries[entryIndex] == null)
            {
                return;
            }

            _entries[entryIndex].CaptureCurrentAsValue(gameObject, GetFirstSelectedIndex(_entries[entryIndex].ControllerIndexMask));
        }

        public void CaptureEntryValue(int entryIndex, int selectedIndex)
        {
            if (entryIndex < 0 || entryIndex >= _entries.Count || _entries[entryIndex] == null)
            {
                return;
            }

            _entries[entryIndex].CaptureCurrentAsValue(gameObject, selectedIndex);
        }

        public void ApplyEntryValue(int entryIndex, int selectedIndex)
        {
            if (entryIndex < 0 || entryIndex >= _entries.Count || _entries[entryIndex] == null)
            {
                return;
            }

            _entries[entryIndex].BuildRuntime(gameObject, _controller);
            _entries[entryIndex].ApplyRuntime(selectedIndex);
        }

        public void CaptureEntryFallbackValue(int entryIndex)
        {
            if (entryIndex < 0 || entryIndex >= _entries.Count || _entries[entryIndex] == null)
            {
                return;
            }

            _entries[entryIndex].CaptureCurrentAsFallback(gameObject);
        }

        private void Reset()
        {
            EnsureControllerReference();
            RegisterToController();
        }

        private void Awake()
        {
            Initialize();
        }

        private void OnValidate()
        {
            NormalizeEntries();
            EnsureControllerReference();
            RegisterToController();
        }

        private void OnDestroy()
        {
            if (_controller != null)
            {
                _controller.UnregisterBinding(this);
            }
        }

        private void EnsureControllerReference()
        {
            if (_controller == null)
            {
                _controller = GetComponentInParent<UXController>();
            }
        }

        private void RegisterToController()
        {
            if (_controller != null)
            {
                _controller.RegisterBinding(this);
            }
        }

        private void BuildRuntimeEntries()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i] != null)
                {
                    _entries[i].BuildRuntime(gameObject, _controller);
                }
            }
        }

        private void NormalizeEntries()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                if (_entries[i] != null)
                {
                    _entries[i].Normalize();
                }
            }
        }
    }
}
