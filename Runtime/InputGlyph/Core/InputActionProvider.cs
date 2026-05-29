#if INPUTSYSTEM_SUPPORT
using System;
using System.Collections.Generic;
using AlicizaX;
using UnityEngine;
using UnityEngine.InputSystem;

[AddComponentMenu("Input/Input Action Provider")]
public sealed class InputActionProvider : MonoBehaviour
{
    [Tooltip("InputActionAsset to read and enable at runtime.")]
    [SerializeField] private InputActionAsset actions;

    private InputActionProviderService service;

    public InputActionAsset Actions => actions;

    private void Awake()
    {
        if (AppServices.TryGet(out IInputActionProvider _))
        {
            Log.Warning("[InputActionProvider] Another IInputActionProvider is already registered.");
            enabled = false;
            return;
        }

        service = new InputActionProviderService(actions);
        AppServices.App.Register<IInputActionProvider>(service);
    }

    private void OnDestroy()
    {
        if (!AppServices.HasWorld)
        {
            return;
        }

        if (service != null)
        {
            if (AppServices.App.TryGet(out IInputActionProvider current) && ReferenceEquals(current, service))
            {
                AppServices.App.Unregister<IInputActionProvider>();
            }

            service = null;
        }
    }

    private sealed class InputActionProviderService : ServiceBase, IInputActionProvider
    {
        private readonly Dictionary<string, InputAction> actionLookup = new(StringComparer.Ordinal);
        private readonly HashSet<string> ambiguousActionNames = new(StringComparer.Ordinal);
        private InputActionAsset actions;

        public InputActionProviderService(InputActionAsset actions)
        {
            this.actions = actions;
        }

        public InputActionAsset Actions => actions;

        public void SetActions(InputActionAsset inputActions)
        {
            if (actions == inputActions && IsInitialized)
            {
                return;
            }

            actions = inputActions;
            if (IsInitialized)
            {
                BuildActionLookup();
                actions?.Enable();
            }
        }

        protected override void OnInitialize()
        {
            BuildActionLookup();
            actions?.Enable();
        }

        protected override void OnDestroyService()
        {
            actionLookup.Clear();
            ambiguousActionNames.Clear();
        }

        public bool TryGetAction(string actionName, out InputAction action)
        {
            if (!string.IsNullOrWhiteSpace(actionName) && actionLookup.TryGetValue(actionName, out action))
            {
                return true;
            }

            action = null;
            return false;
        }

        public bool IsActionNameAmbiguous(string actionName)
        {
            return !string.IsNullOrWhiteSpace(actionName) && ambiguousActionNames.Contains(actionName);
        }

        private void BuildActionLookup()
        {
            actionLookup.Clear();
            ambiguousActionNames.Clear();

            if (actions == null)
            {
                Log.Error("[InputActionProvider] InputActionAsset not assigned.");
                return;
            }

            foreach (InputActionMap map in actions.actionMaps)
            {
                foreach (InputAction action in map.actions)
                {
                    RegisterActionLookup(map.name, action.name, action);
                }
            }
        }

        private void RegisterActionLookup(string mapName, string actionName, InputAction action)
        {
            actionLookup[$"{mapName}/{actionName}"] = action;

            if (ambiguousActionNames.Contains(actionName))
            {
                return;
            }

            if (actionLookup.TryGetValue(actionName, out InputAction existing))
            {
                if (existing != action)
                {
                    actionLookup.Remove(actionName);
                    ambiguousActionNames.Add(actionName);
                    Log.Warning($"[InputActionProvider] Duplicate action name '{actionName}' detected. Use 'MapName/{actionName}' to resolve it.");
                }

                return;
            }

            actionLookup[actionName] = action;
        }
    }
}

#endif
