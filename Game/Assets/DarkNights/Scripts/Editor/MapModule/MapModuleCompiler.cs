using System;
using System.Linq;
using DarkNights.Entry.MapModule;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.MapModule;
using DarkNights.Core.Logic.Terrain;
using DarkNights.View.MapModule;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkNights.Editor.MapModule
{
    /// <summary>
    /// 显式检查与固定版本输出；输入只读来自原生Scene／Prefab，不借旧地图草稿或程序生成器。
    /// 资产采用来源与内容摘要命名、不覆盖旧版本；成功后保存派生引用，失败保留原资源和作者输入。
    /// </summary>
    internal static class MapModuleCompiler
    {
        internal static PlayableTerrain Check(MapModuleStudyAuthoring study, MapModuleCaseAuthoring sample)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || PrefabStageUtility.GetCurrentPrefabStage() != null)
                throw new InvalidOperationException("请先停止Play或保存并退出Prefab模式。");
            if (study != null && study.gameObject.scene.GetRootGameObjects().Any(root =>
                root.GetComponentsInChildren<AudioListener>(true).Length != 0))
                throw new InvalidOperationException("研究Scene不能携带AudioListener；通过Bootstrap启动时由其提供唯一监听器。");
            CheckInput(study);
            MapModuleStartupValidation.Check(study);
            string source = MapModuleSource.Hash(study, sample);
            foreach (var module in sample.Modules)
            {
                if (!PrefabUtility.IsPartOfPrefabInstance(module) ||
                    PrefabUtility.GetNearestPrefabInstanceRoot(module) != module.gameObject)
                    throw new InvalidOperationException("模块必须是独立的原生Prefab实例。");
                var overrides = PrefabUtility.GetPropertyModifications(module.gameObject);
                if (overrides != null && overrides.Any(p => p.target is MapModuleAuthoring &&
                    (p.propertyPath.StartsWith("Solids") || p.propertyPath.StartsWith("Ports") ||
                     p.propertyPath == "Footprint" || p.propertyPath.StartsWith("Footprint.") ||
                     p.propertyPath == "ModuleId" || p.propertyPath == "DisplayName")))
                    throw new InvalidOperationException("场景实例覆盖了内部模块作者数据，请在Prefab编辑：" + module.DisplayName);
            }
            var polygons = sample.Modules.SelectMany(module => module.Freeze()).ToArray();
            var data = MapModuleRasterizer.Build(Guid.Empty.ToString("N"), "mm-source:" + source, polygons);
            var shapes = data.CopyShapes();
            foreach (var connection in sample.Connections) CheckPort(data, shapes, connection);
            CheckSpawn(data, shapes, sample.Spawn.position * 100);
            return data;
        }
        private static void CheckInput(MapModuleStudyAuthoring study)
        {
            if (study == null) throw new InvalidOperationException("缺少原生研究作者场景。");
            var entry = study.gameObject.scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MapModuleEntry>(true)).SingleOrDefault();
            var input = entry != null && entry.Stage != null ? entry.Stage.InputPlayer : null;
            if (entry == null || entry.Study != study || input == null || input.gameObject.scene != study.gameObject.scene)
                throw new InvalidOperationException("研究Rig需要显式绑定本Scene的唯一入口、Stage及PlayerInput。");
            var actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(HeroInputAssetSetup.Path);
            if (actions == null || input.actions != actions)
                throw new InvalidOperationException("研究输入必须引用正式Gameplay.inputactions，不能使用InputSystem_Actions模板。");
            foreach (string map in new[] { "Camp", "Player", "UI" }) actions.FindActionMap(map, true);
            entry.FreezePhysics();
            if (input.defaultActionMap != "Camp" || input.notificationBehavior != PlayerNotifications.InvokeCSharpEvents ||
                input.camera != entry.Stage.SceneCamera)
                throw new InvalidOperationException("研究输入需使用Camp默认图、C#事件通知并绑定本Scene镜头。");
        }
        private static void CheckSpawn(PlayableTerrain data, byte[] shapes, Vector3 point)
        {
            int column = Mathf.RoundToInt(point.x / 16);
            int row = PlayableTerrain.CampRow - Mathf.RoundToInt(point.y / 16);
            if (Math.Abs(point.x / 16 - column) > .001 || Math.Abs(point.y / 16 - Mathf.Round(point.y / 16)) > .001 ||
                column < 2 || column >= TerrainGenerationSettings.Width - 2 || row < 4 || row >= TerrainGenerationSettings.Height - 2)
                throw new InvalidOperationException("出生锚点需对齐整格地面，位于地图内部。");
            for (int x = column - 1; x <= column + 1; x++)
            {
                if (data.Material(x, row) == 0 || shapes[row * TerrainGenerationSettings.Width + x] != (byte)TerrainCellShape.Full)
                    throw new InvalidOperationException("首片出生锚点需要三格同高完整支撑。");
                for (int y = row - 4; y < row; y++)
                    if (data.Material(x, y) != 0) throw new InvalidOperationException("出生锚点上方缺少四格净空。");
            }
        }
        private static void CheckPort(PlayableTerrain data, byte[] shapes, MapModuleConnection connection)
        {
            var port = connection.From.Ports.Single(p => p.Id == connection.FromPort);
            var offset = connection.From.CellOffset; var from = port.From + offset; var to = port.To + offset;
            if (port.Normal.y != 0) throw new InvalidOperationException("首片先实现水平相邻连接；纵向端口仍可制作但不烘焙。");
            int left = from.x - 1, right = from.x;
            if (left < 0 || right >= TerrainGenerationSettings.Width)
                throw new InvalidOperationException("连接超出地图内部。");
            for (int y = from.y; y < to.y; y++)
            {
                int row = PlayableTerrain.CampRow - 1 - y;
                if (row < 0 || row >= TerrainGenerationSettings.Height ||
                    data.Material(left, row) != 0 || data.Material(right, row) != 0)
                    throw new InvalidOperationException("连接端口内侧一格带并非完整开放。");
            }
            int bottom = Math.Max(connection.From.CellOffset.y, connection.To.CellOffset.y);
            int top = Math.Min(connection.From.CellOffset.y + connection.From.Footprint.y * 20,
                connection.To.CellOffset.y + connection.To.Footprint.y * 20);
            for (int y = bottom; y < top; y++)
            {
                int row = PlayableTerrain.CampRow - 1 - y;
                if (row < 0 || row >= TerrainGenerationSettings.Height ||
                    data.Material(left, row) != data.Material(right, row) ||
                    shapes[row * TerrainGenerationSettings.Width + left] != shapes[row * TerrainGenerationSettings.Width + right])
                    throw new InvalidOperationException("首片连接边带材料／格形不一致，未自动填补。");
            }
        }
        internal static MapModuleBake Prepare(MapModuleStudyAuthoring study, MapModuleCaseAuthoring sample)
        {
            if (sample.gameObject.scene.isDirty) throw new InvalidOperationException("先保存作者Scene，再准备固定制作版本。");
            var data = Check(study, sample);
            string hash = MapModuleSource.Hash(study, sample);
            MapModulePaths.EnsureFolder(MapModulePaths.Bakes);
            string path = MapModulePaths.Bakes + "/" + study.StudyId + "-" + sample.CaseId + "-" + hash + ".asset";
            var bake = AssetDatabase.LoadAssetAtPath<MapModuleBake>(path);
            if (bake == null)
            {
                if (AssetDatabase.LoadMainAssetAtPath(path) != null) throw new InvalidOperationException("目标路径被其他资源占用。");
                bake = ScriptableObject.CreateInstance<MapModuleBake>();
                bake.Initialize(study.StudyId, sample.CaseId, hash, sample.Spawn.position.x * 100, sample.Spawn.position.y * 100, data);
                AssetDatabase.CreateAsset(bake, path);
                var mesh = MapModuleMesh.Build(data.CopyMaterials(), data.CopyShapes(), new Color32(132, 143, 111, 255));
                mesh.name = "Frozen whitebox"; AssetDatabase.AddObjectToAsset(mesh, bake);
            }
            bake.Freeze(Guid.Empty.ToString("N"));
            if (bake.SourceHash != hash) throw new InvalidOperationException("已有制作版本来源失配。");
            var frozenMesh = AssetDatabase.LoadAllAssetsAtPath(path).OfType<Mesh>().Single();
            Undo.RecordObjects(new UnityEngine.Object[] { sample, sample.PreviewMesh, sample.PreviewRenderer }, "准备模块制作版本");
            sample.Bake = bake; sample.PreviewMesh.sharedMesh = frozenMesh; sample.PreviewRenderer.enabled = true;
            foreach (var module in sample.Modules)
                if (module.PreviewRenderer != null)
                {
                    Undo.RecordObject(module.PreviewRenderer, "切换模块派生显示");
                    module.PreviewRenderer.enabled = false;
                    PrefabUtility.RecordPrefabInstancePropertyModifications(module.PreviewRenderer);
                }
            EditorUtility.SetDirty(sample); EditorUtility.SetDirty(sample.PreviewMesh); EditorUtility.SetDirty(sample.PreviewRenderer);
            AssetDatabase.SaveAssetIfDirty(bake);
            EditorSceneManager.MarkSceneDirty(sample.gameObject.scene);
            if (!EditorSceneManager.SaveScene(sample.gameObject.scene)) throw new InvalidOperationException("制作版本已保留，但场景引用未保存。");
            return bake;
        }
    }
}
