using System;
using System.Linq;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>
    /// 航程面板的临时序列化草稿；属性编辑和行操作均记录 Undo，只有显式应用才写正式会话定义。
    /// 保留打开时的原配置文本，防止覆盖其他 Inspector 或工具随后修改的作者配置。
    /// </summary>
    public sealed class ExpeditionFlowDraft : ScriptableObject
    {
        public ExpeditionFlowConfig Config = new ExpeditionFlowConfig();
        [SerializeField] private string baselineJson;
        [SerializeField] private string draftBaselineJson;
        [SerializeField] private ObjectDefinition source;
        internal string BaselineJson => baselineJson;
        internal ObjectDefinition Source => source;
        internal bool HasChanges => JsonUtility.ToJson(Config) != draftBaselineJson;

        internal void Load(ObjectDefinition source)
        {
            this.source = source;
            var current = Read(source);
            baselineJson = current == null ? "" : JsonUtility.ToJson(current);
            Config = current == null ? new ExpeditionFlowConfig() : Clone(current);
            draftBaselineJson = JsonUtility.ToJson(Config);
        }

        internal void Apply()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Source == null)
                throw new InvalidOperationException("仅允许在编辑态应用到正式会话定义。");
            Config.Validate();
            var current = Read(Source);
            string currentJson = current == null ? "" : JsonUtility.ToJson(current);
            if (currentJson != BaselineJson)
                throw new InvalidOperationException("正式配置已被其他编辑操作修改；请取消草稿后重新编辑，避免覆盖。");
            Undo.RecordObject(Source, "应用星球航程配置");
            var copy = Clone(Config);
            if (current == null) Source.SharedConfigs.Add(copy);
            else Source.SharedConfigs[Source.SharedConfigs.IndexOf(current)] = copy;
            EditorUtility.SetDirty(Source);
            AssetDatabase.SaveAssetIfDirty(Source);
            baselineJson = JsonUtility.ToJson(copy);
            draftBaselineJson = baselineJson;
        }

        internal static T Clone<T>(T value) => JsonUtility.FromJson<T>(JsonUtility.ToJson(value));

        private static ExpeditionFlowConfig Read(ObjectDefinition source)
        {
            if (source == null) throw new InvalidOperationException("找不到正式 WorldSession ObjectDefinition。");
            var configs = source.SharedConfigs.OfType<ExpeditionFlowConfig>().ToArray();
            if (configs.Length > 1) throw new InvalidOperationException("同一会话定义只能有一份航程共享配置。");
            return configs.SingleOrDefault();
        }
    }
}
