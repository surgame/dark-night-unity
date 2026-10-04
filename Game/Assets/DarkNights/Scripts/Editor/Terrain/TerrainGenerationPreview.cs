using System;
using System.Collections.Generic;
using System.Linq;
using AnyRules.Next.Authoring;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>正式星球预览的作者配置草稿；与航程编辑器共用冲突检查和保存，不持有第二份生成规则。</summary>
    public sealed class TerrainGenerationPreview : IDisposable
    {
        private ExpeditionFlowDraft draft;
        private readonly List<TerrainModifierDiagnostic> diagnostics = new List<TerrainModifierDiagnostic>();
        public string PlanetId { get; set; }
        public const string WorldId = "00000000000000000000000000000001";
        public ARDMapDefinition Definition { get; private set; }
        public ExpeditionFlowDraft Draft => draft;
        public PlanetPreset Selected => draft.Config.Planets?.FirstOrDefault(p => p != null && p.Id == PlanetId)
            ?? draft.Config.Planets?.FirstOrDefault(p => p != null);
        public IReadOnlyList<TerrainModifierDiagnostic> Diagnostics => diagnostics;
        public string Identity => TerrainGenerationIdentity.For(draft.Config, Selected);
        public bool HasChanges => draft.HasChanges;

        public TerrainGenerationPreview(ExpeditionFlowDraft existing = null)
        {
            var source = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset");
            Definition = source.SharedConfigs.OfType<DarkNights.Runtime.Terrain.TerrainProfileConfig>().Single().Definition;
            draft = existing;
            if (draft == null) { draft = ScriptableObject.CreateInstance<ExpeditionFlowDraft>(); draft.Load(source); }
            // HideAndDontSave 包含 NotEditable，会禁用序列化属性的输入控件。
            draft.hideFlags = HideFlags.DontSave;
            PlanetId = draft.Config.Planets?.FirstOrDefault(p => p != null && p.Enabled)?.Id ?? Selected?.Id;
        }

        public PlayableTerrain Generate()
        {
            var settings = draft.Config.FreezeCaveMap();
            diagnostics.Clear();
            var planet = Selected?.Freeze() ?? throw new InvalidOperationException("没有可预览的星球。");
            return PlanetTerrainGenerator.GenerateCandidate(planet, settings.Seed,
                WorldId, settings, pipeline: draft.Config.FreezeModifiers(), report: diagnostics.Add);
        }

        public void ObserveExternalChanges()
        {
            var current = draft.Source.SharedConfigs.OfType<ExpeditionFlowConfig>().SingleOrDefault();
            if (!draft.HasChanges && (current?.CanonicalIdentity() ?? "") != draft.BaselineJson)
            { draft.Load(draft.Source); diagnostics.Clear(); }
        }

        public void Draw(Action changed)
        {
            using var controls = new TerrainPlanetControls(this);
            controls.DrawMap(changed);
        }

        public void Apply() => draft.Apply();
        public void Cancel() { draft.Load(draft.Source); diagnostics.Clear(); }

        public void Import(ExpeditionFlowDraft legacy)
        {
            if (legacy == null || !legacy.HasChanges) return;
            if (HasChanges && draft.Config.CanonicalIdentity() != legacy.Config.CanonicalIdentity())
                throw new InvalidOperationException("Tuner 与旧航程窗口都存在不同草稿；请先处理 Tuner 草稿再转移。");
            draft.CopyFrom(legacy);
        }

        public void Dispose() => Dispose(true);

        public void Dispose(bool destroyDraft)
        {
            if (destroyDraft && draft != null) UnityEngine.Object.DestroyImmediate(draft);
            draft = null;
        }
    }
}
