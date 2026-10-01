using System;
using AnyRules.Next.Authoring;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>原生材质耐久的作者草稿；编辑不写资产，应用前检查源版本，Gameplay 绑定存在时是唯一耐久来源。</summary>
    internal sealed class TerrainDurabilityDraft
    {
        internal TerrainDefinition Source { get; }
        internal GameplayDefinition Gameplay { get; }
        internal int HitPoints, Hardness, Stages;
        private readonly string baseline;
        internal UnityEngine.Object Target => Gameplay != null ? (UnityEngine.Object)Gameplay : Source;
        internal TerrainDurabilityDraft(TerrainDefinition source)
        {
            Source = source; Gameplay = source.Gameplay;
            HitPoints = Gameplay != null ? Gameplay.MaximumDurability : source.MaximumDurability;
            Hardness = Gameplay != null ? Gameplay.Hardness : 1;
            Stages = Gameplay != null ? Gameplay.DamageVisualStages : 0;
            baseline = JsonUtility.ToJson(Target);
        }
        internal void RequireUnchanged()
        {
            if (Source.Gameplay != Gameplay || JsonUtility.ToJson(Target) != baseline)
                throw new InvalidOperationException("材质来源已被其他编辑修改，请重新加载草稿。");
            if (HitPoints < 1 || Source.Key != "bedrock" && HitPoints > 1000000 ||
                Hardness < 0 || Hardness > 1000000 || Stages < 0 || Stages > 16)
                throw new InvalidOperationException("耐久、硬度或受损阶段超出允许范围。");
        }
        internal void Apply()
        {
            Undo.RecordObject(Target, "应用材质耐久");
            if (Gameplay != null)
            {
                Gameplay.MaximumDurability = HitPoints; Gameplay.Hardness = Hardness; Gameplay.DamageVisualStages = Stages;
            }
            else Source.MaximumDurability = HitPoints;
            EditorUtility.SetDirty(Target);
        }
    }
}
