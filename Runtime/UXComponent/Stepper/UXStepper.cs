using System;
using UnityEngine.Events;
using UnityEngine.EventSystems;

namespace UnityEngine.UI
{
    public enum UXStepperAxis : byte
    {
        Horizontal = 0,
        Vertical = 1,
    }

    /// <summary>
    /// 步进调值控件：沿 <see cref="axis"/> 方向的导航输入用于触发步进回调（-1 / +1）并消费该次导航事件，
    /// 另一轴向的导航输入照常交给基类 <see cref="UXSelectable"/> 切换焦点；不响应 Submit，以免与窗口确认热键冲突。
    /// </summary>
    [AddComponentMenu("UI/UXStepper", 32)]
    public class UXStepper : UXSelectable
    {
        [Serializable]
        public class StepEvent : UnityEvent<int>
        {
        }

        [SerializeField] private UXStepperAxis m_Axis = UXStepperAxis.Horizontal;
        [SerializeField] private StepEvent m_OnStep = new StepEvent();

        protected UXStepper()
        {
        }

        public UXStepperAxis axis
        {
            get => m_Axis;
            set => m_Axis = value;
        }

        public StepEvent onStep
        {
            get => m_OnStep;
            set => m_OnStep = value ?? new StepEvent();
        }

        public override void OnMove(AxisEventData eventData)
        {
            if (!IsActive() || !IsInteractable())
            {
                base.OnMove(eventData);
                return;
            }

            int delta = ResolveStepDelta(eventData.moveDir);
            if (delta != 0)
            {
                Step(delta);
                eventData.Use();
                return;
            }

            base.OnMove(eventData);
        }

        public void Step(int delta)
        {
            if (delta == 0 || !IsActive() || !IsInteractable())
                return;

            UISystemProfilerApi.AddMarker("Stepper.step", this);
            m_OnStep?.Invoke(delta);
        }

        private int ResolveStepDelta(MoveDirection direction)
        {
            if (m_Axis == UXStepperAxis.Horizontal)
            {
                return direction switch
                {
                    MoveDirection.Left => -1,
                    MoveDirection.Right => 1,
                    _ => 0,
                };
            }

            return direction switch
            {
                MoveDirection.Down => -1,
                MoveDirection.Up => 1,
                _ => 0,
            };
        }
    }
}
