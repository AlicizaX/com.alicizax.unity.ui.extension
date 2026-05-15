using UnityEngine;
using UnityEngine.EventSystems;

namespace AlicizaX.UI
{
    [DisallowMultipleComponent]
    public class Scroller : MonoBehaviour, IScroller, IBeginDragHandler, IEndDragHandler, IDragHandler, IScrollHandler
    {
        private const float LegacyMouseWheelStep = 120f;

        protected enum MotionState
        {
            Idle,
            Smooth,
            Duration,
            Inertia,
            Wheel
        }

        protected float position;

        public float Position
        {
            get => position;
            set => position = value;
        }

        protected float velocity;
        public float Velocity => velocity;

        protected Direction direction;

        public Direction Direction
        {
            get => direction;
            set => direction = value;
        }

        protected Vector2 contentSize;

        public Vector2 ContentSize
        {
            get => contentSize;
            set => contentSize = value;
        }

        protected Vector2 viewSize;

        public Vector2 ViewSize
        {
            get => viewSize;
            set => viewSize = value;
        }

        protected float scrollSpeed = 1f;

        public float ScrollSpeed
        {
            get => scrollSpeed;
            set => scrollSpeed = value;
        }

        protected float wheelSpeed = 30f;

        public float WheelSpeed
        {
            get => wheelSpeed;
            set => wheelSpeed = value;
        }

        protected bool snap;

        public bool Snap
        {
            get => snap;
            set => snap = value;
        }

        protected MovementType movementType = MovementType.Elastic;

        public MovementType MovementType
        {
            get => movementType;
            set => movementType = value;
        }

        protected bool inertia = true;

        public bool Inertia
        {
            get => inertia;
            set => inertia = value;
        }

        protected float decelerationRate = 0.135f;

        public float DecelerationRate
        {
            get => decelerationRate;
            set => decelerationRate = Mathf.Clamp(value, 0.001f, 0.999f);
        }

        protected ScrollerEvent scrollerEvent = new();
        protected MoveStopEvent moveStopEvent = new();
        protected DraggingEvent draggingEvent = new();

        private MotionState motionState;
        private float motionStartPosition;
        private float motionTargetPosition;
        private float motionElapsed;
        private float motionDuration;
        private float motionSpeed;
        private float inertiaVelocity;
        private float wheelTargetPosition;
        private bool notifyMoveStoppedOnComplete;
        private float dragStopTime;
        private float trackedVelocity;

        public float MaxPosition => direction == Direction.Vertical ? Mathf.Max(contentSize.y - viewSize.y, 0) : Mathf.Max(contentSize.x - viewSize.x, 0);

        public float ViewLength => direction == Direction.Vertical ? viewSize.y : viewSize.x;

        public ScrollerEvent OnValueChanged
        {
            get => scrollerEvent;
            set => scrollerEvent = value;
        }

        public MoveStopEvent OnMoveStoped
        {
            get => moveStopEvent;
            set => moveStopEvent = value;
        }

        public DraggingEvent OnDragging
        {
            get => draggingEvent;
            set => draggingEvent = value;
        }

        public bool InputEnabled { get; set; } = true;

        protected virtual void Awake()
        {
        }

        protected virtual void Update()
        {
            if (motionState == MotionState.Idle)
            {
                return;
            }

            TickMotion(Time.deltaTime);
        }

        private void TickMotion(float deltaTime)
        {
            switch (motionState)
            {
                case MotionState.Smooth:
                    TickSmooth(deltaTime);
                    break;
                case MotionState.Duration:
                    TickDuration(deltaTime);
                    break;
                case MotionState.Inertia:
                    TickInertia(deltaTime);
                    break;
                case MotionState.Wheel:
                    TickWheel(deltaTime);
                    break;
                default:
                    motionState = MotionState.Idle;
                    break;
            }
        }

        public virtual void ScrollTo(float position, bool smooth = false)
        {
            if (Mathf.Approximately(position, this.position)) return;

            StopMovement();

            if (!smooth)
            {
                this.position = position;
                OnValueChanged?.Invoke(this.position);
                return;
            }

            StartPositionMotion(position, scrollSpeed, true);
        }

