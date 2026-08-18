using UnityEngine;

namespace AlicizaX.UI.UXFeedback
{
    [DisallowMultipleComponent]
    [AddComponentMenu("UI/UX Audio Binder")]
    [DefaultExecutionOrder(-500)]
    public sealed class UXUiAudioBinder : MonoBehaviour
    {
        [SerializeField] private UXUiAudioProfile _profile;

        public UXUiAudioProfile Profile
        {
            get => _profile;
            set => _profile = value;
        }

        private void Awake()
        {
            UXUiAudioRouter.Bind(_profile);
        }

        private void OnDestroy()
        {
            UXUiAudioRouter.Bind(null);
        }
    }
}
