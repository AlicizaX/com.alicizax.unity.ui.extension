#if INPUTSYSTEM_SUPPORT
using System;
using System.IO;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using AlicizaX;

public sealed class InputBindingManager : MonoServiceBehaviour<AppScope>
{
    private const string NULL_BINDING = "__NULL__";
    private const string KEYBOARD_DEVICE = "<Keyboard>";
    private const string MOUSE_DELTA = "<Mouse>/delta";
    private const string MOUSE_SCROLL = "<Mouse>/scroll";
    private const string MOUSE_SCROLL_X = "<Mouse>/scroll/x";
    private const string MOUSE_SCROLL_Y = "<Mouse>/scroll/y";
    private const string KEYBOARD_ESCAPE = "<Keyboard>/escape";

    private const string FILE_NAME = "input_bindings.json";
    public bool debugMode = false;

    private InputActionAsset actions;
    private InputActionRebindingExtensions.RebindingOperation rebindOperation;
    private InputAction rebindAction;
    private int rebindBindingIndex = -1;
    private bool isApplyPending = false;
    private string defaultBindingsJson = string.Empty;
    private string cachedSavePath;
    private readonly Dictionary<string, ActionMap> actionMap = new(StringComparer.Ordinal);
    private readonly HashSet<RebindContext> preparedRebinds = new();
    private readonly Dictionary<string, (ActionMap map, ActionMap.Action action)> actionLookup = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, (ActionMap map, ActionMap.Action action)> actionLookupById = new();
    private readonly HashSet<string> ambiguousActionNames = new(StringComparer.Ordinal);

    public event Action<bool, RebindContext[]> OnApply;
    public event Action<RebindContext> OnRebindPrepare;
    public event Action OnRebindStart;
    public event Action<bool, RebindContext> OnRebindEnd;
    public event Action<RebindContext, RebindContext> OnRebindConflict;
    public static event Action BindingsChanged;


    public IReadOnlyDictionary<string, ActionMap> ActionMaps => actionMap;
    public IReadOnlyCollection<RebindContext> PreparedRebinds => preparedRebinds;
    private string SavePath
    {
        get
        {
            if (!string.IsNullOrEmpty(cachedSavePath))
                return cachedSavePath;

#if UNITY_EDITOR
            string folder = Application.dataPath;
#else
                string folder = Application.persistentDataPath;
#endif
            cachedSavePath = Path.Combine(folder, FILE_NAME);
            return cachedSavePath;
        }
    }

    private void EnsureSaveDirectoryExists()
    {
        var directory = Path.GetDirectoryName(SavePath);
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }

    protected override void Awake()
    {
        base.Awake();
    }

    protected override void OnInitialize()
    {
        if (IsMobilePlatform())
        {
            return;
        }

        if (!AppServices.TryGet(out IInputActionProvider provider) || provider.Actions == null)
        {
            Log.Error("InputBindingManager: IInputActionProvider with InputActionAsset is required.");
            return;
        }

        actions = provider.Actions;

        BuildActionMap();

        defaultBindingsJson = actions.SaveBindingOverridesAsJson();

        if (File.Exists(SavePath))
        {
            var json = File.ReadAllText(SavePath);
            if (!string.IsNullOrEmpty(json))
            {
                actions.LoadBindingOverridesFromJson(json);
                RefreshBindingPathsFromActions();
                BindingsChanged?.Invoke();
                if (debugMode)
                {
                    Log.Info($"Loaded overrides from {SavePath}");
                }
            }
        }

        actions.Enable();
    }

    private static bool IsMobilePlatform()
    {
#if UNITY_ANDROID || UNITY_IOS
        return true;
#else
        return Application.isMobilePlatform;
#endif
    }

    protected override void OnDestroyService()
    {
        rebindOperation?.Dispose();
        rebindOperation = null;

        OnApply = null;
        OnRebindPrepare = null;
        OnRebindStart = null;
        OnRebindEnd = null;
        OnRebindConflict = null;
        BindingsChanged = null;
    }

    private void BuildActionMap()
    {
        actionMap.Clear();
        actionLookup.Clear();
        actionLookupById.Clear();
        ambiguousActionNames.Clear();

        foreach (var map in actions.actionMaps)
        {
            var actionMapObj = new ActionMap(map);
            actionMap.Add(map.name, actionMapObj);

            foreach (var actionPair in actionMapObj.actions)
            {
                RegisterActionLookup(map.name, actionPair.Key, actionMapObj, actionPair.Value);
            }
        }
    }

