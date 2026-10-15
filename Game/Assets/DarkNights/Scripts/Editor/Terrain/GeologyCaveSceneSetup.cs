using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AnyRules.Next;
using DarkNights.Core.Config.Terrain;
using DarkNights.Entry.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor.Terrain
{
    /// <summary>把专属GameplayLab升级为两段既有作者矿洞的可玩样板；只定向修改本场景和两份局部定义。原矿洞输入只读，运行使用相同格形和正式岩壁渲染。</summary>
    public static class GeologyCaveSceneSetup
    {
        public static void Upgrade()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
                Enumerable.Range(0, SceneManager.sceneCount).Any(i => SceneManager.GetSceneAt(i).isDirty))
                throw new InvalidOperationException("升级前需要空闲Editor及已保存场景。");
            string backup = Path.GetFullPath("../artifacts/cl02-collapse-20261015/original");
            Directory.CreateDirectory(backup);
            if (!File.Exists(Path.Combine(backup, "GameplayLab.unity")))
                File.Copy(GeologyLabEntry.ScenePath, Path.Combine(backup, "GameplayLab.unity"));
            foreach (string file in new[] { "GeologyEvent.prefab", "SupportCollapse.asset", "RegionRockfall.asset" })
                if (!File.Exists(Path.Combine(backup, file))) File.Copy(GeologyLabAssets.Root + "/" + file, Path.Combine(backup, file));
            GeologyCollapseAssets.Upgrade();
            var scene = EditorSceneManager.OpenScene(GeologyLabEntry.ScenePath);
            var lab = scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<GeologyLabEntry>()).Single();
            foreach (var system in scene.GetRootGameObjects().SelectMany(r => r.GetComponentsInChildren<UnityEngine.EventSystems.EventSystem>()).ToArray())
                Undo.DestroyObjectImmediate(system.gameObject);
            Undo.RegisterFullObjectHierarchyUndo(lab.gameObject, "升级塌方矿洞样板");
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(GeologyLabAssets.Root + "/CellMarker.png");
            foreach (var block in lab.Blocks) if (block != null) Undo.DestroyObjectImmediate(block.gameObject);
            var blocks = new List<GeologyLabBlock>();
            CopyCave(blocks, lab.transform, sprite, "rift-expanded", 50, 30, 32, 24, 40, 26);
            CopyCave(blocks, lab.transform, sprite, "vein-maze-expanded", 17, 8, 76, 28, 40, 24);
            Add(blocks, lab.transform, sprite, 50, -41, 2, false, "SupportToMine");
            for (int u = 0; u < 32; u++) Add(blocks, lab.transform, sprite, u, -42, 8, true, "Approach");
            for (int u = 72; u < 76; u++) Add(blocks, lab.transform, sprite, u, -42, 8, true, "Connector");
            lab.Blocks = blocks.ToArray(); lab.SpawnX = 49 * 16; lab.SpawnHeight = -32; lab.WorldWidth = 2048;
            Configure(lab.EventSlots[0], new GeologyEventConfig { SourceRows = 3, RubbleWidth = 5, SourceV = -31 });
            Configure(lab.EventSlots[1], new GeologyEventConfig
            { EventId = "region", EnterRegion = true, SourceU = 95, SourceV = -34, LandingU = 95,
                SourceRows = 3, RubbleWidth = 5, RegionU = 92, RegionV = -41, RegionWidth = 4, RegionHeight = 5 });
            if (lab.TerrainView == null)
            {
                var root = new GameObject("NativeCaveTerrain"); root.transform.SetParent(lab.transform, false);
                lab.TerrainView = root.AddComponent<TerrainPreview>();
            }
            lab.TerrainView.transform.localPosition = new Vector3(0, 6.32f, 0);
            lab.TerrainView.transform.localScale = Vector3.one * .16f;
            lab.TerrainView.ViewCamera = lab.ViewCamera; lab.TerrainView.LocalPreviewBackground = true;
            lab.TerrainView.LocalRegion = new GridBounds(0, -64, 128, 64);
            lab.ViewCamera.depth = 100; lab.ViewCamera.backgroundColor = new Color(.035f, .04f, .045f);
            lab.ViewCamera.orthographicSize = 1.8f; lab.ViewCamera.transform.position = new Vector3(8.2f, .4f, -10);
            lab.Status.text = "矿洞局部塌方 · 真实关卡片段\n左侧低岩为支撑触发；右侧区域为进入触发。\n掉灰后碎岩加速坠落，落地堆积；避让后用矿镐清障。\nEsc暂停；重新Play恢复初始地形。";
            var region = lab.transform.Find("RegionTrigger");
            if (region != null) { region.position = new Vector3(15.04f, .08f, 0); region.localScale = new Vector3(4, 5, 1); }
            EditorUtility.SetDirty(lab); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        }

        private static void CopyCave(List<GeologyLabBlock> blocks, Transform root, Sprite sprite, string name,
            int sourceX, int sourceY, int targetX, int targetY, int width, int height)
        {
            var map = AssetDatabase.LoadAssetAtPath<MineMapAsset>("Assets/DarkNights/Res/Terrain/MineMaps/" + name + ".minemap").Freeze();
            var cells = map.DecodeBase();
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width;)
                {
                    if (cells[(sourceY + y) * map.BaseWidth + sourceX + x] == 0) { x++; continue; }
                    int start = x++;
                    while (x < width && cells[(sourceY + y) * map.BaseWidth + sourceX + x] != 0) x++;
                    Add(blocks, root, sprite, targetX + start, -(targetY + y), 2, targetY + y >= 42, name, x - start);
                }
        }

        private static void Add(List<GeologyLabBlock> blocks, Transform root, Sprite sprite, int u, int v, byte material, bool protect, string name, int width = 1)
        {
            var art = GeologyLabAssets.Marker(name + "_" + u + "_" + v, root, sprite,
                name == "SupportToMine" ? new Color(.75f, .64f, .3f) : new Color(.33f, .35f, .34f));
            art.transform.position = new Vector3((u + (width - 1) * .5f) * .16f, (632 + v * 16) / 100f, 0);
            art.transform.localScale = new Vector3(width, 1, 1);
            var block = art.gameObject.AddComponent<GeologyLabBlock>();
            block.U = u; block.V = v; block.Width = width; block.Material = material; block.Protected = protect; block.Art = art;
            blocks.Add(block);
        }

        private static void Configure(GeologyLabEventSlot slot, GeologyEventConfig config)
        {
            config.Freeze();
            int index = slot.Definition.SharedConfigs.FindIndex(c => c is GeologyEventConfig);
            Undo.RecordObject(slot.Definition, "塌方岩团和瓦砾堆配置");
            slot.Definition.SharedConfigs[index] = config; EditorUtility.SetDirty(slot.Definition);
            AssetDatabase.SaveAssetIfDirty(slot.Definition);
            slot.View.Warning.transform.position = new Vector3((config.LandingU + 1) * .16f, (632 + config.LandingV * 16) / 100f, 0);
            slot.View.Warning.transform.localScale = new Vector3(config.RubbleWidth, 1, 1);
        }
    }
}
