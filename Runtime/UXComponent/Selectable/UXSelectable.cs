using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    [Serializable]
    public class TransitionData
    {
        public Graphic targetGraphic;
        public Selectable.Transition transition = Selectable.Transition.ColorTint;
        public ColorBlock colors = ColorBlock.defaultColorBlock;
        public SpriteState spriteState;
    }

    /// <summary>
    /// 外部驱动的可视交互状态。对应 Selectable 内部 SelectionState，但作为公开 API 使用。
    /// </summary>
    public enum UXSelectionState
    {
        Normal = 0,
        Highlighted = 1,
        Pressed = 2,
        Selected = 3,
        Disabled = 4,
    }

    public class UXSelectable : Selectable
    {
        [SerializeField] private List<TransitionData> m_ChildTransitions = new();
        private SelectionState m_SelectionState;
        private bool m_HasSelectionState;
        private bool m_HasExternalState;
        private UXSelectionState m_ExternalState;
        private Coroutine m_PulseCoroutine;

#if UNITY_EDITOR
        internal static Action<UXSelectable, UXSelectionState> EditorSampleAnimation;
#endif

        /// <summary>
        /// 是否正在由外部强制驱动视觉状态。
        /// </summary>
        public bool HasExternalState => m_HasExternalState;

        /// <summary>
        /// 当前外部强制状态；未启用外部状态时无意义。
        /// </summary>
        public UXSelectionState ExternalState => m_ExternalState;

        /// <summary>
        /// 由外部强制设置视觉状态，并覆盖 Selectable 内部根据 pointer/selected 推导的状态。
        /// 适用于控件本身不是当前 EventSystem 选中对象、但仍需展示高亮/按下等反馈的场景。
        /// </summary>
        public void SetExternalState(UXSelectionState state, bool instant = false)
        {
            m_HasExternalState = true;
            m_ExternalState = state;
            m_HasSelectionState = false;
            DoStateTransition(ToSelectionState(state), instant);
        }

        /// <summary>
        /// 清除外部强制状态，并恢复为 Selectable 当前推导状态。
        /// </summary>
        public void ClearExternalState(bool instant = false)
        {
            StopPulseCoroutine();
            if (!m_HasExternalState)
            {
                return;
            }

            m_HasExternalState = false;
            m_HasSelectionState = false;
            DoStateTransition(currentSelectionState, instant);
        }

        /// <summary>
        /// 短暂切换到 <paramref name="pulseState"/>，结束后恢复到 <paramref name="restoreState"/> 并保持外部状态。
        /// </summary>
        public void PulseExternalState(UXSelectionState pulseState, UXSelectionState restoreState, float duration = -1f)
        {
            if (duration < 0f)
            {
                duration = colors.fadeDuration;
            }

            SetExternalState(pulseState, false);
            StopPulseCoroutine();
            if (!isActiveAndEnabled || duration <= 0f)
            {
                SetExternalState(restoreState, false);
                return;
            }

            m_PulseCoroutine = StartCoroutine(PulseExternalStateCoroutine(restoreState, duration));
        }

        IEnumerator PulseExternalStateCoroutine(UXSelectionState restoreState, float duration)
        {
            var elapsedTime = 0f;
            while (elapsedTime < duration)
            {
                elapsedTime += Time.unscaledDeltaTime;
                yield return null;
            }

            m_PulseCoroutine = null;
            if (m_HasExternalState)
            {
                SetExternalState(restoreState, false);
            }
        }

        static SelectionState ToSelectionState(UXSelectionState state)
        {
            return state switch
            {
                UXSelectionState.Highlighted => SelectionState.Highlighted,
                UXSelectionState.Pressed => SelectionState.Pressed,
                UXSelectionState.Selected => SelectionState.Selected,
                UXSelectionState.Disabled => SelectionState.Disabled,
                _ => SelectionState.Normal,
            };
        }

        static UXSelectionState ToUXSelectionState(SelectionState state)
        {
            return state switch
            {
                SelectionState.Highlighted => UXSelectionState.Highlighted,
                SelectionState.Pressed => UXSelectionState.Pressed,
                SelectionState.Selected => UXSelectionState.Selected,
                SelectionState.Disabled => UXSelectionState.Disabled,
                _ => UXSelectionState.Normal,
            };
        }

        void StopPulseCoroutine()
        {
            if (m_PulseCoroutine == null)
            {
                return;
            }

            StopCoroutine(m_PulseCoroutine);
            m_PulseCoroutine = null;
        }

        void StartChildColorTween(TransitionData transitionData, Color targetColor, bool instant)
        {
            if (transitionData.targetGraphic == null)
                return;
            transitionData.targetGraphic.CrossFadeColor(targetColor, instant ? 0f : transitionData.colors.fadeDuration, true, true);
        }

        void DoChildSpriteSwap(TransitionData transitionData, Sprite newSprite)
        {
            if (transitionData.targetGraphic == null)
                return;

            if (transitionData.targetGraphic is Image img)
                img.overrideSprite = newSprite;
        }

        protected override void InstantClearState()
        {
            StopPulseCoroutine();
            m_HasExternalState = false;
            base.InstantClearState();
            m_HasSelectionState = false;
            for (int i = 0; i < m_ChildTransitions.Count; i++)
            {
                switch (m_ChildTransitions[i].transition)
                {
                    case Transition.ColorTint:
                        StartChildColorTween(m_ChildTransitions[i], Color.white, true);
                        break;
                    case Transition.SpriteSwap:
                        DoChildSpriteSwap(m_ChildTransitions[i], null);
                        break;
                }
            }
        }

        protected override void DoStateTransition(SelectionState state, bool instant)
        {
            if (m_HasExternalState)
            {
                state = ToSelectionState(m_ExternalState);
            }

            if (Application.isPlaying)
            {
                if (m_HasSelectionState && m_SelectionState == state) return;
                m_SelectionState = state;
                m_HasSelectionState = true;
            }

            if (!Application.isPlaying)
                instant = true;

            base.DoStateTransition(state, instant);
#if UNITY_EDITOR
            if (!Application.isPlaying && transition == Transition.Animation)
                EditorSampleAnimation?.Invoke(this, ToUXSelectionState(state));
#endif
            for (int i = 0; i < m_ChildTransitions.Count; i++)
            {
                TransitionData transitionData = m_ChildTransitions[i];
                Color tintColor;
                Sprite transitionSprite;
                switch (state)
                {
                    case SelectionState.Normal:
                        tintColor = transitionData.colors.normalColor;
                        transitionSprite = null;
                        break;
                    case SelectionState.Highlighted:
                        tintColor = transitionData.colors.highlightedColor;
                        transitionSprite = transitionData.spriteState.highlightedSprite;
                        break;
                    case SelectionState.Pressed:
                        tintColor = transitionData.colors.pressedColor;
                        transitionSprite = transitionData.spriteState.pressedSprite;
                        break;
                    case SelectionState.Selected:
                        tintColor = transitionData.colors.selectedColor;
                        transitionSprite = transitionData.spriteState.selectedSprite;
                        break;
                    case SelectionState.Disabled:
                        tintColor = transitionData.colors.disabledColor;
                        transitionSprite = transitionData.spriteState.disabledSprite;
                        break;
                    default:
                        tintColor = Color.black;
                        transitionSprite = null;
                        break;
                }

                switch (transitionData.transition)
                {
                    case Transition.ColorTint:
                        StartChildColorTween(transitionData, tintColor * transitionData.colors.colorMultiplier, instant);
                        break;
                    case Transition.SpriteSwap:
                        DoChildSpriteSwap(transitionData, transitionSprite);
                        break;
                }
            }
        }

        public override Selectable FindSelectableOnLeft()
        {
            if (navigation.mode == Navigation.Mode.Explicit && navigation.selectOnLeft != null && navigation.selectOnLeft.interactable)
            {
                return navigation.selectOnLeft;
            }

            if ((navigation.mode & Navigation.Mode.Horizontal) != 0)
            {
                return FindSelectable(transform.rotation * Vector3.left);
            }

            return null;
        }

        public override Selectable FindSelectableOnRight()
        {
            if (navigation.mode == Navigation.Mode.Explicit && navigation.selectOnRight != null && navigation.selectOnRight.interactable)
            {
                return navigation.selectOnRight;
            }

            if ((navigation.mode & Navigation.Mode.Horizontal) != 0)
            {
                return FindSelectable(transform.rotation * Vector3.right);
            }

            return null;
        }

        public override Selectable FindSelectableOnUp()
        {
            if (navigation.mode == Navigation.Mode.Explicit && navigation.selectOnUp != null && navigation.selectOnUp.interactable)
            {
                return navigation.selectOnUp;
            }

            if ((navigation.mode & Navigation.Mode.Vertical) != 0)
            {
                return FindSelectable(transform.rotation * Vector3.up);
            }

            return null;
        }

        public override Selectable FindSelectableOnDown()
        {
            if (navigation.mode == Navigation.Mode.Explicit && navigation.selectOnDown != null && navigation.selectOnDown.interactable)
            {
                return navigation.selectOnDown;
            }

            if ((navigation.mode & Navigation.Mode.Vertical) != 0)
            {
                return FindSelectable(transform.rotation * Vector3.down);
            }

            return null;
        }
    }
}
