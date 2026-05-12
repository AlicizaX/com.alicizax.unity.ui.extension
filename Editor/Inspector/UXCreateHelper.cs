using System;
using System.Reflection;
using TMPro;
using TMPro.EditorUtilities;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.UI;
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
        var image = obj.AddComponent<UXImage>();
        image.material = AssetDatabase.LoadAssetAtPath<Material>(UXGUIConfig.UIDefaultMatPath);
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
        DestroyImmediate(obj.GetComponent<Toggle>());
        var uxToggle = obj.AddComponent<UXToggle>();
        uxToggle.targetGraphic = obj.transform.Find("Background/Checkmark").GetComponent<Graphic>();
        uxToggle.targetGraphic = obj.transform.Find("Background").GetComponent<Graphic>();
    }

#if TEXTMESHPRO_SUPPORT

#if !UNITY_6000_3_OR_NEWER
    [MenuItem("GameObject/UI/UXTextMeshPro")]
#else
    [MenuItem("GameObject/UI (Canvas)/UXTextMeshPro")]
#endif
    public static void CreateUXTextMeshPro(MenuCommand menuCommand)
    {
        Type MenuOptionsType = typeof(ImageEditor).Assembly.GetType("UnityEditor.UI.MenuOptions");
        InvokeMethod(MenuOptionsType, "AddText", new object[] { menuCommand });
        GameObject obj = Selection.activeGameObject;
        obj.name = "UXTextMeshPro";
        DestroyImmediate(obj.GetComponent<Text>());
        obj.AddComponent<UXTextMeshPro>();
    }

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

#if !UNITY_6000_3_OR_NEWER
    [MenuItem("GameObject/UI/UXInput Field")]
#else
    [MenuItem("GameObject/UI (Canvas)/UXInput Field")]
#endif
    public static void CreateUXInputField(MenuCommand menuCommand)
    {
        Type MenuOptionsType = typeof(TMPro_CreateObjectMenu).Assembly.GetType("TMPro.EditorUtilities.TMPro_CreateObjectMenu");
        InvokeMethod(MenuOptionsType, "AddTextMeshProInputField", new object[] { menuCommand });
        GameObject obj = Selection.activeGameObject;
        obj.name = "UXInputField";
        TMP_InputField inputField = obj.GetComponent<TMP_InputField>();
        TextMeshProUGUI oldTextField = inputField.placeholder.GetComponent<TextMeshProUGUI>();
        GameObject placeholderGameObject = inputField.placeholder.gameObject;
        float oldFontSize = oldTextField.fontSize;
        Color oldColor = oldTextField.color;
        string oldText = oldTextField.text;
        DestroyImmediate(oldTextField);
        TextMeshProUGUI newTextField = placeholderGameObject.AddComponent<TextMeshProUGUI>();
        newTextField.fontSize = oldFontSize;
        newTextField.color = oldColor;
        newTextField.text = oldText;
        inputField.placeholder = newTextField;
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
        PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.UserAction);
        instance.name = prefab.name + Random.Range(1, 1000);
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
