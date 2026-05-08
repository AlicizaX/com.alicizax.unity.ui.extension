using UnityEngine;

public abstract class InputGlyphBehaviourBase : MonoBehaviour
{
    protected InputDeviceWatcher.InputDeviceCategory CurrentCategory { get; private set; }

    protected virtual void OnEnable()
    {
        CurrentCategory = InputDeviceWatcher.CurrentCategory;
        InputDeviceWatcher.OnDeviceChanged += HandleDeviceChanged;
        InputBindingManager.BindingsChanged += HandleBindingsChanged;
        RefreshGlyph();
    }

    protected virtual void OnDisable()
    {
        InputDeviceWatcher.OnDeviceChanged -= HandleDeviceChanged;
        InputBindingManager.BindingsChanged -= HandleBindingsChanged;
    }

    private void HandleDeviceChanged(InputDeviceWatcher.InputDeviceCategory category)
    {
        InputDeviceWatcher.InputDeviceCategory previousCategory = CurrentCategory;
        CurrentCategory = category;
        OnDeviceCategoryChanged(previousCategory, category);
        RefreshGlyph();
    }

    private void HandleBindingsChanged()
    {
        RefreshGlyph();
    }

    protected virtual void OnDeviceCategoryChanged(
        InputDeviceWatcher.InputDeviceCategory previousCategory,
        InputDeviceWatcher.InputDeviceCategory newCategory)
    {
    }

    protected abstract void RefreshGlyph();
}
