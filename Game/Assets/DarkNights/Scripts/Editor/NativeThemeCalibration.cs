using System;
using System.IO;
using DarkNights.View;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace DarkNights.Editor
{
    /// <summary>
    /// 显式修正首版 UI 遗漏的主题圆角及按钮状态；仅编辑冻结输入中有背景样式的既有控件。
    /// 全批预检后保留 Image／Button／GUID 与布局，已经校准的资源拒绝覆盖，后续由人工维护。
    /// </summary>
    public static class NativeThemeCalibration
    {
        [MenuItem("Dark Nights/Content/Calibrate Native UI Theme Once")]
        public static void Apply()
        {
            var pages = JObject.Parse(File.ReadAllText(NativeUiSetup.InputPath))["profiles"][0]["pages"];
            foreach (JObject page in pages)
            {
                GameObject asset = AssetDatabase.LoadAssetAtPath<GameObject>(PathFor(page));
                if (asset == null || asset.GetComponentsInChildren<NativePanelTheme>(true).Length != 0)
                    throw new InvalidOperationException("Missing or already calibrated panel: " + PathFor(page));
                foreach (JObject row in page["nodes"])
                    if (Style(row) != null && Target(asset, row).GetComponent<Image>() == null)
                        throw new InvalidOperationException("Expected existing Image: " + row["path"]);
            }
            int count = 0;
            foreach (JObject page in pages)
            {
                string path = PathFor(page);
                GameObject root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    foreach (JObject row in page["nodes"])
                    {
                        JToken style = Style(row);
                        if (style == null) continue;
                        GameObject target = Target(root, row);
                        var outline = target.GetComponent<Outline>();
                        if (outline != null) UnityEngine.Object.DestroyImmediate(outline);
                        NativePanelStyle normal = Parse(style);
                        JToken styles = row["styles"];
                        target.AddComponent<NativePanelTheme>().Configure(target.GetComponent<Button>(), normal,
                            Parse(styles["hover"] ?? style), Parse(styles["pressed"] ?? style), Parse(styles["disabled"] ?? style));
                        count++;
                    }
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            AssetDatabase.SaveAssets();
            Debug.Log("DARK_NIGHTS_THEME_CALIBRATED panels=5 controls=" + count);
        }

        private static string PathFor(JObject page) => NativeUiSetup.Root + "/" + page["name"] + "/" + page["name"] + ".prefab";
        private static GameObject Target(GameObject root, JObject row) => (string)row["path"] == "." ? root : root.transform.Find((string)row["path"]).gameObject;
        private static JToken Style(JObject row) => row["styles"]["normal"] ?? row["styles"]["panel"] ?? row["styles"]["background"];
        private static NativePanelStyle Parse(JToken style) => new NativePanelStyle
        {
            Background = ColorOf(style["background"]), Border = ColorOf(style["border"]),
            BorderWidth = (float)style["borderWidth"], Radius = (float)style["radius"]
        };
        private static Color ColorOf(JToken value) => new Color((float)value[0], (float)value[1], (float)value[2], (float)value[3]);
    }
}