    private void RegisterActionLookup(string mapName, string actionName, ActionMap map, ActionMap.Action action)
    {
        actionLookupById[action.action.id] = (map, action);
        actionLookup[$"{mapName}/{actionName}"] = (map, action);

        if (ambiguousActionNames.Contains(actionName))
        {
            return;
        }

        if (actionLookup.TryGetValue(actionName, out var existing))
        {
            if (existing.action.action != action.action)
            {
                actionLookup.Remove(actionName);
                ambiguousActionNames.Add(actionName);
                Log.Warning($"[InputBindingManager] Duplicate action name '{actionName}' detected. Use 'MapName/{actionName}' to resolve it.");
            }

            return;
        }

        actionLookup[actionName] = (map, action);
    }

    private void RefreshBindingPathsFromActions()
    {
        foreach (var mapPair in actionMap.Values)
        {
            foreach (var actionPair in mapPair.actions.Values)
            {
                var a = actionPair;
                foreach (var bpair in a.bindings)
                {
                    bpair.Value.bindingPath.EffectivePath = a.action.bindings[bpair.Key].effectivePath;
                }
            }
        }
    }

    public sealed class ActionMap
    {
        public string name;
        public Dictionary<string, Action> actions;

        public ActionMap(InputActionMap map)
        {
            name = map.name;
            int actionCount = map.actions.Count;
            actions = new Dictionary<string, Action>(actionCount);
            foreach (var action in map.actions)
            {
                actions.Add(action.name, new Action(action));
            }
        }

        public sealed class Action
        {
            public InputAction action;
            public Dictionary<int, Binding> bindings;

            public Action(InputAction action)
            {
                this.action = action;
                int count = action.bindings.Count;
                bindings = new Dictionary<int, Binding>(count);

                for (int i = 0; i < count; i++)
                {
                    if (action.bindings[i].isComposite)
                    {
                        int first = i + 1;
                        int last = first;
                        while (last < count && action.bindings[last].isPartOfComposite) last++;
                        for (int p = first; p < last; p++)
                            AddBinding(action.bindings[p], p);
                        i = last - 1;
                    }
                    else
                    {
                        AddBinding(action.bindings[i], i);
                    }
                }

                void AddBinding(InputBinding binding, int bindingIndex)
                {
                    bindings.Add(bindingIndex, new Binding(
                        binding.name,
                        action.name,
                        binding.name,
                        bindingIndex,
                        new BindingPath(binding.path, binding.overridePath),
                        binding
                    ));
                }
            }

            public readonly struct Binding
            {
                public readonly string name;
                public readonly string parentAction;
                public readonly string compositePart;
                public readonly int bindingIndex;
                public readonly BindingPath bindingPath;
                public readonly InputBinding inputBinding;

                public Binding(string name, string parentAction, string compositePart, int bindingIndex,
                    BindingPath bindingPath, InputBinding inputBinding)
                {
                    this.name = name;
                    this.parentAction = parentAction;
                    this.compositePart = compositePart;
                    this.bindingIndex = bindingIndex;
                    this.bindingPath = bindingPath;
                    this.inputBinding = inputBinding;
                }
            }
        }
    }

    public sealed class BindingPath
    {
        public string bindingPath;
        public string overridePath;
        private event Action<string> onEffectivePathChanged;

        public BindingPath(string bindingPath, string overridePath)
        {
            this.bindingPath = bindingPath;
            this.overridePath = overridePath;
        }

        public string EffectivePath
        {
            get => !string.IsNullOrEmpty(overridePath) ? overridePath : bindingPath;
            set
            {
                overridePath = (value == bindingPath) ? string.Empty : value;
                onEffectivePathChanged?.Invoke(EffectivePath);
            }
        }

        public void SubscribeToEffectivePathChanged(Action<string> callback)
        {
            onEffectivePathChanged += callback;
        }

        public void UnsubscribeFromEffectivePathChanged(Action<string> callback)
        {
            onEffectivePathChanged -= callback;
        }

        public void Dispose()
        {
            onEffectivePathChanged = null;
        }
    }

    public sealed class RebindContext
    {
        public InputAction action;
        public int bindingIndex;
        public string overridePath;
        private string cachedToString;

        public RebindContext(InputAction action, int bindingIndex, string overridePath)
        {
            this.action = action;
            this.bindingIndex = bindingIndex;
            this.overridePath = overridePath;
        }

