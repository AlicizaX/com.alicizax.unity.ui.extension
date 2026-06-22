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
    [MenuItem("GameObject/UI/Replace Selected TextMeshPro With UXTextMeshPro", false, 2030)]
#else
    [MenuItem("GameObject/UI (Canvas)/Replace Selected TextMeshPro With UXTextMeshPro", false, 2030)]
#endif
    public static void ReplaceSelectedTextMeshProWithUXTextMeshPro()
    {
        int replaceCount = 0;
        GameObject[] selection = Selection.gameObjects;
        for (int i = 0; i < selection.Length; i++)
        {
            TextMeshProUGUI textMeshPro = selection[i].GetComponent<TextMeshProUGUI>();
            if (ReplaceTextMeshProWithUXTextMeshPro(textMeshPro))
            {
                replaceCount++;
            }
        }

        if (replaceCount == 0)
        {
            Debug.LogWarning("No ordinary TextMeshProUGUI component was selected.");
        }
    }

#if !UNITY_6000_3_OR_NEWER
    [MenuItem("GameObject/UI/Replace Selected TextMeshPro With UXTextMeshPro", true)]
#else
    [MenuItem("GameObject/UI (Canvas)/Replace Selected TextMeshPro With UXTextMeshPro", true)]
#endif
    public static bool ValidateReplaceSelectedTextMeshProWithUXTextMeshPro()
    {
        GameObject[] selection = Selection.gameObjects;
        for (int i = 0; i < selection.Length; i++)
        {
            TextMeshProUGUI textMeshPro = selection[i].GetComponent<TextMeshProUGUI>();
            if (CanReplaceTextMeshPro(textMeshPro))
            {
                return true;
            }
        }

        return false;
    }

    [MenuItem("CONTEXT/TextMeshProUGUI/Replace With UXTextMeshPro")]
    public static void ReplaceTextMeshProWithUXTextMeshPro(MenuCommand menuCommand)
    {
        ReplaceTextMeshProWithUXTextMeshPro(menuCommand.context as TextMeshProUGUI);
    }

    [MenuItem("CONTEXT/TextMeshProUGUI/Replace With UXTextMeshPro", true)]
    public static bool ValidateReplaceTextMeshProWithUXTextMeshPro(MenuCommand menuCommand)
    {
        return CanReplaceTextMeshPro(menuCommand.context as TextMeshProUGUI);
    }

    public static bool CanReplaceTextMeshPro(TextMeshProUGUI textMeshPro)
    {
        return textMeshPro != null && textMeshPro.GetType() == typeof(TextMeshProUGUI);
    }

    public static bool ReplaceTextMeshProWithUXTextMeshPro(TextMeshProUGUI textMeshPro)
    {
        if (!CanReplaceTextMeshPro(textMeshPro))
        {
            return false;
        }

        MonoScript uxTextMeshProScript = GetUXTextMeshProScript();
        if (uxTextMeshProScript == null)
        {
            Debug.LogError("Failed to find UXTextMeshPro script asset.");
            return false;
        }

        GameObject gameObject = textMeshPro.gameObject;
        string textMeshProName = textMeshPro.name;

        Undo.RegisterCompleteObjectUndo(textMeshPro, "Replace TextMeshPro With UXTextMeshPro");

        SerializedObject serializedObject = new SerializedObject(textMeshPro);
        SerializedProperty scriptProperty = serializedObject.FindProperty("m_Script");
        if (scriptProperty == null)
        {
            Debug.LogError($"Failed to replace {textMeshProName}: m_Script property was not found.");
            return false;
        }

        scriptProperty.objectReferenceValue = uxTextMeshProScript;
        serializedObject.ApplyModifiedProperties();

        UXTextMeshPro uxTextMeshPro = gameObject.GetComponent<UXTextMeshPro>();
        if (uxTextMeshPro != null)
        {
            ClearUXTextMeshProLocalization(uxTextMeshPro);
            EditorUtility.SetDirty(uxTextMeshPro);
            PrefabUtility.RecordPrefabInstancePropertyModifications(uxTextMeshPro);
        }

        return true;
    }

    public static MonoScript GetUXTextMeshProScript()
    {
        string[] guids = AssetDatabase.FindAssets($"{nameof(UXTextMeshPro)} t:MonoScript");
        for (int i = 0; i < guids.Length; i++)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[i]);
            MonoScript script = AssetDatabase.LoadAssetAtPath<MonoScript>(assetPath);
            if (script != null && script.GetClass() == typeof(UXTextMeshPro))
            {
                return script;
            }
        }

        return null;
    }

    public static void ClearUXTextMeshProLocalization(UXTextMeshPro uxTextMeshPro)
    {
        SerializedObject serializedObject = new SerializedObject(uxTextMeshPro);
        SerializedProperty localizationID = serializedObject.FindProperty("m_localizationID");
        SerializedProperty localizationKey = serializedObject.FindProperty("m_localizationKey");

        if (localizationID != null)
        {
            localizationID.intValue = 0;
        }

        if (localizationKey != null)
        {
            localizationKey.stringValue = string.Empty;
        }

        serializedObject.ApplyModifiedProperties();
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
