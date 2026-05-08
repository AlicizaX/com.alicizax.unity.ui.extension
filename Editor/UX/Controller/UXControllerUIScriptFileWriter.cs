using System;
using System.IO;
using System.Text;
using AlicizaX.UI.Editor;
using UnityEditor;

namespace UnityEngine.UI
{
    public sealed class UXControllerUIScriptFileWriter : IUIScriptFileWriter
    {
        public void Write(GameObject targetObject, string className, string scriptContent, UIScriptGenerateData scriptGenerateData)
        {
            if (string.IsNullOrEmpty(className)) throw new ArgumentNullException(nameof(className));
            if (scriptContent == null) throw new ArgumentNullException(nameof(scriptContent));
            if (scriptGenerateData == null) throw new ArgumentNullException(nameof(scriptGenerateData));

            var scriptFolderPath = scriptGenerateData.GenerateHolderCodePath;
            var scriptFilePath = Path.Combine(scriptFolderPath, $"{className}.cs");

            Directory.CreateDirectory(scriptFolderPath);
            scriptContent = scriptContent.Replace("#Controller#", GetControllerContent(targetObject));

            if (File.Exists(scriptFilePath) && IsContentUnchanged(scriptFilePath, scriptContent))
            {
                UIScriptGeneratorHelper.BindUIScript();
                return;
            }

            File.WriteAllText(scriptFilePath, scriptContent, Encoding.UTF8);
            AssetDatabase.Refresh();
        }

        private static bool IsContentUnchanged(string filePath, string newContent)
        {
            var oldText = File.ReadAllText(filePath, Encoding.UTF8);
            return oldText.Equals(newContent, StringComparison.Ordinal);
        }

        private static string GetControllerContent(GameObject targetObject)
        {
            UXController controller = targetObject.GetComponent<UXController>();
            if (controller == null || controller.Controllers.Count == 0) return string.Empty;

            StringBuilder sb = new StringBuilder();
            foreach (var ctl in controller.Controllers)
            {
                string varibleName = ctl.Name.Substring(0, 1).ToUpper() + ctl.Name.Substring(1);
                sb.AppendLine($"\t\tpublic UXController.ControllerDefinition {varibleName} {{ get; private set; }}");
            }

            sb.AppendLine("\t\tpublic override void Awake()");
            sb.AppendLine("\t\t{");
            sb.AppendLine("\t\t\tbase.Awake();");
            sb.AppendLine("\t\t\tvar ctl = gameObject.GetComponent<UXController>();");

            foreach (var ctl in controller.Controllers)
            {
                string varibleName = ctl.Name.Substring(0, 1).ToUpper() + ctl.Name.Substring(1);
                sb.AppendLine($"\t\t\t{varibleName} = ctl.GetControllerByName(\"{ctl.Name}\");");
            }

            sb.AppendLine("\t\t}");
            return sb.ToString();
        }
    }
}
