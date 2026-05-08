using UnityEngine;
using UnityEngine.EventSystems;

namespace AlicizaX.UI
{
    [DisallowMultipleComponent]
    public class Scroller : MonoBehaviour, IScroller, IBeginDragHandler, IEndDragHandler, IDragHandler, IScrollHandler
    {
        private const float MaxInertiaVelocity = 60f;
        private const float MinInertiaDuration = 0.06f;
        private const float MaxInertiaDuration = 0.24f;
        private const float MinInertiaDistanceFactor = 1.5f;
        private const float MaxInertiaDistanceFactor = 6f;

        protected enum MotionState
        {
            Idle,
            Smooth,
            Duration,
            Inertia
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
        private float inertiaDistance;
        private bool notifyMoveStoppedOnComplete;

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

            OnValueChanged?.Invoke(position);
        }

        public void OnScroll(PointerEventData eventData)
        {
            if (!InputEnabled)
            {
                return;
            }

            StopMovement();

            float rate = GetScrollRate() * wheelSpeed;
            velocity = direction == Direction.Vertical ? -eventData.scrollDelta.y * rate : eventData.scrollDelta.x * rate;
            position += velocity;

            OnValueChanged?.Invoke(position);
            StartReleaseMotion();
        }

        internal virtual float GetDelta(PointerEventData eventData)
        {
            float rate = GetScrollRate();
            return direction == Direction.Vertical ? eventData.delta.y * rate : -eventData.delta.x * rate;
        }

        protected float GetScrollRate()
        {
            float rate = 1f;
            float viewLength = ViewLength;
            if (viewLength <= 0f)
            {
                return rate;
            }

            if (position < 0)
            {
                rate = Mathf.Max(0, 1 - (Mathf.Abs(position) / viewLength));
            }
            else if (position > MaxPosition)
            {
                rate = Mathf.Max(0, 1 - (Mathf.Abs(position - MaxPosition) / viewLength));
            }

            return rate;
        }

        protected virtual void Inertia()
        {
            if (Mathf.Abs(velocity) <= 0.1f)
            {
                CompleteMotion(true);
                return;
            }

            StopMovement();
            motionState = MotionState.Inertia;
            motionStartPosition = position;
            motionElapsed = 0f;
            inertiaVelocity = Mathf.Clamp(velocity, -MaxInertiaVelocity, MaxInertiaVelocity);
            float normalizedVelocity = Mathf.Clamp01(Mathf.Abs(inertiaVelocity) / MaxInertiaVelocity);
            motionDuration = Mathf.Lerp(MinInertiaDuration, MaxInertiaDuration, normalizedVelocity);
            float distanceFactor = Mathf.Lerp(MinInertiaDistanceFactor, MaxInertiaDistanceFactor, normalizedVelocity);
            inertiaDistance = inertiaVelocity * distanceFactor;
            notifyMoveStoppedOnComplete = true;
        }

        protected virtual bool StartElasticMotion()
        {
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
            notifyMoveStoppedOnComplete = false;
        }

        private void StartReleaseMotion()
        {
            if (StartElasticMotion())
            {
                return;
            }

            Inertia();
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
            motionElapsed += deltaTime;
            float t = Mathf.Clamp01(motionElapsed / motionDuration);
            float offset = (float)EaseUtil.EaseOutCirc(t) * inertiaDistance;
            float nextPosition = motionStartPosition + offset;
            float maxPosition = MaxPosition;

            if (nextPosition < 0f)
            {
                position = 0f;
                OnValueChanged?.Invoke(position);
                StopMovement();
                StartPositionMotion(0f, 7f, true);
                return;
            }

            if (nextPosition > maxPosition)
            {
                position = maxPosition;
                OnValueChanged?.Invoke(position);
                StopMovement();
                StartPositionMotion(maxPosition, 7f, true);
                return;
            }

            position = nextPosition;
            OnValueChanged?.Invoke(position);

            if (t >= 1f)
            {
                CompleteMotion(true);
            }
        }

        private void CompleteMotion(bool notifyStopped)
        {
            motionState = MotionState.Idle;
            velocity = 0f;
            bool shouldNotify = notifyStopped || notifyMoveStoppedOnComplete;
            notifyMoveStoppedOnComplete = false;

            if (shouldNotify)
            {
                OnMoveStoped?.Invoke();
            }
        }
    }


}
