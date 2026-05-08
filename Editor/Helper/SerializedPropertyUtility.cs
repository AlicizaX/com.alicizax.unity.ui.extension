using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class SerializedPropertyUtility
{
    /// <summary>
    /// 获取SerializedProperty的值（返回object类型）
    /// </summary>
    public static object GetPropertyValue(SerializedProperty property)
    {
        if (property == null) return null;

        switch (property.propertyType)
        {
            case SerializedPropertyType.Integer:
                return property.intValue;
            case SerializedPropertyType.Boolean:
                return property.boolValue;
            case SerializedPropertyType.Float:
                return property.floatValue;
            case SerializedPropertyType.String:
                return property.stringValue;
            case SerializedPropertyType.Vector2:
                return property.vector2Value;
            case SerializedPropertyType.Vector3:
                return property.vector3Value;
            case SerializedPropertyType.Vector4:
                return property.vector4Value;
            case SerializedPropertyType.Quaternion:
                return property.quaternionValue;
            case SerializedPropertyType.Color:
                return property.colorValue;
            case SerializedPropertyType.ObjectReference:
                return property.objectReferenceValue;
            case SerializedPropertyType.Enum:
                return property.enumValueIndex;
            case SerializedPropertyType.Vector2Int:
                return property.vector2IntValue;
            case SerializedPropertyType.Vector3Int:
                return property.vector3IntValue;
            case SerializedPropertyType.Rect:
                return property.rectValue;
            case SerializedPropertyType.RectInt:
                return property.rectIntValue;
            case SerializedPropertyType.Bounds:
                return property.boundsValue;
            case SerializedPropertyType.BoundsInt:
                return property.boundsIntValue;
            case SerializedPropertyType.AnimationCurve:
                return property.animationCurveValue;
            case SerializedPropertyType.Generic:
            default:
                // 对于不支持的类型或复杂类型，返回null或尝试其他处理
                Debug.LogWarning($"Unsupported property type: {property.propertyType}");
                return null;
        }
    }

    /// <summary>
    /// 获取SerializedProperty的值（泛型版本）
    /// </summary>
    public static T GetPropertyValue<T>(SerializedProperty property, T defaultValue = default(T))
    {
        try
        {
            object value = GetPropertyValue(property);
            if (value == null) return defaultValue;

            // 如果类型匹配，直接返回
            if (value is T typedValue)
                return typedValue;

            // 尝试类型转换
            return (T)Convert.ChangeType(value, typeof(T));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"Failed to get property value as type {typeof(T).Name}: {e.Message}");
            return defaultValue;
        }
    }

    /// <summary>
    /// 安全地获取属性值，返回是否成功
    /// </summary>
    public static bool TryGetPropertyValue<T>(SerializedProperty property, out T value, T defaultValue = default(T))
    {
        value = defaultValue;

        try
        {
            value = GetPropertyValue<T>(property, defaultValue);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
