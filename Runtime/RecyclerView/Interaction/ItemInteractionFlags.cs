using System;

namespace AlicizaX.UI
{
    [Flags]
    public enum ItemInteractionFlags
    {
        None = 0,
        PointerClick = 1 << 0,
        PointerEnter = 1 << 1,
        PointerExit = 1 << 2,
        Select = 1 << 3,
        Deselect = 1 << 4,
        Move = 1 << 5,
        BeginDrag = 1 << 6,
        Drag = 1 << 7,
        EndDrag = 1 << 8,
        Submit = 1 << 9,
        Cancel = 1 << 10,
        Navigation = Select | Deselect | Move | Submit | Cancel,
        PointerNavigation = PointerClick | PointerEnter | PointerExit | Navigation,
    }
}
