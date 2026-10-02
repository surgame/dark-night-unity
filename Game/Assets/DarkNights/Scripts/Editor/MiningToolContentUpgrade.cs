using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using DarkNights.Runtime.Objects;
using GameCore.Objects.Definition;
using GameCore.Editor.Objects.Definition;
using GameCore.Objects.Runner;
using UnityEditor;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.Editor
{
    /// <summary>显式升级现有矿镐定义和 Prefab；保留 GUID、图像和已有组件，只补 YYGC 装配及迁移原参数，重复执行不覆盖工具配置。</summary>
    public static class MiningToolContentUpgrade
    {
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling)
                throw new InvalidOperationException("退出 Play 并等待编译完成后升级。");
            const string sessionPath = "Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset";
            const string toolPath = "Assets/DarkNights/Res/Objects/ShipTrade/item-pickaxe.asset";
            const string prefabPath = "Assets/DarkNights/Res/Objects/ShipTrade/item-pickaxe.prefab";
            var tool = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(toolPath);
            if (tool == null) throw new InvalidOperationException("缺少正式矿镐 Definition。");
            Undo.RecordObject(tool, "迁移矿镐 Definition 能力");
            if (!tool.SharedConfigs.OfType<MiningToolConfig>().Any())
            {
                string text = File.ReadAllText(sessionPath);
                var mining = new MiningToolConfig
                {
                    Damage = (int)Read(text, "PickaxeDamage", 10), Reach = Read(text, "PickaxeReach", 48),
                    HandHeight = Read(text, "PickaxeHandHeight", 36), Seconds = Read(text, "PickaxeSeconds", .48f),
                    ImpactFraction = Read(text, "PickaxeImpactFraction", .6f)
                };
                mining.Freeze(); tool.SharedConfigs.Add(mining);
            }
            if (!tool.BehaviourTypes.Contains(typeof(MiningToolBehaviour).FullName)) tool.BehaviourTypes.Add(typeof(MiningToolBehaviour).FullName);
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var instance = root.GetComponent<ObjectInstance>() ?? root.AddComponent<ObjectInstance>();
                var initializer = root.GetComponent<LocalObjectInstanceInitializer>() ?? root.AddComponent<LocalObjectInstanceInitializer>();
                var view = root.GetComponent<ObjectView>() ?? root.AddComponent<ObjectView>();
                Set(instance, "_view", view); Set(initializer, "_objectInstance", instance);
                view.ForceRefreshAllReferences();
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
            EditorUtility.SetDirty(tool); AssetDatabase.SaveAssetIfDirty(tool);
            var session = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(sessionPath);
            EditorUtility.SetDirty(session); AssetDatabase.SaveAssetIfDirty(session);
            var deposit = AssetDatabase.LoadAssetAtPath<ObjectDefinition>(MineralDepositContentSetup.DefinitionPath);
            EditorUtility.SetDirty(deposit); AssetDatabase.SaveAssetIfDirty(deposit);
            const string bombPath = "Assets/DarkNights/Res/Objects/ShipTrade/item-bomb.asset";
            var database = ObjectDefinitionDatabase.Instance;
            if (database.GetDefinitionByKey("item.bomb") == null)
            {
                if (File.Exists(bombPath)) throw new InvalidOperationException("未注册的炸弹定义已存在，请先核对。");
                var bomb = UnityEngine.Object.Instantiate(database.GetDefinitionByKey("item.pistol"));
                bomb.Name = "爆破弹"; bomb.BehaviourTypes.Clear(); bomb.SharedConfigs.Clear();
                bomb.SharedConfigs.Add(new EquipmentItemConfig { RuleKey = "bomb", Handheld = Core.Config.HeroEquipmentKind.Bomb });
                AssetDatabase.CreateAsset(bomb, bombPath);
                DefinitionIdentityAuthoring.AdoptCopiedAsset(bomb, database);
                bomb.EditorRenameKey("item.bomb"); database.RebuildLookup();
                EditorUtility.SetDirty(database); AssetDatabase.SaveAssetIfDirty(database);
                EditorUtility.SetDirty(bomb); AssetDatabase.SaveAssetIfDirty(bomb);
            }
        }
        private static float Read(string text, string name, float fallback)
        {
            var match = Regex.Match(text, @"(?m)^\s+" + name + @":\s*([0-9.]+)\s*$");
            return match.Success ? float.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture) : fallback;
        }
        private static void Set(UnityEngine.Object owner, string field, UnityEngine.Object value)
        {
            var serialized = new SerializedObject(owner);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
