using UnityEngine;

namespace AlicizaX.UI.UXFeedback
{
    public static class UXUiFeedback
    {
        public static void Raise(Component source, UXUiCue cue)
        {
            UXUiAudioRouter.Dispatch(source, cue);
        }
    }
}
