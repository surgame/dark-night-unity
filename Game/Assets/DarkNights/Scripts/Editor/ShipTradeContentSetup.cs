using System;
using System.IO;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Runtime.Objects;
using GameCore.Editor.Objects.Definition;
using GameCore.Objects.Definition;
using GameCore.Objects.Types;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace DarkNights.Editor
{
    /// <summary>一次性安装船上两个可替换终端与三种装备定义；仅创建新资产并给既有飞船 Prefab 增加子物体。</summary>
    public static class ShipTradeContentSetup
    {
        private const string Root = "Assets/DarkNights/Res/Objects/ShipTrade";
        private const string Database = "Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset";
        private const string Uxml = "Assets/DarkNights/Res/UI/ShipEquipment/ShipEquipment.uxml";
        [DarkNightsWorkbenchCommand("Dark Nights/Content/安装飞船交易和装备", "安装飞船交易和装备")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(ShipAssetSetup.Prefab) ||
                Directory.Exists(Root)) throw new InvalidOperationException("先退出 Play；目标目录必须不存在且原飞船 Prefab 已安装。");
            var database = AssetDatabase.LoadAssetAtPath<ObjectDefinitionDatabase>(Database);
            if (database == null) throw new InvalidOperationException("缺少 YYGC 定义库。");
            foreach (string key in new[] { "ship-service.sale", "ship-service.shop", "item.pistol", "item.pickaxe", "item.jetpack" })
                if (database.GetDefinitionByKey(key) != null) throw new InvalidOperationException("定义已存在：" + key);
            var shipAsset = AssetDatabase.LoadAssetAtPath<GameObject>(ShipAssetSetup.Prefab);
            if (shipAsset.GetComponentsInChildren<ShipServiceAnchor>(true).Length != 0)
                throw new InvalidOperationException("飞船已有服务终端；拒绝覆盖人工编辑。");
            AssetDatabase.CreateFolder("Assets/DarkNights/Res/Objects", "ShipTrade");
            var material = shipAsset.GetComponentsInChildren<SpriteRenderer>(true).First().sharedMaterial;
            ObjectDefinition sale = CreateService(database, "sale", "矿石出售点", "cargo_locker", material);
            ObjectDefinition shop = CreateService(database, "shop", "装备商店", "pilot_console", material);
            var shipDefinition = database.GetDefinitionByKey("expedition.ship");
            if (shipDefinition == null || shipDefinition.SharedConfigs.OfType<ShipServicesConfig>().Any())
                throw new InvalidOperationException("飞船定义缺失或已有服务配置。");
            shipDefinition.SharedConfigs.Add(new ShipServicesConfig
            { Sale = new DefinitionReference(sale.Guid), Shop = new DefinitionReference(shop.Guid) });
            EditorUtility.SetDirty(shipDefinition);
            CreateItem(database, "pistol", "手枪", HeroEquipmentKind.Pistol, ObjectType.Weapon_Ranged, material);
            CreateItem(database, "pickaxe", "矿镐", HeroEquipmentKind.Pickaxe, ObjectType.Tool, material);
            CreateItem(database, "jetpack", "喷气背包", HeroEquipmentKind.Empty, ObjectType.Armor_Accessory, material);
            Attach(shipAsset, sale, shop);
            string uxmlGuid = AssetDatabase.AssetPathToGUID(Uxml);
            if (string.IsNullOrEmpty(uxmlGuid)) throw new InvalidOperationException("商店 UXML 未导入。");
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.CreateOrMoveEntry(uxmlGuid, settings.DefaultGroup).address = "dark_nights.ui.ship_equipment";
            EditorUtility.SetDirty(database); database.RebuildLookup();
            EditorUtility.SetDirty(settings); EditorUtility.SetDirty(settings.DefaultGroup);
            AssetDatabase.SaveAssets();
            Validate(database);
            Debug.Log("DARK_NIGHTS_SHIP_TRADE_INSTALLED services=2 items=3 uxml=1");
        }

        private static ObjectDefinition CreateService(ObjectDefinitionDatabase database, string key, string title,
            string image, Material material)
        {
            string path = Root + "/" + key + ".prefab";
            var root = new GameObject(title);
            try
            {
                root.AddComponent<ShipServiceAnchor>();
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = ShipAssetSetup.Sprite(image); renderer.sharedMaterial = material;
                renderer.sortingOrder = 12;
                root.transform.localScale = new Vector3(.34f, .34f, 1);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            var definition = Define(database, path, "ship-service." + key, title, ObjectType.Placeable_Workstation);
            definition.SharedConfigs.Add(new ShipServiceConfig { Service = key, Radius = 18 });
            var contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                contents.GetComponent<ShipServiceAnchor>().EditorSetDefinition(definition);
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); }
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void CreateItem(ObjectDefinitionDatabase database, string key, string title,
            HeroEquipmentKind handheld, ObjectType type, Material material)
        {
            string path = Root + "/item-" + key + ".prefab";
            var root = new GameObject(title);
            try
            {
                var renderer = root.AddComponent<SpriteRenderer>();
                renderer.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(
                    "Assets/DarkNights/Res/Art/Custom/Handheld/" +
                    (key == "jetpack" ? "BombIcon" : key == "pistol" ? "PistolIcon" : "PickaxeIcon") + ".png");
                renderer.sharedMaterial = material;
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
            var definition = Define(database, path, "item." + key, title, type);
            definition.SharedConfigs.Add(new EquipmentItemConfig
            { RuleKey = key, Jetpack = key == "jetpack", Handheld = handheld });
            EditorUtility.SetDirty(definition);
        }

        private static ObjectDefinition Define(ObjectDefinitionDatabase database, string prefab, string key,
            string title, ObjectType type)
        {
            var definition = ScriptableObject.CreateInstance<ObjectDefinition>();
            definition.Name = title; definition.Type = type; definition.NetType = NetworkType.Local;
            definition.PrefabRef = new AssetReferenceGameObject(AssetDatabase.AssetPathToGUID(prefab));
            AssetDatabase.CreateAsset(definition, Root + "/" + key.Replace('.', '-') + ".asset");
            definition.EditorSetIdentity(DefinitionIdentityAuthoring.ReadAssetGuid(definition), key, false);
            database.AddDefinition(definition);
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            settings.CreateOrMoveEntry(definition.PrefabRef.AssetGUID, settings.DefaultGroup).address = "dark_nights." + key;
            EditorUtility.SetDirty(definition); return definition;
        }

        private static void Attach(GameObject shipAsset, ObjectDefinition sale, ObjectDefinition shop)
        {
            var root = PrefabUtility.LoadPrefabContents(ShipAssetSetup.Prefab);
            try
            {
                foreach (var pair in new[] { (sale, -40f, 40f), (shop, 32f, 50f) })
                {
                    var module = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/" + pair.Item1.Key.Split('.')[1] + ".prefab");
                    var child = (GameObject)PrefabUtility.InstantiatePrefab(module, root.transform);
                    child.transform.localPosition = new Vector3(pair.Item2 / 100, pair.Item3 / 100, 0);
                }
                PrefabUtility.SaveAsPrefabAsset(root, ShipAssetSetup.Prefab);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        public static void Validate(ObjectDefinitionDatabase database)
        {
            database.RebuildLookup();
            var ship = AssetDatabase.LoadAssetAtPath<GameObject>(ShipAssetSetup.Prefab);
            var anchors = ship.GetComponentsInChildren<ShipServiceAnchor>(true);
            if (anchors.Length != 2 || anchors.Select(a => a.Definition.Resolve(database)?.SharedConfigs
                    .OfType<ShipServiceConfig>().SingleOrDefault()?.Service).OrderBy(x => x)
                    .SequenceEqual(new[] { "sale", "shop" }) == false)
                throw new InvalidOperationException("飞船子终端绑定无效。");
            foreach (string key in new[] { "pistol", "pickaxe", "jetpack" })
                if (database.GetDefinitionByKey("item." + key)?.SharedConfigs.OfType<EquipmentItemConfig>().SingleOrDefault()?.RuleKey != key)
                    throw new InvalidOperationException("装备定义绑定无效：" + key);
        }
    }
}
