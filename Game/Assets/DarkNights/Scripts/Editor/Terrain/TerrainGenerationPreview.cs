using System;
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
        private string planetId;
        public const string WorldId = "00000000000000000000000000000001";
        public ARDMapDefinition Definition { get; private set; }
        public string Identity => JsonUtility.ToJson(draft.Config) + "|" + planetId;
        public bool HasChanges => draft.HasChanges;

        public TerrainGenerationPreview()
        {
            Definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>("Assets/DarkNights/Res/Terrain/StrataCave/MapDefinition.asset");
            draft = ScriptableObject.CreateInstance<ExpeditionFlowDraft>();
            draft.hideFlags = HideFlags.HideAndDontSave;
            draft.Load(AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset"));
            planetId = draft.Config.PreviewPlanet().Id;
        }

        public PlayableTerrain Generate()
        {
            var settings = draft.Config.FreezeCaveMap();
            return PlanetTerrainGenerator.GenerateCandidate(draft.Config.PreviewPlanet(planetId), settings.Seed, WorldId, settings);
        }

        public void ObserveExternalChanges()
        {
            var current = draft.Source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            if (!draft.HasChanges && JsonUtility.ToJson(current) != draft.BaselineJson) draft.Load(draft.Source);
        }

        public void Draw(Action changed)
        {
            EditorGUILayout.HelpBox("与 Bootstrap 共用完整星球生成。选择同一星球、输入航程实际种子即可逐格复现；拆填仅作临时预览。", MessageType.Info);
            var planets = draft.Config.FreezePlanets().Where(p => p.Enabled).ToArray();
            int selected = Math.Max(0, Array.FindIndex(planets, p => p.Id == planetId));
            EditorGUI.BeginChangeCheck();
            int next = EditorGUILayout.Popup("正式星球", selected, planets.Select(p => p.DisplayName).ToArray());
            planetId = planets[next].Id;
            draft.Config.CaveMap.Seed = EditorGUILayout.TextField("预览／复现种子", draft.Config.CaveMap.Seed);
            draft.Config.CaveMap.Amplitude = EditorGUILayout.Slider("地表起伏", (float)draft.Config.CaveMap.Amplitude, .3f, 1.6f);
            if (EditorGUI.EndChangeCheck()) changed();
            EditorGUILayout.LabelField("天空、泊位和入口参数来自“星球与航程”配置。", EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("使用星球固定种子") && planets[next].Seed.Length != 0)
            { draft.Config.CaveMap.Seed = planets[next].Seed; changed(); }
            if (GUILayout.Button("新预览种子"))
            { draft.Config.CaveMap.Seed = Guid.NewGuid().ToString("N"); changed(); }
            EditorGUILayout.EndHorizontal();
            if (GUILayout.Button("将预览种子设为此星球固定种子"))
            { draft.Config.Planets.Single(p => p.Id == planetId).Seed = draft.Config.CaveMap.Seed; changed(); }
            using (new EditorGUI.DisabledScope(!draft.HasChanges))
            {
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("保存正式生成配置")) { draft.Apply(); changed(); }
                if (GUILayout.Button("取消生成配置草稿")) { draft.Load(draft.Source); changed(); }
                EditorGUILayout.EndHorizontal();
            }
        }

        public void Dispose()
        {
            if (draft != null) UnityEngine.Object.DestroyImmediate(draft);
            draft = null;
        }
    }
}
