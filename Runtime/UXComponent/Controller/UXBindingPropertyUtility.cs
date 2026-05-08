using System;
using System.Collections.Generic;
using TMPro;

namespace UnityEngine.UI
{
    public sealed class UXBindingPropertyMetadata
    {
        public UXBindingPropertyMetadata(
            UXBindingProperty property,
            string displayName,
            UXBindingValueKind valueKind,
            Type objectReferenceType)
        {
            Property = property;
            DisplayName = displayName;
            ValueKind = valueKind;
            ObjectReferenceType = objectReferenceType;
        }

        public UXBindingProperty Property { get; }
        public string DisplayName { get; }
        public UXBindingValueKind ValueKind { get; }
        public Type ObjectReferenceType { get; }
    }

    public struct UXBindingResolvedTarget
    {
        public GameObject GameObject;
        public Transform Transform;
        public CanvasGroup CanvasGroup;
        public Graphic Graphic;
        public Image Image;
        public Text Text;
        public TextMeshProUGUI TmpText;
        public RectTransform RectTransform;
    }

    public static class UXBindingPropertyUtility
    {
        private static readonly UXBindingPropertyMetadata[] Metadata =
        {
            new UXBindingPropertyMetadata(UXBindingProperty.GameObjectActive, "GameObject/Active", UXBindingValueKind.Boolean, null),
            new UXBindingPropertyMetadata(UXBindingProperty.CanvasGroupAlpha, "CanvasGroup/Alpha", UXBindingValueKind.Float, null),
            new UXBindingPropertyMetadata(UXBindingProperty.CanvasGroupInteractable, "CanvasGroup/Interactable", UXBindingValueKind.Boolean, null),
            new UXBindingPropertyMetadata(UXBindingProperty.CanvasGroupBlocksRaycasts, "CanvasGroup/Blocks Raycasts", UXBindingValueKind.Boolean, null),
            new UXBindingPropertyMetadata(UXBindingProperty.GraphicColor, "Graphic/Color", UXBindingValueKind.Color, null),
            new UXBindingPropertyMetadata(UXBindingProperty.GraphicMaterial, "Graphic/Material", UXBindingValueKind.ObjectReference, typeof(Material)),
            new UXBindingPropertyMetadata(UXBindingProperty.ImageSprite, "Image/Sprite", UXBindingValueKind.ObjectReference, typeof(Sprite)),
            new UXBindingPropertyMetadata(UXBindingProperty.TextContent, "Text/Content", UXBindingValueKind.String, null),
            new UXBindingPropertyMetadata(UXBindingProperty.TextColor, "Text/Color", UXBindingValueKind.Color, null),
            new UXBindingPropertyMetadata(UXBindingProperty.RectTransformAnchoredPosition, "RectTransform/Anchored Position", UXBindingValueKind.Vector2, null),
            new UXBindingPropertyMetadata(UXBindingProperty.TransformLocalScale, "Transform/Local Scale", UXBindingValueKind.Vector3, null),
            new UXBindingPropertyMetadata(UXBindingProperty.TransformLocalEulerAngles, "Transform/Local Rotation", UXBindingValueKind.Vector3, null)
        };

        public static IReadOnlyList<UXBindingPropertyMetadata> AllMetadata => Metadata;

        public static UXBindingPropertyMetadata GetMetadata(UXBindingProperty property)
        {
            int index = (int)property;
            if ((uint)index < (uint)Metadata.Length && Metadata[index].Property == property)
            {
                return Metadata[index];
            }

            for (int i = 0; i < Metadata.Length; i++)
            {
                if (Metadata[i].Property == property)
                {
                    return Metadata[i];
                }
            }

            return Metadata[0];
        }

        public static bool Resolve(GameObject target, UXBindingProperty property, out UXBindingResolvedTarget resolvedTarget)
        {
            resolvedTarget = default;
            if (target == null)
            {
                return false;
            }

            resolvedTarget.GameObject = target;
            resolvedTarget.Transform = target.transform;

            switch (property)
            {
                case UXBindingProperty.GameObjectActive:
                case UXBindingProperty.TransformLocalScale:
                case UXBindingProperty.TransformLocalEulerAngles:
                    return true;
                case UXBindingProperty.CanvasGroupAlpha:
                case UXBindingProperty.CanvasGroupInteractable:
                case UXBindingProperty.CanvasGroupBlocksRaycasts:
                    return target.TryGetComponent(out resolvedTarget.CanvasGroup);
                case UXBindingProperty.GraphicColor:
                case UXBindingProperty.GraphicMaterial:
                    return target.TryGetComponent(out resolvedTarget.Graphic);
                case UXBindingProperty.ImageSprite:
                    return target.TryGetComponent(out resolvedTarget.Image);
                case UXBindingProperty.TextContent:
                case UXBindingProperty.TextColor:
                    if (target.TryGetComponent(out resolvedTarget.Text))
                    {
                        return true;
                    }

                    return target.TryGetComponent(out resolvedTarget.TmpText);
                case UXBindingProperty.RectTransformAnchoredPosition:
                    return target.TryGetComponent(out resolvedTarget.RectTransform);
                default:
                    return false;
            }
        }

        public static bool IsSupported(GameObject target, UXBindingProperty property)
        {
            return Resolve(target, property, out _);
        }

        public static void GetSupportedProperties(GameObject target, List<UXBindingProperty> output)
        {
            output.Clear();
            if (target == null)
            {
                return;
            }

            for (int i = 0; i < Metadata.Length; i++)
            {
                UXBindingProperty property = Metadata[i].Property;
                if (IsSupported(target, property))
                {
                    output.Add(property);
                }
            }
        }

