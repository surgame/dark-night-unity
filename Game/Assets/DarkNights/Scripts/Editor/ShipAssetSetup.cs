using System;
using System.IO;
using System.Linq;
using DarkNights.Runtime.Framework;
using DarkNights.View;
using GameCore.Objects.Definition;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.Rendering;

namespace DarkNights.Editor
{
    /// <summary>岩层飞船的显式首版安装批次；只新建目标 Prefab，保留旧资源和定义 GUID，保存重开后才更新场景引用。</summary>
    public static class ShipAssetSetup
    {
        public const string Root = "Assets/DarkNights/Res/Objects/ExpeditionShip";
        public const string Art = Root + "/Art";
        public const string Prefab = Root + "/ExpeditionShip.prefab";
        internal static JObject Manifest => JObject.Parse(File.ReadAllText("../tools/walkable-ship/art-source/manifest.json"));

        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || Enumerable.Range(0, UnityEngine.SceneManagement.SceneManager.sceneCount)
                .Any(i => UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)) throw new InvalidOperationException("先退出 Play 并保存场景。");
            if (File.Exists(Prefab)) throw new InvalidOperationException("目标 Prefab 已存在，拒绝覆盖后续人工编辑。");
            Import();
            var database = AssetDatabase.LoadAssetAtPath<ObjectDefinitionDatabase>("Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset");
            var index = new DefinitionRuleIndex(database);
            BuildShip(); Assign(index.GetRequired("ship"), Prefab);
            ShipUnitAssetSetup.Install(database, index);
            ShipSceneAssetSetup.Install(index.GetRequired("ship"));
            EditorUtility.SetDirty(database); database.RebuildLookup(); AssetDatabase.SaveAssets();
            Debug.Log("Walkable ship: 50 native textures; ship/robot/drone prefabs; scene and HUD saved/reopened.");
        }

        private static void Import()
        {
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var pair in (JObject)Manifest["assets"])
                {
                    string path = Art + "/" + pair.Key + ".png";
                    var importer = (TextureImporter)AssetImporter.GetAtPath(path);
                    if (importer == null) throw new InvalidOperationException("PNG 未导入：" + path);
                    bool unit = pair.Key.StartsWith("robot_walk_") || pair.Key.StartsWith("drone_hover_") || pair.Key.StartsWith("crew_walk_");
                    importer.textureType = TextureImporterType.Sprite; importer.spriteImportMode = SpriteImportMode.Single;
                    importer.spritePixelsPerUnit = unit ? 100 : 50; importer.filterMode = FilterMode.Point;
                    importer.textureCompression = TextureImporterCompression.Uncompressed; importer.mipmapEnabled = false;
                    importer.alphaIsTransparency = true; importer.sRGBTexture = true; importer.maxTextureSize = 2048;
                    var settings = new TextureImporterSettings(); importer.ReadTextureSettings(settings);
                    settings.spriteAlignment = 9; settings.spriteMeshType = SpriteMeshType.FullRect;
                    settings.spritePivot = new Vector2((float)pair.Value["pivot_unity_normalized"][0], (float)pair.Value["pivot_unity_normalized"][1]);
                    importer.SetTextureSettings(settings); importer.SaveAndReimport();
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
        }

        private static void BuildShip()
        {
            var root = PrefabUtility.LoadPrefabContents("Assets/DarkNights/Res/Objects/Expedition/ship/ship.prefab");
            try
            {
                root.name = "ExpeditionShip";
                var view = root.GetComponent<BuildingView>(); var data = new SerializedObject(view);
                var old = root.GetComponentsInChildren<SpriteRenderer>(true);
                Material material = old.First().sharedMaterial;
                foreach (var renderer in old) renderer.gameObject.SetActive(false);
                // 船体后壁在人物后、外壳在人物前；保留根 SortingGroup 引用供原生视图生命周期使用。
                root.GetComponent<SortingGroup>().enabled = false;
                var art = new GameObject("Ship layers"); art.transform.SetParent(root.transform, false);
                var back = Layer(art.transform, "hull_back", material, -20);
                Layer(art.transform, "hull_structure", material, 3);
                Layer(art.transform, "engine_left", material, 5); Layer(art.transform, "engine_right", material, 5);
                foreach (int x in new[] { 60, 140 })
                {
                    Layer(art.transform, "gear_socket", material, 2, new Vector2(x - 6, 76));
                    Layer(art.transform, "gear_piston", material, 2, new Vector2(x - 3, 78));
                    Layer(art.transform, "gear_foot", material, 2, new Vector2(x - 9, 90));
                }
                Layer(art.transform, "pilot_seat", material, -10); Layer(art.transform, "pilot_console", material, 2);
                Layer(art.transform, "cabin_lamp", material, 3);
                var ship = root.AddComponent<ExpeditionShipView>();
                ship.RobotDock = Layer(art.transform, "robot_dock_empty", material, -10).gameObject;
                ship.CargoLocker = Layer(art.transform, "cargo_locker", material, -10).gameObject;
                ship.WorkShell = Layer(art.transform, "shell_work_section", material, 20);
                ship.CockpitShell = Layer(art.transform, "shell_cockpit_section", material, 20);
                ship.Ramp = Layer(art.transform, "ramp_deployed", material, 2);
                ship.Door = Layer(art.transform, "airlock_open", material, 8);
                ship.Hatch = Layer(art.transform, "drone_hatch_open", material, 4);
                ship.LeftFlame = Layer(art.transform, "flame_medium_0", material, -21, new Vector2(4, 72));
                ship.RightFlame = Layer(art.transform, "flame_medium_0", material, -21, new Vector2(156, 72));
                ship.RampOpen = Sprite("ramp_deployed"); ship.RampClosed = Sprite("ramp_stowed");
                ship.DoorOpen = Sprite("airlock_open"); ship.DoorHalf = Sprite("airlock_half"); ship.DoorClosed = Sprite("airlock_closed");
                ship.HatchOpen = Sprite("drone_hatch_open"); ship.HatchClosed = Sprite("drone_hatch_closed");
                ship.FlameLow = Frames("flame_low", 3); ship.FlameMedium = Frames("flame_medium", 3); ship.FlameHigh = Frames("flame_high", 3);
                data.FindProperty("ship").objectReferenceValue = ship;
                data.FindProperty("complete").objectReferenceValue = back;
                data.FindProperty("portrait").objectReferenceValue = Sprite("hull_front_shell");
                data.FindProperty("expeditionVariants").arraySize = 0; data.FindProperty("clips").arraySize = 0;
                data.FindProperty("pickBounds").rectValue = new Rect(-1.76f, 0, 3.52f, 1.92f);
                SetTint(data, art.GetComponentsInChildren<SpriteRenderer>(true));
                data.ApplyModifiedPropertiesWithoutUndo(); ship.Preview();
                PrefabUtility.SaveAsPrefabAsset(root, Prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            var reopened = PrefabUtility.LoadPrefabContents(Prefab);
            try
            {
                if (reopened.GetComponent<ExpeditionShipView>().DoorClosed == null) throw new InvalidOperationException("船体重开引用丢失。");
            }
            finally { PrefabUtility.UnloadPrefabContents(reopened); }
        }

        internal static SpriteRenderer Layer(Transform parent, string key, Material material, int order, Vector2? origin = null)
        {
            var node = new GameObject(key); node.transform.SetParent(parent, false);
            var a = Manifest["assets"][key];
            Vector2 top = origin ?? new Vector2((float)a["assembly_top_left"][0], (float)a["assembly_top_left"][1]);
            node.transform.localPosition = new Vector3((top.x + (float)a["pivot_top_left"][0] - 88) / 50,
                (96 - top.y - (float)a["pivot_top_left"][1]) / 50, 0);
            var renderer = node.AddComponent<SpriteRenderer>(); renderer.sprite = Sprite(key); renderer.sharedMaterial = material;
            renderer.sortingOrder = order; return renderer;
        }
        internal static Sprite Sprite(string key) => AssetDatabase.LoadAssetAtPath<Sprite>(Art + "/" + key + ".png") ?? throw new InvalidOperationException(key);
        internal static Sprite[] Frames(string key, int count) => Enumerable.Range(0, count).Select(i => Sprite(key + "_" + i)).ToArray();
        internal static void SetTint(SerializedObject data, SpriteRenderer[] renderers)
        {
            var tint = data.FindProperty("tintTargets.targets"); tint.arraySize = renderers.Length;
            for (int i = 0; i < renderers.Length; i++)
            {
                tint.GetArrayElementAtIndex(i).FindPropertyRelative("renderer").objectReferenceValue = renderers[i];
                tint.GetArrayElementAtIndex(i).FindPropertyRelative("baseColor").colorValue = Color.white;
            }
        }
        internal static void Assign(ObjectDefinition definition, string prefab)
        {
            string guid = AssetDatabase.AssetPathToGUID(prefab);
            definition.PrefabRef = new AssetReferenceGameObject(guid); EditorUtility.SetDirty(definition);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.CreateOrMoveEntry(guid, settings.DefaultGroup).address = "dark_nights.ship." + definition.Key;
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(settings.DefaultGroup);
        }
    }
}
