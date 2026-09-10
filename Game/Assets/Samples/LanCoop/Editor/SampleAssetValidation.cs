using System;
using System.IO;
using FishNet.Object;
using GameCore.Objects.Runner;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DarkNights.Samples.LanCoop.Editor
{
    /// <summary>验证独立样板的原生资产引用及编辑往返；仅修改临时副本，源 Prefab 和正式场景保持原样。</summary>
    public static class SampleAssetValidation
    {
        public static void Run()
        {
            string original = SampleBuilder.Root + "/SharedWorksite.prefab";
            string temporary = SampleBuilder.Root + "/__Roundtrip.prefab";
            if (File.Exists(temporary)) throw new InvalidOperationException("Validation temporary asset already exists");
            var scene = EditorSceneManager.OpenScene(SampleBuilder.ScenePath);
            if (scene.rootCount < 4) throw new InvalidOperationException("Sample scene layout is missing");
            if (!AssetDatabase.CopyAsset(original, temporary)) throw new InvalidOperationException("Cannot copy Prefab");
            try
            {
                var editable = PrefabUtility.LoadPrefabContents(temporary);
                try
                {
                    var instance = editable.GetComponent<ObjectInstance>();
                    if (instance.ObjectView == null || instance.ObjectView.gameObject != editable)
                        throw new InvalidOperationException("ObjectInstance/ObjectView binding missing");
                    editable.transform.Find("ArtOffset").localPosition = new Vector3(0, .75f, 0);
                    PrefabUtility.SaveAsPrefabAsset(editable, temporary);
                }
                finally { PrefabUtility.UnloadPrefabContents(editable); }
                var reopened = PrefabUtility.LoadPrefabContents(temporary);
                try
                {
                    if (reopened.transform.Find("ArtOffset").localPosition.y != .75f)
                        throw new InvalidOperationException("Prefab authoring roundtrip failed");
                }
                finally { PrefabUtility.UnloadPrefabContents(reopened); }
                foreach (string name in new[] { "CampSession", "OwnedEndpoint" })
                    if (AssetDatabase.LoadAssetAtPath<GameObject>(SampleBuilder.Root + "/" + name + ".prefab")
                        .GetComponent<NetworkObject>() == null) throw new InvalidOperationException("Missing NetworkObject");
                Directory.CreateDirectory("../artifacts/lan-sample");
                File.WriteAllText("../artifacts/lan-sample/asset-validation.txt",
                    "PASS: scene reopened; ObjectInstance/ObjectView bound; Prefab copy edited, saved and reopened; both network Prefabs valid.");
                Debug.Log("LAN_SAMPLE_ASSET_VALIDATION_PASS");
            }
            finally { AssetDatabase.DeleteAsset(temporary); }
        }
    }
}
