#if INPUTSYSTEM_SUPPORT
using AlicizaX;
using UnityEngine.InputSystem;

public static class InputActionResolver
{
    public static InputAction Action(string actionName)
    {
        if (TryGetAction(actionName, out InputAction action))
        {
            return action;
        }

        if (IsActionNameAmbiguous(actionName))
        {
            Log.Error($"[InputActionResolver] Action name '{actionName}' is ambiguous. Use 'MapName/{actionName}' instead.");
            return null;
        }

        Log.Error($"[InputActionResolver] Could not find action '{actionName}'");
        return null;
    }

    public static bool TryGetAction(string actionName, out InputAction action)
    {
        if (AppServices.TryGet(out IInputActionProvider provider)
            && provider.TryGetAction(actionName, out action))
        {
            return true;
        }

        action = null;
        return false;
    }

    private static bool IsActionNameAmbiguous(string actionName)
    {
        return AppServices.TryGet(out IInputActionProvider provider) && provider.IsActionNameAmbiguous(actionName);
    }
}

#endif
