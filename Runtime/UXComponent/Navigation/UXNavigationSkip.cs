#if INPUTSYSTEM_SUPPORT && UX_NAVIGATION
namespace UnityEngine.UI
{
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/UX Navigation Skip")]
    public sealed class UXNavigationSkip : MonoBehaviour
    {
        private void OnEnable()
        {
            InvalidateNavigation();
        }

        private void OnDisable()
        {
            InvalidateNavigation();
        }

        private void OnTransformParentChanged()
        {
            InvalidateNavigation();
        }

        private static void InvalidateNavigation()
        {
            if (UXNavigationRuntime.TryGetInstance(out var runtime))
            {
                runtime.InvalidateSkipCaches();
            }
        }
    }
}
#endif
