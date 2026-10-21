using System;
using System.Collections.Generic;
using System.Linq;
using AnyRules.Next.Authoring;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
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
        private readonly List<string> contentDiagnostics = new List<string>();
        public IReadOnlyList<string> ContentDiagnostics => contentDiagnostics;
        public CaveRulePlan RulePlan { get; private set; }
        public bool RuleMode => draft.Config.CaveMap?.Rules?.Enabled == true;
        public int GenerationRequest { get; private set; }
        public bool ExplicitGeneration => RuleMode && GenerationRequest == 0;
        public void RequestGeneration() { JointDraft = true; GenerationRequest++; }
        public string PlanetId { get; set; }
        public const string WorldId = "00000000000000000000000000000001";
        public ARDMapDefinition Definition { get; private set; }
        public ExpeditionFlowDraft Draft => draft;
        public PlanetPreset Selected => draft.Config.Planets?.FirstOrDefault(p => p != null && p.Id == PlanetId)
            ?? draft.Config.Planets?.FirstOrDefault(p => p != null);
        public IReadOnlyList<TerrainModifierDiagnostic> Diagnostics => diagnostics;
        public string Identity => TerrainGenerationIdentity.For(draft.Config, Selected) + "|joint-preview:" + JointDraft +
            (JointDraft && !RuleMode ? "|" + TerrainVeinStudySettings.instance.JointVeinCount + "|" + TerrainVeinStudySettings.instance.JointNests : "");
        public bool HasChanges => draft.HasChanges;
        public ExpeditionFlowConfig InstalledConfig => draft.Source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
        public bool CandidateFresh => JointDraft && LastCandidate != null && LastCandidateIdentity == Identity;
        public PlayableTerrain LastCandidate { get; private set; }
        public string LastCandidateIdentity { get; private set; }
        public bool JointDraft { get; set; }
        public string GeologyConflict { get; private set; } = "";

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
            contentDiagnostics.Clear(); RulePlan = null;
            var planet = Selected?.Freeze() ?? throw new InvalidOperationException("没有可预览的星球。");
            PlayableTerrain candidate;
            GeologyConflict = "";
            if (JointDraft)
            {
                var tools = TerrainVeinStudySettings.instance;
                candidate = JointMineContentSetup.Build(draft.Config, tools.JointVeinCount, tools.JointNests, contentDiagnostics.Add);
                try { candidate = DarkNights.Runtime.Terrain.GeologyMapDesign.Attach(candidate, draft.Config.Geology); }
                catch (Exception error) { GeologyConflict = error.Message; }
            }
            else
            {
                if (draft.Config.ReviewedMap != null) draft.Config.ReviewedMap.VerifySource(draft.Config);
                candidate = draft.Config.ReviewedMap != null ? draft.Config.ReviewedMap.Freeze(WorldId) : PlanetTerrainGenerator.GenerateCandidate(planet, settings.Seed,
                    WorldId, settings, pipeline: draft.Config.FreezeModifiers(), report: diagnostics.Add, contentReport: contentDiagnostics.Add);
            }
            if (RuleMode && (JointDraft || draft.Config.ReviewedMap == null))
            { var actual = settings.CopyValidated(); actual.Seed = candidate.Seed; RulePlan = CaveRulePlanner.Build(actual); }
            if (RulePlan != null) contentDiagnostics.AddRange(RulePlan.Diagnostics);
            GenerationRequest = 0;
            LastCandidate = candidate; LastCandidateIdentity = Identity;
            return candidate;
        }

        public string ExportBlockReason()
        {
            if (HasChanges) return "作者草稿未保存；先保存地图草稿。";
            if (EditorUtility.IsDirty(draft.Source)) return "正式配置存在其他未保存修改；先处理该编辑器草稿。";
            if (InstalledConfig.CanonicalIdentity() != draft.BaselineJson) return "正式来源在窗口外变化；先同步或取消本窗口草稿。";
            if (!CandidateFresh) return "没有与当前草稿一致的候选；先更新候选。";
            if (GeologyConflict.Length != 0) return "塌方静态空间冲突：" + GeologyConflict;
            return "";
        }

        /// <summary>检查当前候选的静态数据与序列化合同，不执行人物通行或任何运行验收。</summary>
        public void CheckCandidate()
        {
            string reason = ExportBlockReason();
            if (reason.Length != 0) throw new InvalidOperationException(reason);
            draft.Config.Validate();
            ReviewedMineMap.Encode(LastCandidate, Recipe());
            foreach (var site in LastCandidate.GeologySites)
            {
                reason = GeologySiteValidation.Check(LastCandidate, site);
                if (reason.Length != 0) throw new InvalidOperationException(reason);
            }
        }

        public ReviewedMineMap ExportJoint(bool install = true)
        {
            CheckCandidate();
            return JointMineContentSetup.Create(LastCandidate, ReviewedMineMap.SourceFingerprint(draft.Config), Recipe(), install,
                draft.Source, JointMineContentSetup.VersionRoot);
        }

        private string Recipe()
        {
            var tools = TerrainVeinStudySettings.instance;
            return RuleMode ? CaveRuleSettings.TemplateVersion + "|" + Runtime.Terrain.TerrainAuthoringIdentity.Json(draft.Config.FreezeCaveMap()) :
                JointMineCandidate.Version + "|max=" + tools.JointVeinCount + "|nests=" + tools.JointNests;
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
