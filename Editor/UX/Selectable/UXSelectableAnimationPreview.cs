using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.UI;

namespace UnityEditor.UI
{
    [InitializeOnLoad]
    internal static class UXSelectableAnimationPreview
    {
        static bool s_OwnsAnimationMode;

        static UXSelectableAnimationPreview()
        {
            UXSelectable.EditorSampleAnimation = Sample;
            AssemblyReloadEvents.beforeAssemblyReload += Shutdown;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        static void OnPlayModeStateChanged(PlayModeStateChange change)
        {
            if (change == PlayModeStateChange.ExitingEditMode)
                Shutdown();
        }

        static void Shutdown()
        {
            UXSelectable.EditorSampleAnimation = null;
            if (!s_OwnsAnimationMode)
                return;

            if (AnimationMode.InAnimationMode())
                AnimationMode.StopAnimationMode();

            s_OwnsAnimationMode = false;
        }

        static void Sample(UXSelectable selectable, UXSelectionState state)
        {
            if (selectable == null || Application.isPlaying)
                return;

            if (PrefabUtility.IsPartOfPrefabAsset(selectable))
                return;

            Animator animator = selectable.animator;
            if (animator == null || !animator.isActiveAndEnabled || animator.runtimeAnimatorController == null)
                return;

            string trigger = GetTriggerName(selectable.animationTriggers, state);
            if (string.IsNullOrEmpty(trigger))
                return;

            AnimationClip clip = FindClip(animator.runtimeAnimatorController, trigger);
            if (clip == null)
                return;

            if (!AnimationMode.InAnimationMode())
            {
                AnimationMode.StartAnimationMode();
                s_OwnsAnimationMode = true;
            }

            AnimationMode.BeginSampling();
            AnimationMode.SampleAnimationClip(selectable.gameObject, clip, clip.length);
            AnimationMode.EndSampling();
            SceneView.RepaintAll();
        }

        static string GetTriggerName(AnimationTriggers triggers, UXSelectionState state)
        {
            if (triggers == null)
                return string.Empty;

            switch (state)
            {
                case UXSelectionState.Highlighted:
                    return triggers.highlightedTrigger;
                case UXSelectionState.Pressed:
                    return triggers.pressedTrigger;
                case UXSelectionState.Selected:
                    return triggers.selectedTrigger;
                case UXSelectionState.Disabled:
                    return triggers.disabledTrigger;
                default:
                    return triggers.normalTrigger;
            }
        }

        static AnimationClip FindClip(RuntimeAnimatorController controller, string stateName)
        {
            if (controller == null || string.IsNullOrEmpty(stateName))
                return null;

            AnimatorOverrideController overrideController = controller as AnimatorOverrideController;
            RuntimeAnimatorController source = overrideController != null
                ? overrideController.runtimeAnimatorController
                : controller;

            AnimationClip clip = FindStateClip(source, stateName);
            if (clip != null && overrideController != null)
            {
                var overrides = new System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<AnimationClip, AnimationClip>>();
                overrideController.GetOverrides(overrides);
                for (int i = 0; i < overrides.Count; i++)
                {
                    if (overrides[i].Key == clip && overrides[i].Value != null)
                        return overrides[i].Value;
                }
            }

            if (clip != null)
                return clip;

            AnimationClip[] clips = controller.animationClips;
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] != null && clips[i].name == stateName)
                    return clips[i];
            }

            return null;
        }

        static AnimationClip FindStateClip(RuntimeAnimatorController controller, string stateName)
        {
            AnimatorController animatorController = controller as AnimatorController;
            if (animatorController == null)
                return null;

            AnimatorControllerLayer[] layers = animatorController.layers;
            for (int i = 0; i < layers.Length; i++)
            {
                AnimationClip clip = FindStateClip(layers[i].stateMachine, stateName);
                if (clip != null)
                    return clip;
            }

            return null;
        }

        static AnimationClip FindStateClip(AnimatorStateMachine stateMachine, string stateName)
        {
            if (stateMachine == null)
                return null;

            ChildAnimatorState[] states = stateMachine.states;
            for (int i = 0; i < states.Length; i++)
            {
                AnimatorState state = states[i].state;
                if (state != null && state.name == stateName)
                    return state.motion as AnimationClip;
            }

            ChildAnimatorStateMachine[] children = stateMachine.stateMachines;
            for (int i = 0; i < children.Length; i++)
            {
                AnimationClip clip = FindStateClip(children[i].stateMachine, stateName);
                if (clip != null)
                    return clip;
            }

            return null;
        }
    }
}