        public static bool CaptureValue(GameObject target, UXBindingProperty property, UXBindingValue destination)
        {
            if (!Resolve(target, property, out UXBindingResolvedTarget resolvedTarget))
            {
                return false;
            }

            return CaptureValue(in resolvedTarget, property, destination);
        }

        public static bool CaptureValue(in UXBindingResolvedTarget target, UXBindingProperty property, UXBindingValue destination)
        {
            if (destination == null || target.GameObject == null)
            {
                return false;
            }

            switch (property)
            {
                case UXBindingProperty.GameObjectActive:
                    destination.BoolValue = target.GameObject.activeSelf;
                    return true;
                case UXBindingProperty.CanvasGroupAlpha:
                    if (target.CanvasGroup == null) return false;
                    destination.FloatValue = target.CanvasGroup.alpha;
                    return true;
                case UXBindingProperty.CanvasGroupInteractable:
                    if (target.CanvasGroup == null) return false;
                    destination.BoolValue = target.CanvasGroup.interactable;
                    return true;
                case UXBindingProperty.CanvasGroupBlocksRaycasts:
                    if (target.CanvasGroup == null) return false;
                    destination.BoolValue = target.CanvasGroup.blocksRaycasts;
                    return true;
                case UXBindingProperty.GraphicColor:
                    if (target.Graphic == null) return false;
                    destination.ColorValue = target.Graphic.color;
                    return true;
                case UXBindingProperty.GraphicMaterial:
                    if (target.Graphic == null) return false;
                    destination.ObjectValue = target.Graphic.defaultMaterial;
                    return true;
                case UXBindingProperty.ImageSprite:
                    if (target.Image == null) return false;
                    destination.ObjectValue = target.Image.sprite;
                    return true;
                case UXBindingProperty.TextContent:
                    if (target.Text != null)
                    {
                        destination.StringValue = target.Text.text;
                        return true;
                    }

                    if (target.TmpText != null)
                    {
                        destination.StringValue = target.TmpText.text;
                        return true;
                    }

                    return false;
                case UXBindingProperty.TextColor:
                    if (target.Text != null)
                    {
                        destination.ColorValue = target.Text.color;
                        return true;
                    }

                    if (target.TmpText != null)
                    {
                        destination.ColorValue = target.TmpText.color;
                        return true;
                    }

                    return false;
                case UXBindingProperty.RectTransformAnchoredPosition:
                    if (target.RectTransform == null) return false;
                    destination.Vector2Value = target.RectTransform.anchoredPosition;
                    return true;
                case UXBindingProperty.TransformLocalScale:
                    if (target.Transform == null) return false;
                    destination.Vector3Value = target.Transform.localScale;
                    return true;
                case UXBindingProperty.TransformLocalEulerAngles:
                    if (target.Transform == null) return false;
                    destination.Vector3Value = target.Transform.localEulerAngles;
                    return true;
                default:
                    return false;
            }
        }

        public static bool ApplyValue(GameObject target, UXBindingProperty property, UXBindingValue value)
        {
            if (!Resolve(target, property, out UXBindingResolvedTarget resolvedTarget))
            {
                return false;
            }

            return ApplyValue(in resolvedTarget, property, value);
        }

        public static bool ApplyValue(in UXBindingResolvedTarget target, UXBindingProperty property, UXBindingValue value)
        {
            if (value == null || target.GameObject == null)
            {
                return false;
            }

            switch (property)
            {
                case UXBindingProperty.GameObjectActive:
                    target.GameObject.SetActive(value.BoolValue);
                    return true;
                case UXBindingProperty.CanvasGroupAlpha:
                    if (target.CanvasGroup == null) return false;
                    target.CanvasGroup.alpha = value.FloatValue;
                    return true;
                case UXBindingProperty.CanvasGroupInteractable:
                    if (target.CanvasGroup == null) return false;
                    target.CanvasGroup.interactable = value.BoolValue;
                    return true;
                case UXBindingProperty.CanvasGroupBlocksRaycasts:
                    if (target.CanvasGroup == null) return false;
                    target.CanvasGroup.blocksRaycasts = value.BoolValue;
                    return true;
                case UXBindingProperty.GraphicColor:
                    if (target.Graphic == null) return false;
                    target.Graphic.color = value.ColorValue;
                    return true;
                case UXBindingProperty.GraphicMaterial:
                    if (target.Graphic == null) return false;
                    target.Graphic.material = value.ObjectValue as Material;
                    return true;
                case UXBindingProperty.ImageSprite:
                    if (target.Image == null) return false;
                    target.Image.sprite = value.ObjectValue as Sprite;
                    return true;
                case UXBindingProperty.TextContent:
                    if (target.Text != null)
                    {
                        target.Text.text = value.StringValue;
                        return true;
                    }

                    if (target.TmpText != null)
                    {
                        target.TmpText.text = value.StringValue;
                        return true;
                    }

                    return false;
                case UXBindingProperty.TextColor:
                    if (target.Text != null)
                    {
                        target.Text.color = value.ColorValue;
                        return true;
                    }

                    if (target.TmpText != null)
                    {
                        target.TmpText.color = value.ColorValue;
                        return true;
                    }

                    return false;
                case UXBindingProperty.RectTransformAnchoredPosition:
                    if (target.RectTransform == null) return false;
                    target.RectTransform.anchoredPosition = value.Vector2Value;
                    return true;
                case UXBindingProperty.TransformLocalScale:
                    if (target.Transform == null) return false;
                    target.Transform.localScale = value.Vector3Value;
                    return true;
                case UXBindingProperty.TransformLocalEulerAngles:
                    if (target.Transform == null) return false;
                    target.Transform.localEulerAngles = value.Vector3Value;
                    return true;
                default:
                    return false;
            }
        }
    }
}
