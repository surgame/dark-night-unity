using System;
using System.Linq;
using DarkNights.Runtime.Framework;
using DarkNights.View;
using GameCore.Editor.Objects.Definition;
using GameCore.Objects.Definition;
using GameCore.Objects.Types;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>机器人和无人机首版 Prefab 装配；复用原生角色绑定和动画能力，替换显示精灵而不克隆定义身份。</summary>
    internal static class ShipUnitAssetSetup
    {
        internal static void Install(ObjectDefinitionDatabase database, DefinitionRuleIndex index)
        {
            foreach (bool drone in new[] { false, true })
            {
                string key = drone ? "scout-drone" : "hauler";
                string path = ShipAssetSetup.Root + "/" + key + ".prefab";
                var root = PrefabUtility.LoadPrefabContents("Assets/DarkNights/Res/Objects/Worker/Worker.prefab");
                try
                {
                    root.name = key; var actor = root.GetComponent<ActorView>(); var data = new SerializedObject(actor);
                    var unit = root.AddComponent<ExpeditionUnitView>(); unit.Drone = drone;
                    unit.Legacy = root.GetComponentsInChildren<SpriteRenderer>(true);
                    var node = new GameObject("Native body"); node.transform.SetParent((Transform)data.FindProperty("facing").objectReferenceValue, false);
                    unit.Body = node.AddComponent<SpriteRenderer>(); unit.Body.sharedMaterial = unit.Legacy.First().sharedMaterial;
                    unit.Frames = ShipAssetSetup.Frames(drone ? "drone_hover" : "robot_walk", drone ? 3 : 4);
                    unit.Body.sprite = unit.Frames[0]; unit.Body.sortingOrder = 0;
                    data.FindProperty("expeditionUnit").objectReferenceValue = unit;
                    data.FindProperty("portrait").objectReferenceValue = unit.Frames[0];
                    data.FindProperty("pickBounds").rectValue = drone ? new Rect(-.08f, -.06f, .16f, .12f) : new Rect(-.06f, 0, .12f, .16f);
                    ShipAssetSetup.SetTint(data, unit.Legacy.Concat(new[] { unit.Body }).ToArray()); data.ApplyModifiedPropertiesWithoutUndo();
                    unit.Present(null); PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
                ObjectDefinition definition;
                if (drone)
                {
                    definition = ScriptableObject.CreateInstance<ObjectDefinition>();
                    definition.Name = "侦察无人机"; definition.NetType = NetworkType.Local; definition.Type = ObjectType.Unit;
                    AssetDatabase.CreateAsset(definition, ShipAssetSetup.Root + "/scout-drone.asset");
                    definition.EditorSetIdentity(DefinitionIdentityAuthoring.ReadAssetGuid(definition), "expedition.scout-drone", false);
                    definition.BehaviourTypes.Add(typeof(ActorPresentationBehaviour).FullName);
                    ObjectCapabilitySetup.ConfigureLocal(definition, key); database.AddDefinition(definition);
                }
                else definition = index.GetRequired(key);
                ShipAssetSetup.Assign(definition, path);
                var check = PrefabUtility.LoadPrefabContents(path);
                try
                { if (check.GetComponent<ExpeditionUnitView>().Frames.Any(s => s == null)) throw new InvalidOperationException("单位帧引用丢失。"); }
                finally { PrefabUtility.UnloadPrefabContents(check); }
            }
        }
    }
}
