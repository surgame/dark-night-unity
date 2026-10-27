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
        private TerrainGenerationWork work;
        internal bool IsGenerating => work != null;
        private readonly List<TerrainModifierDiagnostic> diagnostics = new List<TerrainModifierDiagnostic>();
        private readonly List<string> contentDiagnostics = new List<string>();
        public IReadOnlyList<string> ContentDiagnostics => contentDiagnostics;
        public CaveRulePlan RulePlan { get; private set; }
        public TerrainGenerationResult Result { get; private set; }
        public bool CaptureStages { get; set; }
        public bool HasRoomPlan => draft.Config.CaveMap.Layout == TerrainGenerationSettings.ConnectedRooms;
        public bool RegionalMinerals => draft.Config.CaveMap.Minerals?.Algorithm == TerrainMineralSettings.RegionalVeins;
        public int GenerationRequest { get; private set; }
        public bool ExplicitGeneration => HasRoomPlan && GenerationRequest == 0;
        public void RequestGeneration() { GenerationRequest++; }
        public string PlanetId { get; set; }
        public const string WorldId = "00000000000000000000000000000001";
        public ARDMapDefinition Definition { get; private set; }
        public ExpeditionFlowDraft Draft => draft;
        public PlanetPreset Selected => draft.Config.Planets?.FirstOrDefault(p => p != null && p.Id == PlanetId)
            ?? draft.Config.Planets?.FirstOrDefault(p => p != null);
        public IReadOnlyList<TerrainModifierDiagnostic> Diagnostics => diagnostics;
        public string Identity => TerrainGenerationIdentity.For(draft.Config, Selected);
        public bool HasChanges => draft.HasChanges;
        public ExpeditionFlowConfig InstalledConfig => draft.Source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
        public bool CandidateFresh => LastCandidate != null && LastCandidateIdentity == Identity;
        public bool CandidateFromSaved { get; private set; }
        public bool CandidateNeedsSave => LastCandidate != null && !CandidateFromSaved;
        public PlayableTerrain LastCandidate { get; private set; }
        public string LastCandidateIdentity { get; private set; }
        public string GeologyConflict { get; private set; } = "";
        public string LandingConflict
        {
            get
            {
                if (LastCandidate == null || Selected == null) return "尚无完整候选或泊位。";
                try { return ShipLandingGeometry.Inspect(LastCandidate, Selected.Freeze()); }
                catch (Exception error) { return "泊位配置待修正：" + error.Message; }
            }
        }

        public TerrainGenerationPreview(ExpeditionFlowDraft existing = null)
        {
            var source = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset");
            Definition = source.SharedConfigs.OfType<DarkNights.Runtime.Terrain.TerrainProfileConfig>().Single().Definition;
            draft = existing;
            if (draft == null) { draft = ScriptableObject.CreateInstance<ExpeditionFlowDraft>(); draft.Load(source); }
            // HideAndDontSave 包含 NotEditable，会禁用序列化属性的输入控件。
            draft.hideFlags = HideFlags.DontSave;
            MapWorkbenchImport.NormalizeDraft(draft);
            PlanetId = draft.Config.Planets?.FirstOrDefault(p => p != null && p.Enabled)?.Id ?? Selected?.Id;
        }

        /// <summary>串行推进最新一次制作计算；连续输入不并发开工，过期结果不触碰当前画布。</summary>
        internal bool AdvanceGeneration()
        {
            if (work == null) { work = new TerrainGenerationWork(this); return false; }
            if (!work.Task.IsCompleted) return false;
            var completed = work; work = null;
            if (completed.Identity != Identity)
            {
                _ = completed.Task.Exception;
                work = new TerrainGenerationWork(this);
                return false;
            }
            GenerationRequest = 0;
            Result = completed.Task.GetAwaiter().GetResult();
            var candidate = Result.Terrain;
            LastCandidate = candidate; LastCandidateIdentity = completed.Identity;
            CandidateFromSaved = false;
            RulePlan = Result.Plan; GeologyConflict = completed.GeologyConflict;
            contentDiagnostics.Clear(); contentDiagnostics.AddRange(completed.Report);
            return true;
        }

        /// <summary>安装严格读取且来源一致的制作结果；保留同版完整地图，后续作者参数变化才重新生成。</summary>
        internal void AcceptSaved(PlayableTerrain map, CaveRulePlan plan, TerrainGenerationResult result = null)
        {
            if (work != null) throw new InvalidOperationException("上一批计算尚未结束，不能安装文档地图。");
            LastCandidate = map; LastCandidateIdentity = Identity; RulePlan = plan;
            CandidateFromSaved = true;
            Result = result;
            GenerationRequest = 0; GeologyConflict = ""; diagnostics.Clear(); contentDiagnostics.Clear();
            if (plan != null) contentDiagnostics.AddRange(plan.Diagnostics);
        }

        public PlayableTerrain Generate()
        {
            MapWorkbenchImport.NormalizeDraft(draft);
            var settings = draft.Config.FreezeCaveMap();
            diagnostics.Clear(); contentDiagnostics.Clear(); RulePlan = null; GeologyConflict = "";
            var planet = Selected?.Freeze() ?? throw new InvalidOperationException("没有可预览的星球。");
            var generated = PlanetTerrainGenerator.GenerateResult(planet, settings.Seed, WorldId, settings,
                contentReport: contentDiagnostics.Add, captureStages: CaptureStages);
            var candidate = generated.Terrain;
            try { candidate = GeologyMapDesign.Attach(candidate, draft.Config.Geology); }
            catch (Exception error)
            {
                GeologyConflict = error.Message;
            }
            Result = new TerrainGenerationResult(candidate, generated.Plan, generated.Version,
                generated.SpaceIdentity, generated.Steps.ToArray());
            RulePlan = Result.Plan;
            GenerationRequest = 0;
            LastCandidate = candidate; LastCandidateIdentity = Identity;
            CandidateFromSaved = false;
            return candidate;
        }

        internal void MarkCandidateSaved() => CandidateFromSaved = true;

        public string ExportBlockReason()
        {
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

        private string Recipe() => "terrain-recipe-v1|" + TerrainAuthoringIdentity.Json(draft.Config.CaveMap);

        public void ObserveExternalChanges()
        {
            var current = draft.Source.SharedConfigs.OfType<ExpeditionFlowConfig>().SingleOrDefault();
            if (!draft.IndependentDocument && !draft.HasChanges && (current?.CanonicalIdentity() ?? "") != draft.BaselineJson)
            { draft.Load(draft.Source); MapWorkbenchImport.NormalizeDraft(draft); diagnostics.Clear(); }
        }

        public void Draw(Action changed)
        {
            using var controls = new TerrainPlanetControls(this);
            controls.DrawMap(changed);
        }

        public void Apply() => draft.Apply();
        public void Cancel() { draft.Load(draft.Source); MapWorkbenchImport.NormalizeDraft(draft); diagnostics.Clear(); }

        public void Import(ExpeditionFlowDraft legacy)
        {
            if (legacy == null || !legacy.HasChanges) return;
            if (HasChanges && draft.Config.CanonicalIdentity() != legacy.Config.CanonicalIdentity())
                throw new InvalidOperationException("两个编辑窗口存在不同的未保存配置；请先处理当前配置再导入。");
            draft.CopyFrom(legacy);
            MapWorkbenchImport.NormalizeDraft(draft);
        }

        public void Dispose() => Dispose(true);

        public void Dispose(bool destroyDraft)
        {
            if (destroyDraft && draft != null) UnityEngine.Object.DestroyImmediate(draft);
            draft = null;
        }
    }
}
