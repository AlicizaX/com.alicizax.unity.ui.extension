using System;
using System.Collections.Generic;
using AlicizaX;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.UI;

[AddComponentMenu("UI/Input Glyph")]
public sealed class InputGlyph : InputGlyphBehaviourBase
{
    public enum ActionSourceMode
    {
        ActionReference,
        HotkeyTrigger,
        ActionName
    }

    public enum OutputMode
    {
        Image,
        Text
    }

    [Serializable]
    public sealed class DeviceCategoryEvent
    {
        public InputDeviceWatcher.InputDeviceCategory category;
        public UnityEvent onMatched;
        public UnityEvent onNotMatched;
    }

    [Header("Source")] [SerializeField] private ActionSourceMode actionSourceMode = ActionSourceMode.ActionReference;
    [SerializeField] private InputActionReference actionReference;
    [SerializeField] private Component hotkeyTrigger;
    [SerializeField] private string actionName;
    [SerializeField] private string compositePartName;

    [Header("Output")] [SerializeField] private OutputMode outputMode = OutputMode.Image;
    [SerializeField] private Image targetImage;
    [SerializeField] private TMP_Text targetText;

    [Header("Platform Events")] [SerializeField]
    private List<DeviceCategoryEvent> categoryEvents = new();

    private Sprite _cachedSprite;
    private string _templateText;
    private string _cachedFormattedText;
    private string _cachedReplacementToken;
    private bool _hasInvokedCategoryEvent;
    private InputDeviceWatcher.InputDeviceCategory _lastInvokedCategory;

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoAssignHotkeyTrigger();
        AutoAssignTarget();
    }
#endif

    protected override void OnEnable()
    {
        AutoAssignHotkeyTrigger();
        AutoAssignTarget();
        CacheTemplateText();
        base.OnEnable();
        InvokeCategoryEvents(true);
    }

    protected override void OnDeviceCategoryChanged(
        InputDeviceWatcher.InputDeviceCategory previousCategory,
        InputDeviceWatcher.InputDeviceCategory newCategory)
    {
        if (previousCategory == newCategory)
        {
            return;
        }

        InvokeCategoryEvents(false);
    }

    protected override void RefreshGlyph()
    {
        InputAction action = ResolveAction();
        switch (outputMode)
        {
            case OutputMode.Image:
                RefreshImage(action);
                break;
            case OutputMode.Text:
                RefreshText(action);
                break;
        }
    }

    private void RefreshImage(InputAction action)
    {
        if (targetImage == null)
        {
            return;
        }

        if (action == null)
        {
            ClearImage();
            return;
        }

        bool hasSprite = GlyphService.TryGetUISpriteForActionPath(action, compositePartName, CurrentCategory, out Sprite sprite);
        if (!hasSprite)
        {
            sprite = null;
        }

        if (_cachedSprite != sprite || targetImage.sprite != sprite)
        {
            _cachedSprite = sprite;
            targetImage.sprite = sprite;
        }
    }

    private void RefreshText(InputAction action)
    {
        if (targetText == null)
        {
            return;
        }

        CacheTemplateText();
        if (action == null)
        {
            ResetText();
            return;
        }

        string replacementToken;
        if (GlyphService.TryGetTMPTagForActionPath(action, compositePartName, CurrentCategory, out string tag, out string displayFallback))
        {
            replacementToken = tag;
        }
        else
        {
            replacementToken = displayFallback;
        }

        if (string.IsNullOrEmpty(replacementToken))
        {
            ResetText();
            return;
        }

        if (_cachedReplacementToken == replacementToken
            && !string.IsNullOrEmpty(_cachedFormattedText)
            && targetText.text == _cachedFormattedText)
        {
            return;
        }

        string formattedText = Utility.Text.Format(_templateText, replacementToken);
        if (_cachedFormattedText == formattedText && targetText.text == formattedText)
        {
            _cachedReplacementToken = replacementToken;
            return;
        }

        _cachedReplacementToken = replacementToken;
        if (_cachedFormattedText != formattedText || targetText.text != formattedText)
        {
            _cachedFormattedText = formattedText;
            targetText.text = formattedText;
        }
    }

    private InputAction ResolveAction()
    {
        switch (actionSourceMode)
        {
            case ActionSourceMode.ActionReference:
                return actionReference != null ? actionReference.action : null;
            case ActionSourceMode.HotkeyTrigger:
                return ResolveHotkeyAction();
            case ActionSourceMode.ActionName:
                return InputBindingManager.Action(actionName);
            default:
                return null;
        }
    }

    private InputAction ResolveHotkeyAction()
    {
        IHotkeyTrigger trigger = ResolveHotkeyTrigger();
        return trigger != null && trigger.HotkeyAction != null ? trigger.HotkeyAction.action : null;
    }

    private IHotkeyTrigger ResolveHotkeyTrigger()
    {
        AutoAssignHotkeyTrigger();
        return hotkeyTrigger as IHotkeyTrigger;
    }

    private void AutoAssignHotkeyTrigger()
    {
        if (actionSourceMode != ActionSourceMode.HotkeyTrigger || hotkeyTrigger != null)
        {
            return;
        }

        if (TryGetComponent(typeof(IHotkeyTrigger), out Component component))
        {
            hotkeyTrigger = component;
        }
    }

    private void AutoAssignTarget()
    {
        switch (outputMode)
        {
            case OutputMode.Image:
                if (targetImage == null)
                {
                    targetImage = GetComponent<Image>();
                }

                break;
            case OutputMode.Text:
                if (targetText == null)
                {
                    targetText = GetComponent<TMP_Text>();
                }

                break;
        }
    }

    private void CacheTemplateText()
    {
        if (targetText == null)
        {
            return;
        }

        if (string.IsNullOrEmpty(_templateText))
        {
            _templateText = targetText.text;
        }
    }

    private void ResetText()
    {
        _cachedReplacementToken = null;
        _cachedFormattedText = null;
        if (targetText != null && targetText.text != _templateText)
        {
            targetText.text = _templateText;
        }
    }

    private void ClearImage()
    {
        _cachedSprite = null;
        if (targetImage != null && targetImage.sprite != null)
        {
            targetImage.sprite = null;
        }
    }

    private void InvokeCategoryEvents(bool force)
    {
        if (!force && _hasInvokedCategoryEvent && _lastInvokedCategory == CurrentCategory)
        {
            return;
        }

        _hasInvokedCategoryEvent = true;
        _lastInvokedCategory = CurrentCategory;
        if (categoryEvents == null)
        {
            return;
        }

        for (int i = 0; i < categoryEvents.Count; i++)
        {
            DeviceCategoryEvent categoryEvent = categoryEvents[i];
            if (categoryEvent == null)
            {
                continue;
            }

            if (categoryEvent.category == CurrentCategory)
            {
                categoryEvent.onMatched?.Invoke();
            }
            else
            {
                categoryEvent.onNotMatched?.Invoke();
            }
        }
    }
}
