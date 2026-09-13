using System;
using System.Linq;
using DarkNights.Runtime.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Sirenix.OdinInspector.Editor;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// Workshop 与 Definition Inspector 共用的规则字段绘制器，从原 balance.json 提供分类选择。
    /// 数值只读展示，选择仅写 RuleKey；按内容哈希更新缓存，不建立第二份人工配置来源。
    /// </summary>
    public sealed class RuleKeyDrawer : OdinAttributeDrawer<RuleKeyAttribute, string>
    {
        public const string RulesPath = "Assets/DarkNights/Res/Config/balance.json";
        private static JObject rules;
        private static Hash128 hash;

        public static JObject Family(string name)
        {
            Hash128 current = AssetDatabase.GetAssetDependencyHash(RulesPath);
            if (rules == null || current != hash)
            {
                TextAsset asset = AssetDatabase.LoadAssetAtPath<TextAsset>(RulesPath);
                if (asset == null) throw new InvalidOperationException("找不到规则目录：" + RulesPath);
                rules = JObject.Parse(asset.text);
                hash = current;
            }
            return rules[name] as JObject ?? throw new InvalidOperationException("未知规则类别：" + name);
        }

        protected override void DrawPropertyLayout(GUIContent label)
        {
            try
            {
                JObject family = Family(Attribute.Family);
                string[] keys = family.Properties().Select(p => p.Name).ToArray();
                string[] choices = keys.Select(key => (string)family[key]["name"] + "  (" + key + ")").ToArray();
                int selected = Array.IndexOf(keys, ValueEntry.SmartValue);
                int changed = EditorGUILayout.Popup(label ?? new GUIContent("规则"), selected, choices);
                if (changed >= 0 && changed != selected) ValueEntry.SmartValue = keys[changed];
                if (!family.TryGetValue(ValueEntry.SmartValue ?? "", out JToken entry))
                    EditorGUILayout.HelpBox("请选择此对象家族的有效规则。", MessageType.Error);
                else
                {
                    EditorGUILayout.LabelField("规则数值（只读，来自 balance.json）", EditorStyles.miniBoldLabel);
                    using (new EditorGUI.DisabledScope(true))
                        foreach (JProperty field in ((JObject)entry).Properties())
                            EditorGUILayout.TextField(field.Name, field.Value.Type == JTokenType.String
                                ? (string)field.Value : field.Value.ToString(Formatting.None));
                }
            }
            catch (Exception error) { EditorGUILayout.HelpBox(error.Message, MessageType.Error); }
        }
    }
}
