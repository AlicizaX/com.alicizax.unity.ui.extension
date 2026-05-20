#if INPUTSYSTEM_SUPPORT && UX_NAVIGATION
namespace AlicizaX.UI.UXNavigation
{
    using UnityEngine;

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
            UXNavigationManager.Instance.InvalidateSkipCaches();
        }
    }
}
#endif
