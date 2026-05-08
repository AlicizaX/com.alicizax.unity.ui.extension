#if INPUTSYSTEM_SUPPORT && UX_NAVIGATION
using AlicizaX.UI.Runtime;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    public interface IUXNavigationCursorPolicy
    {
        void OnNavigationContextChanged(UXInputMode mode, UXNavigationScope previousTopScope, UXNavigationScope currentTopScope);
    }

    public sealed class UXNavigationRuntime : MonoBehaviour
    {
        private const int ScopeCapacity = 128;
        private const int InvalidIndex = -1;

        private static UXNavigationRuntime _instance;
        private static IUXNavigationCursorPolicy _cursorPolicy;

        private readonly UXNavigationScope[] _scopes = new UXNavigationScope[ScopeCapacity];
        private int _scopeCount;

        private UXNavigationScope _topScope;
        private ulong _activationSerial;
        private bool _stateDirty = true;
        private bool _suppressionDirty = true;
        private bool _isFlushingState;
        private bool _isEnsuringSelection;
        private bool _contextNotificationDirty;
        private UXNavigationScope _pendingPreviousTopScope;

        internal static UXNavigationRuntime EnsureInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            GameObject go = new GameObject("[UXNavigationRuntime]");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<UXNavigationRuntime>();
            return _instance;
        }

        internal static bool TryGetInstance(out UXNavigationRuntime runtime)
        {
            runtime = _instance;
            return runtime != null;
        }

        public static void SetCursorPolicy(IUXNavigationCursorPolicy cursorPolicy)
        {
            _cursorPolicy = cursorPolicy;
            if (_instance != null)
            {
                _cursorPolicy?.OnNavigationContextChanged(UXInputModeService.CurrentMode, _instance._topScope, _instance._topScope);
            }
        }

        public static void NotifySelection(GameObject selectedObject)
        {
            if (_instance != null)
            {
                _instance.RecordSelection(selectedObject);
            }
        }

        internal static void RequestRefresh(bool ensureSelection)
        {
            if (_instance != null)
            {
                _instance.RequestRefreshInternal(ensureSelection);
            }
        }

        internal static void RequestEnsureSelection()
        {
            if (_instance != null)
            {
                _instance.FlushStateIfDirty(false, true);
            }
        }

        public static bool IsHolderWithinTopScope(UIHolderObjectBase holder)
        {
            if (_instance == null || _instance._topScope == null || holder == null)
            {
                return true;
            }

            UXNavigationScope scope = holder.GetComponent<UXNavigationScope>();
            if (scope == null)
            {
                scope = holder.GetComponentInParent<UXNavigationScope>(true);
            }

            return scope == _instance._topScope;
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
            hideFlags = HideFlags.HideAndDontSave;
        }

        private void OnEnable()
        {
            UXInputModeService.OnModeChanged += OnInputModeChanged;
        }

        private void OnDisable()
        {
            UXInputModeService.OnModeChanged -= OnInputModeChanged;
        }

        private void OnDestroy()
        {
            UXInputModeService.OnModeChanged -= OnInputModeChanged;
            if (_instance == this)
            {
                _instance = null;
                _cursorPolicy = null;
            }
        }

        internal void RegisterScope(UXNavigationScope scope)
        {
            if (scope == null || scope.RuntimeIndex != InvalidIndex)
            {
                return;
            }

            if (_scopeCount >= _scopes.Length)
            {
                ReportCapacityExceeded();
                return;
            }

            int index = _scopeCount++;
            _scopes[index] = scope;
            scope.RuntimeIndex = index;
            UXInputModeService.EnsureInstance();
            MarkStateDirty();
        }

        internal void UnregisterScope(UXNavigationScope scope)
        {
            if (scope == null)
            {
                return;
            }

            int index = scope.RuntimeIndex;
            if (index < 0 || index >= _scopeCount || _scopes[index] != scope)
            {
                return;
            }

            if (_topScope == scope)
            {
                SetTopScope(null, false);
            }

            scope.IsAvailable = false;
            scope.WasAvailable = false;
            scope.SetNavigationSuppressed(false);
            scope.RuntimeIndex = InvalidIndex;

            int last = --_scopeCount;
            UXNavigationScope movedScope = _scopes[last];
            _scopes[last] = null;
            if (index != last)
            {
                _scopes[index] = movedScope;
                movedScope.RuntimeIndex = index;
            }

            MarkStateDirty();
        }

        internal void MarkStateDirty()
        {
            _stateDirty = true;
            _suppressionDirty = true;
        }

        internal void MarkSuppressionDirty()
        {
            _suppressionDirty = true;
        }

        private void RequestRefreshInternal(bool ensureSelection)
        {
            MarkStateDirty();
            FlushStateIfDirty(true, ensureSelection);
        }

        internal void InvalidateSkipCaches()
        {
            for (int i = 0; i < _scopeCount; i++)
            {
                _scopes[i].InvalidateSkipCacheOnly();
            }

            MarkStateDirty();
        }

        private void FlushStateIfDirty(bool notifyContext, bool ensureSelection = true)
        {
            if (_isFlushingState || (!_stateDirty && !_suppressionDirty && !ensureSelection))
            {
                return;
            }

            _isFlushingState = true;
            UXNavigationScope previousTopScope = _topScope;
            if (_stateDirty)
            {
                UXNavigationScope newTopScope = FindTopScope();
                _stateDirty = false;
                SetTopScope(newTopScope, false);
            }

            if (_suppressionDirty)
            {
                ApplyScopeSuppression();
                _suppressionDirty = false;
            }

            if (ensureSelection && (UXInputModeService.CurrentMode == UXInputMode.Gamepad || UXInputModeService.CurrentMode == UXInputMode.Keyboard))
            {
                EnsureNavigationSelection();
            }

            _isFlushingState = false;
            if (notifyContext)
            {
                NotifyContextIfChanged(previousTopScope, _topScope);
            }
            else if (!ReferenceEquals(previousTopScope, _topScope))
            {
                QueueContextNotification(previousTopScope);
            }
        }

        private UXNavigationScope FindTopScope()
        {
            UXNavigationScope bestScope = null;
            for (int i = 0; i < _scopeCount; i++)
            {
                UXNavigationScope scope = _scopes[i];
                bool available = IsScopeAvailable(scope);
                scope.IsAvailable = available;
                if (scope.WasAvailable != available)
                {
                    scope.WasAvailable = available;
                    if (available)
                    {
                        scope.ActivationSerial = ++_activationSerial;
                    }

                    _suppressionDirty = true;
                }

                if (available && (bestScope == null || IsHigherPriority(scope, bestScope)))
                {
                    bestScope = scope;
                }
            }

            return bestScope;
        }

        private static bool IsScopeAvailable(UXNavigationScope scope)
        {
            if (scope == null || !scope.isActiveAndEnabled || !scope.gameObject.activeInHierarchy)
            {
                return false;
            }

            Canvas canvas = scope.Canvas;
            return canvas != null
                   && canvas.gameObject.layer == UIComponent.UIShowLayer
                   && !scope.IsNavigationSkipped
                   && scope.HasAvailableSelectable();
        }

        private void ApplyScopeSuppression()
        {
            for (int i = 0; i < _scopeCount; i++)
            {
                UXNavigationScope scope = _scopes[i];
                bool suppress = scope.IsAvailable
                                && _topScope != null
                                && scope != _topScope
                                && _topScope.BlockLowerScopes
                                && IsHigherPriority(_topScope, scope);
                scope.SetNavigationSuppressed(suppress);
            }
        }

        private void EnsureNavigationSelection()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null || _topScope == null || !_topScope.RequireSelectionWhenGamepad)
            {
                return;
            }

            GameObject currentSelected = eventSystem.currentSelectedGameObject;
            if (_topScope.IsSelectableOwnedAndValid(currentSelected))
            {
                _topScope.RecordSelection(currentSelected);
                return;
            }

            Selectable preferred = _topScope.GetPreferredSelectable();
            _isEnsuringSelection = true;
            eventSystem.SetSelectedGameObject(preferred != null ? preferred.gameObject : null);
            _isEnsuringSelection = false;
            GameObject selectedObject = eventSystem.currentSelectedGameObject;
            if (selectedObject != null)
            {
                _topScope.RecordSelection(selectedObject);
            }
        }

        private void RecordSelection(GameObject selectedObject)
        {
            if (!_isEnsuringSelection && (_stateDirty || _suppressionDirty))
            {
                FlushStateIfDirty(true);
            }

            if (_topScope != null && _topScope.IsSelectableOwnedAndValid(selectedObject))
            {
                _topScope.RecordSelection(selectedObject);
            }
        }

        private void OnInputModeChanged(UXInputMode mode)
        {
            UXNavigationScope previousTopScope = _topScope;
            if (mode == UXInputMode.Gamepad || mode == UXInputMode.Keyboard)
            {
                FlushStateIfDirty(false, true);
            }

            NotifyContextIfChanged(previousTopScope, _topScope);
        }

        private void SetTopScope(UXNavigationScope topScope, bool notifyContext)
        {
            if (ReferenceEquals(_topScope, topScope))
            {
                return;
            }

            UXNavigationScope previousTopScope = _topScope;
            _topScope = topScope;
            _suppressionDirty = true;
            if (notifyContext)
            {
                NotifyContextIfChanged(previousTopScope, _topScope);
            }
            else
            {
                QueueContextNotification(previousTopScope);
            }
        }

        private void QueueContextNotification(UXNavigationScope previousTopScope)
        {
            if (!_contextNotificationDirty)
            {
                _pendingPreviousTopScope = previousTopScope;
                _contextNotificationDirty = true;
            }
        }

        private void NotifyContextIfChanged(UXNavigationScope previousTopScope, UXNavigationScope currentTopScope)
        {
            if (_contextNotificationDirty)
            {
                previousTopScope = _pendingPreviousTopScope;
                _pendingPreviousTopScope = null;
                _contextNotificationDirty = false;
            }

            _cursorPolicy?.OnNavigationContextChanged(UXInputModeService.CurrentMode, previousTopScope, currentTopScope);
        }

        private static bool IsHigherPriority(UXNavigationScope left, UXNavigationScope right)
        {
            int leftOrder = left.Canvas != null ? left.Canvas.sortingOrder : int.MinValue;
            int rightOrder = right.Canvas != null ? right.Canvas.sortingOrder : int.MinValue;
            if (leftOrder != rightOrder)
            {
                return leftOrder > rightOrder;
            }

            int leftDepth = left.GetHierarchyDepth();
            int rightDepth = right.GetHierarchyDepth();
            if (leftDepth != rightDepth)
            {
                return leftDepth > rightDepth;
            }

            return left.ActivationSerial > right.ActivationSerial;
        }

        private static void ReportCapacityExceeded()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Debug.LogError("UXNavigationRuntime scope capacity exceeded.");
#endif
        }
    }
}
#endif
