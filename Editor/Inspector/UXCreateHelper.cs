using System;
using System.Reflection;
#if TEXTMESHPRO_SUPPORT
using TMPro;
using TMPro.EditorUtilities;
#endif
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Random = UnityEngine.Random;


public class UXCreateHelper : Editor
{
    static object InvokeMethod(Type type, string methodName, object[] parameters = null)
    {
        if (parameters == null)
        {
            return type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static).Invoke(null, null);
        }

        Type[] types = new Type[parameters.Length];
        for (int i = 0; i < parameters.Length; i++)
        {
            types[i] = parameters[i].GetType();
        }

        return type.GetMethod(methodName, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null, types, null).Invoke(null, parameters);
    }
#if !UNITY_6000_3_OR_NEWER
    [MenuItem("GameObject/UI/UXImage")]
#else
    [MenuItem("GameObject/UI (Canvas)/UXImage")]
#endif
    public static void CreateUXImage(MenuCommand menuCommand)
    {
        Type MenuOptionsType = typeof(UnityEditor.UI.ImageEditor).Assembly.GetType("UnityEditor.UI.MenuOptions");
        InvokeMethod(MenuOptionsType, "AddImage", new object[] { menuCommand });
        GameObject obj = Selection.activeGameObject;
        obj.name = "UXImage";
        DestroyImmediate(obj.GetComponent<Image>());
        obj.AddComponent<UXImage>();
    }

#if !UNITY_6000_3_OR_NEWER
    [MenuItem("GameObject/UI/UXToggle")]
#else
    [MenuItem("GameObject/UI (Canvas)/UXToggle")]
#endif
    public static void CreateUXToggle(MenuCommand menuCommand)
    {
        Type MenuOptionsType = typeof(UnityEditor.UI.SliderEditor).Assembly.GetType("UnityEditor.UI.MenuOptions");
        InvokeMethod(MenuOptionsType, "AddToggle", new object[] { menuCommand });
        GameObject obj = Selection.activeGameObject;
        obj.name = "UXToggle";
        Toggle toggle = obj.GetComponent<Toggle>();
        Graphic toggleGraphic = toggle != null ? toggle.graphic : null;
        bool isOn = toggle != null && toggle.isOn;
        if (toggle != null)
            DestroyImmediate(toggle);

        var uxToggle = obj.AddComponent<UXToggle>();
        if (toggleGraphic == null)
            toggleGraphic = obj.transform.Find("Background/Checkmark")?.GetComponent<Graphic>();

        uxToggle.graphic = toggleGraphic;
        uxToggle.isOn = isOn;
        uxToggle.targetGraphic = obj.transform.Find("Background")?.GetComponent<Graphic>();
    }

#if TEXTMESHPRO_SUPPORT

#if !UNITY_6000_3_OR_NEWER
    [MenuItem("GameObject/UI/UXButton")]
#else
    [MenuItem("GameObject/UI (Canvas)/UXButton")]
#endif
    public static void CreateUXButton(MenuCommand menuCommand)
    {
        Type MenuOptionsType = typeof(TMPro_CreateObjectMenu).Assembly.GetType("TMPro.EditorUtilities.TMPro_CreateObjectMenu");
        InvokeMethod(MenuOptionsType, "AddButton", new object[] { menuCommand });
        GameObject obj = Selection.activeGameObject;
        obj.name = "UXButton";
        DestroyImmediate(obj.GetComponent<Button>());
        obj.AddComponent<UXButton>();
    }

#endif

#if !UNITY_6000_3_OR_NEWER
    [MenuItem("GameObject/UI/UXScrollView")]
#else
    [MenuItem("GameObject/UI (Canvas)/UXScrollView")]
#endif
    public static void CreateUxRecyclerView()
    {
        GameObject selectionObject = Selection.activeGameObject;
        Transform parent = selectionObject != null ? selectionObject.transform : PrefabStageUtility.GetCurrentPrefabStage().prefabContentsRoot.transform;
        const string prefabPath = "Packages/com.alicizax.unity.ui.extension/Editor/RecyclerView/Res/ScrollView.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
        Undo.RegisterCreatedObjectUndo(instance, "Create UXScrollView");
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.UserAction);
        instance.name = prefab.name;
        Selection.activeGameObject = instance;
    }

#if !UNITY_6000_3_OR_NEWER
    [MenuItem("GameObject/UI/UXTemplateWindow")]
#else
    [MenuItem("GameObject/UI (Canvas)/UXTemplateWindow")]
#endif
    private static void CreateTemplateWindow()
    {
        GameObject selectionObject = Selection.activeGameObject;
        if (selectionObject == null) return;
        const string prefabPath = "Packages/com.alicizax.unity.ui.extension/Editor/Res/Template/UITemplateWindow.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, selectionObject.transform);
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.UserAction);
        Selection.activeGameObject = instance;
    }
}