        public virtual void ScrollToDuration(float position, float duration)
        {
            if (Mathf.Approximately(position, this.position))
            {
                return;
            }

            StopMovement();
            if (duration <= 0f)
            {
                this.position = position;
                OnValueChanged?.Invoke(this.position);
                return;
            }

            motionState = MotionState.Duration;
            motionStartPosition = this.position;
            motionTargetPosition = position;
            motionDuration = Mathf.Max(duration, 0.0001f);
            motionElapsed = 0f;
            notifyMoveStoppedOnComplete = true;
        }

        public virtual void ScrollToRatio(float ratio)
        {
            ScrollTo(MaxPosition * ratio, false);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (!InputEnabled)
            {
                return;
            }

            OnDragging?.Invoke(true);
            StopMovement();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!InputEnabled)
            {
                return;
            }

            StartReleaseMotion();
            OnDragging?.Invoke(false);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!InputEnabled)
            {
                return;
            }

            velocity = GetDelta(eventData);
            position += velocity;

            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                trackedVelocity = velocity / dt;
            }
            dragStopTime = Time.time;

            if (movementType == MovementType.Clamped)
            {
                position = ClampPosition(position);
            }

            OnValueChanged?.Invoke(position);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!InputEnabled)
            {
                return;
            }

            if (motionState != MotionState.Wheel)
            {
                StopMovement();
                wheelTargetPosition = position;
            }

            float rate = GetScrollRate() * wheelSpeed;
            Vector2 normalizedDelta = NormalizeScrollDelta(eventData.scrollDelta);
            velocity = direction == Direction.Vertical ? -normalizedDelta.y * rate : normalizedDelta.x * rate;
            wheelTargetPosition = ClampPosition(wheelTargetPosition + velocity);

            if (Mathf.Approximately(wheelTargetPosition, position))
            {
                return;
            }

            float distance = wheelTargetPosition - position;
            position = ClampPosition(position + distance * 0.85f);
            OnValueChanged?.Invoke(position);

            motionState = MotionState.Wheel;
            notifyMoveStoppedOnComplete = true;
        }

        internal virtual float GetDelta(PointerEventData eventData)
        {
            float rate = GetScrollRate();
            return direction == Direction.Vertical ? eventData.delta.y * rate : -eventData.delta.x * rate;
        }

        private static Vector2 NormalizeScrollDelta(Vector2 delta)
        {
            return new Vector2(NormalizeScrollAxis(delta.x), NormalizeScrollAxis(delta.y));
        }

        private static float NormalizeScrollAxis(float value)
        {
            float magnitude = Mathf.Abs(value);
            if (magnitude <= 0f)
            {
                return 0f;
            }

            // Legacy input modules can emit +/-120 per wheel notch, while newer input paths
            // usually emit +/-1. Normalize both to a stable per-notch scale for WheelSpeed.
            if (magnitude > 10f)
            {
                value /= LegacyMouseWheelStep;
            }

            return value;
        }

        protected virtual float GetScrollRate()
        {
            float rate = 1f;
            float viewLength = ViewLength;
            if (viewLength <= 0f)
            {
                return rate;
            }

            if (position < 0)
            {
                rate = movementType == MovementType.Clamped
                    ? 0f
                    : Mathf.Max(0, 1 - (Mathf.Abs(position) / viewLength));
            }
            else if (position > MaxPosition)
            {
                rate = movementType == MovementType.Clamped
                    ? 0f
                    : Mathf.Max(0, 1 - (Mathf.Abs(position - MaxPosition) / viewLength));
            }

            return rate;
        }

        protected virtual void StartInertia()
        {
            if ((Time.time - dragStopTime) > 0.03f)
            {
                CompleteMotion(true);
                return;
            }

            if (Mathf.Abs(trackedVelocity) <= 50f)
            {
                CompleteMotion(true);
                return;
            }

            StopMovement();
            motionState = MotionState.Inertia;
            float maxVelocity = ViewLength * 5f;
            inertiaVelocity = Mathf.Clamp(trackedVelocity, -maxVelocity, maxVelocity);
            notifyMoveStoppedOnComplete = true;
        }

        protected virtual bool StartElasticMotion()
        {
            if (movementType == MovementType.Clamped)
            {
                position = ClampPosition(position);
                return false;
            }

            if (position < 0)
            {
                StopMovement();
                StartPositionMotion(0, 7f, true);
                return true;
            }

            if (position > MaxPosition)
            {
                StopMovement();
                StartPositionMotion(MaxPosition, 7f, true);
                return true;
            }

            return false;
        }

        protected void StopMovement()
        {
            motionState = MotionState.Idle;
            wheelTargetPosition = position;
            notifyMoveStoppedOnComplete = false;
        }

        private void StartReleaseMotion()
        {
            if (StartElasticMotion())
            {
                return;
            }

            if (!inertia)
            {
                CompleteMotion(true);
                return;
            }

            StartInertia();
        }

        private void StartPositionMotion(float targetPosition, float speed, bool notifyStopped)
        {
            motionState = MotionState.Smooth;
            motionStartPosition = position;
            motionTargetPosition = targetPosition;
            motionElapsed = Time.deltaTime;
            motionSpeed = speed;
            notifyMoveStoppedOnComplete = notifyStopped;
        }

        private void TickSmooth(float deltaTime)
        {
            if (Mathf.Abs(motionTargetPosition - position) <= 0.1f)
            {
                position = motionTargetPosition;
                OnValueChanged?.Invoke(position);
                CompleteMotion(false);
                return;
            }

            position = Mathf.Lerp(motionStartPosition, motionTargetPosition, motionElapsed * motionSpeed);
            motionElapsed += deltaTime;
            OnValueChanged?.Invoke(position);
        }

        private void TickDuration(float deltaTime)
        {
            motionElapsed += deltaTime;
            float t = Mathf.Clamp01(motionElapsed / motionDuration);
            position = Mathf.Lerp(motionStartPosition, motionTargetPosition, t);
            OnValueChanged?.Invoke(position);

            if (t >= 1f)
            {
                position = motionTargetPosition;
                OnValueChanged?.Invoke(position);
                CompleteMotion(false);
            }
        }

        private void TickInertia(float deltaTime)
        {
            inertiaVelocity *= Mathf.Pow(decelerationRate, deltaTime);

            if (Mathf.Abs(inertiaVelocity) < 1f)
            {
                if (movementType == MovementType.Elastic && !IsInBounds(position))
                {
                    StartElasticReturn();
                }
                else
                {
                    CompleteMotion(true);
                }
                return;
            }

            float nextPosition = position + inertiaVelocity * deltaTime;

            if (movementType == MovementType.Elastic)
            {
                float overscroll = GetOverscroll(nextPosition);
                if (Mathf.Abs(overscroll) > 0f)
                {
                    float damping = 1f - Mathf.Clamp01(Mathf.Abs(overscroll) / ViewLength);
                    inertiaVelocity *= damping;

                    if (Mathf.Abs(inertiaVelocity) < 50f)
                    {
                        position = nextPosition;
                        OnValueChanged?.Invoke(position);
                        StartElasticReturn();
                        return;
                    }
                }

                position = nextPosition;
                OnValueChanged?.Invoke(position);
            }
            else
            {
                float clampedPosition = ClampPosition(nextPosition);
                position = clampedPosition;
                OnValueChanged?.Invoke(position);

                if (!Mathf.Approximately(clampedPosition, nextPosition))
                {
                    CompleteMotion(true);
                }
            }
        }

        private float GetOverscroll(float pos)
        {
            if (pos < 0f) return pos;
            if (pos > MaxPosition) return pos - MaxPosition;
            return 0f;
        }

        private bool IsInBounds(float pos)
        {
            return pos >= 0f && pos <= MaxPosition;
        }

        private void StartElasticReturn()
        {
            float target = position < 0f ? 0f : MaxPosition;
            StopMovement();
            StartPositionMotion(target, 7f, true);
        }

        private void TickWheel(float deltaTime)
        {
            float target = ClampPosition(wheelTargetPosition);
            if (Mathf.Abs(target - position) <= 0.1f)
            {
                position = target;
                OnValueChanged?.Invoke(position);
                CompleteMotion(true);
                return;
            }

            float speed = 15f;
            position = Mathf.Lerp(position, target, 1f - Mathf.Exp(-speed * deltaTime));
            OnValueChanged?.Invoke(position);
        }

        public virtual float ClampPosition(float value)
        {
            return Mathf.Clamp(value, 0f, MaxPosition);
        }

        private void CompleteMotion(bool notifyStopped)
        {
            motionState = MotionState.Idle;
            velocity = 0f;
            wheelTargetPosition = position;
            bool shouldNotify = notifyStopped || notifyMoveStoppedOnComplete;
            notifyMoveStoppedOnComplete = false;

            if (shouldNotify)
            {
                OnMoveStoped?.Invoke();
            }
        }
    }


}
