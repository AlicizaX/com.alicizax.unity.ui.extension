using System;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    [AddComponentMenu("UI/UXToggle", 30)]
    [RequireComponent(typeof(RectTransform))]
    public class UXToggle : UXSelectable, IPointerClickHandler, ISubmitHandler, ICanvasElement
    {
        [Serializable]
        public class ToggleEvent : UnityEvent<bool>
        {
        }

        public Toggle.ToggleTransition toggleTransition = Toggle.ToggleTransition.Fade;
        public Graphic graphic;

        [SerializeField] private UXGroup m_Group;

        public UXGroup group
        {
            get { return m_Group; }
            set
            {
                SetToggleGroup(value, true);
                PlayEffect(true);
            }
        }

        public ToggleEvent onValueChanged = new ToggleEvent();

        [Tooltip("Is the toggle currently on or off?")] [SerializeField]
        private bool m_IsOn;

        protected UXToggle()
        {
        }

#if UNITY_EDITOR
        protected override void OnValidate()
        {
            base.OnValidate();

            if (!UnityEditor.PrefabUtility.IsPartOfPrefabAsset(this) && !Application.isPlaying)
                CanvasUpdateRegistry.RegisterCanvasElementForLayoutRebuild(this);
        }
#endif

        public virtual void Rebuild(CanvasUpdate executing)
        {
#if UNITY_EDITOR
            if (executing == CanvasUpdate.Prelayout)
                PlayEffect(true);
#endif
        }

        public virtual void LayoutComplete()
        {
        }

        public virtual void GraphicUpdateComplete()
        {
        }

        protected override void OnDestroy()
        {
            if (m_Group != null)
                m_Group.UnregisterToggle(this);
            base.OnDestroy();
        }

        protected override void OnDisable()
        {
            if (m_Group != null)
                m_Group.UnregisterToggle(this);

            base.OnDisable();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            if (m_Group != null)
                SetToggleGroup(m_Group, false);

            PlayEffect(true);
        }

        protected override void OnDidApplyAnimationProperties()
        {
            if (graphic != null)
            {
                bool oldValue = !Mathf.Approximately(graphic.canvasRenderer.GetColor().a, 0);
                if (m_IsOn != oldValue)
                {
                    m_IsOn = oldValue;
                    Set(!oldValue);
                }
            }

            base.OnDidApplyAnimationProperties();
        }


        internal void SetToggleGroupInternal(UXGroup newGroup, bool setMemberValue)
        {
            if (setMemberValue)
                m_Group = newGroup;
        }

        protected virtual void SetToggleGroup(UXGroup newGroup, bool setMemberValue)
        {
            if (m_Group == newGroup)
            {
                if (setMemberValue)
                    m_Group = newGroup;

                if (newGroup != null && !newGroup.ContainsToggle(this))
                {
                    newGroup.RegisterToggle(this);
                }

                if (newGroup != null)
                    newGroup.EnsureValidState();

                return;
            }

            if (m_Group != null)
                m_Group.UnregisterToggle(this);

            if (setMemberValue)
                m_Group = newGroup;

            if (newGroup != null)
            {
                if (!newGroup.ContainsToggle(this))
                {
                    newGroup.RegisterToggle(this);
                }

                if (isOn)
                    newGroup.NotifyToggleOn(this);

                newGroup.EnsureValidState();
            }
        }

        public bool isOn
        {
            get { return m_IsOn; }
            set { Set(value); }
        }

        public void SetIsOnWithoutNotify(bool value)
        {
            Set(value, false);
        }

        protected override void DoStateTransition(Selectable.SelectionState state, bool instant)
        {
            if (state == Selectable.SelectionState.Disabled)
            {
                base.DoStateTransition(state, instant);
                return;
            }

            if (m_IsOn)
                state = Selectable.SelectionState.Selected;

            base.DoStateTransition(state, instant);
        }

        protected virtual void Set(bool value, bool sendCallback = true)
        {
            if (m_IsOn == value)
                return;

            OnBeforeValueChanged(value);

            m_IsOn = value;
            if (m_Group != null && m_Group.isActiveAndEnabled && IsActive())
            {
                if (m_IsOn || (!m_Group.AnyTogglesOn() && !m_Group.allowSwitchOff))
                {
                    m_IsOn = true;
                    m_Group.NotifyToggleOn(this, sendCallback);
                }
            }

            PlayEffect(toggleTransition == Toggle.ToggleTransition.None);

            if (sendCallback)
            {
                UISystemProfilerApi.AddMarker("Toggle.value", this);
                onValueChanged.Invoke(m_IsOn);
            }

            bool instant = (toggleTransition == Toggle.ToggleTransition.None);

            var stateToApply = m_IsOn ? Selectable.SelectionState.Selected : currentSelectionState;
            DoStateTransition(stateToApply, instant);
            OnAfterValueChanged(m_IsOn);
        }

        protected virtual void OnBeforeValueChanged(bool value)
        {
        }

        protected virtual void OnAfterValueChanged(bool value)
        {
        }

        protected virtual void PlayEffect(bool instant)
        {
            if (graphic == null)
                return;

            float alpha = m_IsOn ? 1f : 0f;

#if UNITY_EDITOR
            if (!Application.isPlaying)
                graphic.canvasRenderer.SetAlpha(alpha);
            else
#endif
            if (instant)
                graphic.canvasRenderer.SetAlpha(alpha);
            else
                graphic.CrossFadeAlpha(alpha, 0.1f, true);
        }


        protected override void Start()
        {
            PlayEffect(true);
        }

        private void InternalToggle()
        {
            if (!IsActive() || !IsInteractable())
                return;

            isOn = !isOn;
        }

        public override void OnPointerEnter(PointerEventData eventData)
        {
            base.OnPointerEnter(eventData);
            PlayAudio(hoverAudioClip);
        }

        public virtual void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left)
                return;

            InternalToggle();
            PlayAudio(clickAudioClip);
        }

        public override void OnSelect(BaseEventData eventData)
        {
            base.OnSelect(eventData);
#if INPUTSYSTEM_SUPPORT && UX_NAVIGATION
            UXNavigationRuntime.NotifySelection(gameObject);
#endif
            if (eventData is PointerEventData)
                return;
            PlayAudio(hoverAudioClip);
        }

        public virtual void OnSubmit(BaseEventData eventData)
        {
            InternalToggle();
            PlayAudio(clickAudioClip);
        }

        private void PlayAudio(AudioClip clip)
        {
            if (clip && UXComponentExtensionsHelper.AudioHelper != null)
                UXComponentExtensionsHelper.AudioHelper.PlayAudio(clip);
        }


        [SerializeField] private AudioClip hoverAudioClip;
        [SerializeField] private AudioClip clickAudioClip;
    }
}