        public override bool Equals(object obj)
        {
            if (obj is not RebindContext other) return false;
            if (action == null || other.action == null) return false;
            return action.id == other.action.id && bindingIndex == other.bindingIndex;
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = 17;
                hashCode = (hashCode * 31) + (action != null ? action.id.GetHashCode() : 0);
                hashCode = (hashCode * 31) + bindingIndex;
                return hashCode;
            }
        }

        public override string ToString()
        {
            if (cachedToString == null && action != null)
            {
                string mapName = action.actionMap != null ? action.actionMap.name : "<no-map>";
                cachedToString = $"{mapName}/{action.name}:{bindingIndex}";
            }

            return cachedToString ?? "<null>";
        }
    }


    private void PerformInteractiveRebinding(InputAction action, int bindingIndex, string deviceMatchPath = null, bool excludeMouseMovementAndScroll = true)
    {
        rebindAction = action;
        rebindBindingIndex = bindingIndex;
        var op = action.PerformInteractiveRebinding(bindingIndex);

        if (!string.IsNullOrEmpty(deviceMatchPath))
        {
            op = op.WithControlsHavingToMatchPath(deviceMatchPath);
        }

        if (excludeMouseMovementAndScroll)
        {
            op = op.WithControlsExcluding(MOUSE_DELTA)
                .WithControlsExcluding(MOUSE_SCROLL)
                .WithControlsExcluding(MOUSE_SCROLL_X)
                .WithControlsExcluding(MOUSE_SCROLL_Y);
        }

        rebindOperation = op
            .OnApplyBinding(HandleApplyBinding)
            .OnComplete(HandleRebindComplete)
            .OnCancel(HandleRebindCancel)
            .WithCancelingThrough(KEYBOARD_ESCAPE)
            .Start();
    }

    private void HandleApplyBinding(InputActionRebindingExtensions.RebindingOperation operation, string path)
    {
        RebindContext preparedContext = new RebindContext(rebindAction, rebindBindingIndex, path);
        if (AnyPreparedRebind(path, rebindAction, rebindBindingIndex, out var existing))
        {
            PrepareRebind(preparedContext);
            PrepareRebind(new RebindContext(existing.action, existing.bindingIndex, NULL_BINDING));
            OnRebindConflict?.Invoke(preparedContext, existing);
        }
        else if (AnyBindingPath(path, rebindAction, rebindBindingIndex, out var duplicate))
        {
            RebindContext conflictingContext = new RebindContext(duplicate.action, duplicate.bindingIndex, duplicate.action.bindings[duplicate.bindingIndex].path);
            PrepareRebind(preparedContext);
            PrepareRebind(new RebindContext(duplicate.action, duplicate.bindingIndex, NULL_BINDING));
            OnRebindConflict?.Invoke(preparedContext, conflictingContext);
        }
        else
        {
            PrepareRebind(preparedContext);
        }
    }

    private void HandleRebindComplete(InputActionRebindingExtensions.RebindingOperation operation)
    {
        if (debugMode)
        {
            Log.Info("[InputBindingManager] Rebind completed");
        }

        actions.Enable();
        OnRebindEnd?.Invoke(true, CreateCurrentRebindContext());
        CleanRebindOperation();
    }

    private void HandleRebindCancel(InputActionRebindingExtensions.RebindingOperation operation)
    {
        if (debugMode)
        {
            Log.Info("[InputBindingManager] Rebind cancelled");
        }

        actions.Enable();
        OnRebindEnd?.Invoke(false, CreateCurrentRebindContext());
        CleanRebindOperation();
    }

    private RebindContext CreateCurrentRebindContext()
    {
        if (rebindAction == null || rebindBindingIndex < 0)
        {
            return null;
        }

        return new RebindContext(rebindAction, rebindBindingIndex, rebindAction.bindings[rebindBindingIndex].effectivePath);
    }

    private void CleanRebindOperation()
    {
        rebindOperation?.Dispose();
        rebindOperation = null;
        rebindAction = null;
        rebindBindingIndex = -1;
    }

    private bool AnyPreparedRebind(string bindingPath, InputAction currentAction, int currentIndex, out RebindContext duplicate)
    {
        foreach (var ctx in preparedRebinds)
        {
            if (ctx.overridePath == bindingPath && (ctx.action != currentAction || (ctx.action == currentAction && ctx.bindingIndex != currentIndex)))
            {
                duplicate = ctx;
                return true;
            }
        }

        duplicate = null;
        return false;
    }

    private bool AnyBindingPath(string bindingPath, InputAction currentAction, int currentIndex, out (InputAction action, int bindingIndex) duplicate)
    {
        foreach (var map in actionMap.Values)
        {
            foreach (var actionPair in map.actions.Values)
            {
                bool isSameAction = actionPair.action == currentAction;

                foreach (var bindingPair in actionPair.bindings)
                {
                    // 跳过当前正在重绑定的同一个 action/binding。
                    if (isSameAction && bindingPair.Key == currentIndex)
                        continue;

                    if (bindingPair.Value.bindingPath.EffectivePath == bindingPath)
                    {
                        duplicate = (actionPair.action, bindingPair.Key);
                        return true;
                    }
                }
            }
        }

        duplicate = default;
        return false;
    }

    private void PrepareRebind(RebindContext context)
    {
        // 移除同一个 action/binding 已暂存的重绑定状态。
        preparedRebinds.Remove(context);

        BindingPath bindingPath = GetBindingPath(context.action, context.bindingIndex);
        if (bindingPath == null) return;

        if (string.IsNullOrEmpty(context.overridePath))
        {
            context.overridePath = bindingPath.bindingPath;
        }

        if (bindingPath.EffectivePath != context.overridePath)
        {
            preparedRebinds.Add(context);
            isApplyPending = true;
            OnRebindPrepare?.Invoke(context);
            if (debugMode)
            {
                Log.Info($"Prepared rebind: {context} -> {context.overridePath}");
            }
        }
    }

    private async Task WriteOverridesToDiskAsync()
    {
        var json = actions.SaveBindingOverridesAsJson();
        EnsureSaveDirectoryExists();
        using (var sw = new StreamWriter(SavePath, false)) await sw.WriteAsync(json);
        if (debugMode)
        {
            Log.Info($"Overrides saved to {SavePath}");
        }
    }

    private bool TryGetActionRecord(string actionName, out (ActionMap map, ActionMap.Action action) result)
    {
        return actionLookup.TryGetValue(actionName, out result);
    }

    private bool TryGetActionRecord(InputAction action, out (ActionMap map, ActionMap.Action action) result)
    {
        if (action != null && actionLookupById.TryGetValue(action.id, out result))
        {
            return result.action.action == action;
        }

        result = default;
        return false;
    }

    #region Public API
    /// <summary>
    /// ?? Action ???????????
    /// </summary>
    public int FindBestBindingIndexForKeyboard(InputAction action, string compositePartName = null)
    {
        if (action == null) return -1;

        int fallbackPart = -1;
        int fallbackNonComposite = -1;
        bool searchingForCompositePart = !string.IsNullOrEmpty(compositePartName);

        for (int i = 0; i < action.bindings.Count; i++)
        {
            var b = action.bindings[i];
            if (searchingForCompositePart)
            {
                if (!b.isPartOfComposite) continue;
                if (!string.Equals(b.name, compositePartName, StringComparison.OrdinalIgnoreCase)) continue;
            }
            bool isKeyboardBinding = (!string.IsNullOrEmpty(b.path) && b.path.StartsWith(KEYBOARD_DEVICE, StringComparison.OrdinalIgnoreCase)) ||
                                     (!string.IsNullOrEmpty(b.effectivePath) && b.effectivePath.StartsWith(KEYBOARD_DEVICE, StringComparison.OrdinalIgnoreCase));

            if (b.isPartOfComposite)
            {
                if (fallbackPart == -1) fallbackPart = i;
                if (isKeyboardBinding) return i;
            }
            else
            {
                if (fallbackNonComposite == -1) fallbackNonComposite = i;
                if (isKeyboardBinding) return i;
            }
        }

        return fallbackNonComposite >= 0 ? fallbackNonComposite : fallbackPart;
    }
    /// <summary>
    /// ?? ActionName ? MapName/ActionName ?? Action?
    /// </summary>
    public static InputAction Action(string actionName)
    {
        return InputActionResolver.Action(actionName);
    }

    public bool TryGetAction(string actionName, out InputAction action)
    {
        if (string.IsNullOrWhiteSpace(actionName))
        {
            action = null;
            return false;
        }

        if (actionLookup.TryGetValue(actionName, out var result))
        {
            action = result.action.action;
            return true;
        }

        action = null;
        return false;
    }

    public bool IsActionNameAmbiguous(string actionName)
    {
        return !string.IsNullOrWhiteSpace(actionName) && ambiguousActionNames.Contains(actionName);
    }
    /// <summary>
    /// ??? Action ???????????
    /// </summary>
    public void StartRebind(string actionName, string compositePartName = null)
    {
        var action = Action(actionName);
        if (action == null) return;
        int bindingIndex = FindBestBindingIndexForKeyboard(action, compositePartName);
        if (bindingIndex < 0)
        {
            Log.Error($"[InputBindingManager] No suitable binding found for action '{actionName}' (part={compositePartName ?? "<null>"})");
            return;
        }

        actions.Disable();
        PerformInteractiveRebinding(action, bindingIndex, KEYBOARD_DEVICE, true);
        OnRebindStart?.Invoke();
        if (debugMode)
        {
            Log.Info("[InputBindingManager] Rebind started");
        }
    }
    /// <summary>
    /// ???????????????
    /// </summary>
    public void CancelRebind() => rebindOperation?.Cancel();
    /// <summary>
    /// ????????????????????
    /// </summary>
    public async Task<bool> ConfirmApply(bool clearConflicts = true)
    {
        if (!isApplyPending) return false;

        RebindContext[] appliedContexts = OnApply != null ? BuildPreparedSnapshot() : null;
        foreach (var ctx in preparedRebinds)
        {
            if (ctx.overridePath == NULL_BINDING && !clearConflicts)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(ctx.overridePath))
            {
                if (ctx.overridePath == NULL_BINDING)
                {
                    ctx.action.RemoveBindingOverride(ctx.bindingIndex);
                }
                else
                {
                    ctx.action.ApplyBindingOverride(ctx.bindingIndex, ctx.overridePath);
                }
            }

            var bp = GetBindingPath(ctx.action, ctx.bindingIndex);
            if (bp != null)
            {
                bp.EffectivePath = ctx.action.bindings[ctx.bindingIndex].effectivePath;
            }
        }

        preparedRebinds.Clear();
        await WriteOverridesToDiskAsync();
        BindingsChanged?.Invoke();
        OnApply?.Invoke(true, appliedContexts);
        isApplyPending = false;
        if (debugMode)
        {
            Log.Info("[InputBindingManager] Apply confirmed and saved.");
        }

        return true;
    }

    private RebindContext[] BuildPreparedSnapshot()
    {
        if (preparedRebinds.Count == 0)
        {
            return Array.Empty<RebindContext>();
        }

        RebindContext[] snapshot = new RebindContext[preparedRebinds.Count];
        preparedRebinds.CopyTo(snapshot);
        return snapshot;
    }

    /// <summary>
    /// ???????????????
    /// </summary>
    public void DiscardPrepared()
    {
        if (!isApplyPending) return;

        RebindContext[] discardedContexts = OnApply != null ? BuildPreparedSnapshot() : null;
        preparedRebinds.Clear();
        isApplyPending = false;
        OnApply?.Invoke(false, discardedContexts);
        if (debugMode)
        {
            Log.Info("[InputBindingManager] Prepared rebinds discarded.");
        }
    }
    /// <summary>
    /// ?????????????????????
    /// </summary>
    public async Task ResetToDefaultAsync()
    {
        if (!string.IsNullOrEmpty(defaultBindingsJson))
        {
            actions.LoadBindingOverridesFromJson(defaultBindingsJson);
        }
        else
        {
            foreach (var map in actionMap.Values)
            {
                foreach (var a in map.actions.Values)
                {
                    for (int b = 0; b < a.action.bindings.Count; b++)
                    {
                        a.action.RemoveBindingOverride(b);
                    }
                }
            }
        }

        RefreshBindingPathsFromActions();
        await WriteOverridesToDiskAsync();
        BindingsChanged?.Invoke();
        if (debugMode)
        {
            Log.Info("Reset to default and saved.");
        }
    }
    /// <summary>
    /// ???? Action ?????????????????
    /// </summary>
    public BindingPath GetBindingPath(string actionName, int bindingIndex = 0)
    {
        if (TryGetActionRecord(actionName, out var result)
            && result.action.bindings.TryGetValue(bindingIndex, out var binding))
        {
            return binding.bindingPath;
        }

        return null;
    }

    public BindingPath GetBindingPath(InputAction action, int bindingIndex = 0)
    {
        if (action == null) return null;

        if (TryGetActionRecord(action, out var result)
            && result.action.bindings.TryGetValue(bindingIndex, out var binding))
        {
            return binding.bindingPath;
        }

        return null;
    }

    #endregion
}

#endif
