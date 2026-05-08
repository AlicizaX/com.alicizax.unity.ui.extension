using System;
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

    public class UXSelectable : Selectable
    {
        [SerializeField] private List<TransitionData> m_ChildTransitions = new();

        private SelectionState _state;

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
#if UNITY_EDITOR
        protected override void OnValidate()
        {
            if (isActiveAndEnabled)
            {
                for (int i = 0; i < m_ChildTransitions.Count; i++)
                {
                    DoChildSpriteSwap(m_ChildTransitions[i], null);
                    StartChildColorTween(m_ChildTransitions[i], Color.white, true);
                }
            }

            base.OnValidate();
        }
#endif

        protected override void InstantClearState()
        {
            base.InstantClearState();
            for (int i = 0; i < m_ChildTransitions.Count; i++)
            {
                switch (transition)
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
            if (_state == state) return;
            _state = state;
            base.DoStateTransition(state, instant);
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

                switch (transition)
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
