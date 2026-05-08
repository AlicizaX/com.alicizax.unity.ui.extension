using UnityEngine.Events;

namespace AlicizaX.UI
{
    public interface IScroller
    {
        float Position { get; set; }

        void ScrollTo(float position, bool smooth = false);
    }

    public class ScrollerEvent : UnityEvent<float> { }

    public class MoveStopEvent : UnityEvent { }

    public class DraggingEvent : UnityEvent<bool> { }
}
