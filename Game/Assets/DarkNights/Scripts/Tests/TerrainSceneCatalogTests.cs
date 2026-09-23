using DarkNights.Editor.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>地形场景整理的编辑器合同；保留原 GUID，且工作台／测试场景不混入正式构建列表。</summary>
    public sealed class TerrainSceneCatalogTests
    {
        [Test]
        public void MovedScenesKeepTheirOriginalGuids()
        {
            string[,] entries = {
                { TerrainScenePaths.ReferenceChamber, "03daec6cbfa91d24fbe0c1324beff73a" },
                { TerrainScenePaths.RandomCave, "cdc43367998f149489238031f7fd1ff6" },
                { TerrainScenePaths.CaveExploration, "56096e47abdc4ed4ba4d0beb210ac3fc" },
                { TerrainScenePaths.TerrainDebugBootstrap, "078f8b061cf736f47ba98e7fd9f906dc" },
                { TerrainScenePaths.PendingCaveContourStatic, "83de985c436433b4993b518d1e2fee37" },
                { TerrainScenePaths.TerrainTest, "b61d003505f8eb249a5045fac4644372" },
                { TerrainScenePaths.TerrainNetworkTest, "b40a64f316d40b440b346310fc3b5f60" }
            };
            for (int i = 0; i < entries.GetLength(0); i++)
            {
                string path = entries[i, 0];
                Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(path), Is.Not.Null, path);
                Assert.That(AssetDatabase.AssetPathToGUID(path), Is.EqualTo(entries[i, 1]), path);
            }
            foreach (var scene in EditorBuildSettings.scenes)
                Assert.That(scene.path, Does.Not.StartWith("Assets/DarkNights/Res/Scenes/Workbenches/")
                    .And.Not.StartWith("Assets/DarkNights/Res/Scenes/Tests/")
                    .And.Not.StartWith("Assets/DarkNights/Res/Scenes/PendingDeletion/"));
        }

        [Test]
        public void FixedAndRandomWorkbenchesRetainBootstrapAndMapReferences()
        {
            foreach (string path in new[] { TerrainScenePaths.ReferenceChamber, TerrainScenePaths.RandomCave })
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,
                    UnityEditor.SceneManagement.OpenSceneMode.Additive);
                try
                {
                    var bootstraps = new System.Collections.Generic.List<DarkNights.Entry.Terrain.TerrainDebugBootstrap>();
                    foreach (GameObject root in scene.GetRootGameObjects())
                        bootstraps.AddRange(root.GetComponentsInChildren<DarkNights.Entry.Terrain.TerrainDebugBootstrap>(true));
                    Assert.That(bootstraps.Count, Is.EqualTo(1), path);
                    Assert.That(bootstraps[0].Definition, Is.Not.Null);
                    Assert.That(bootstraps[0].CaveStyle, Is.Not.Null);
                    Assert.That(bootstraps[0].FixedMap == null, Is.EqualTo(path == TerrainScenePaths.RandomCave));
                }
                finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
            }
        }

        [Test]
        public void OtherMovedScenesCanBeOpenedReadOnly()
        {
            string[] paths = { TerrainScenePaths.CaveExploration, TerrainScenePaths.TerrainDebugBootstrap,
                TerrainScenePaths.PendingCaveContourStatic, TerrainScenePaths.TerrainTest, TerrainScenePaths.TerrainNetworkTest };
            foreach (string path in paths)
            {
                var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(path,
                    UnityEditor.SceneManagement.OpenSceneMode.Additive);
                try
                {
                    Assert.That(scene.isLoaded, Is.True, path);
                    Assert.That(scene.GetRootGameObjects().Length, Is.GreaterThan(0), path);
                    Assert.That(scene.isDirty, Is.False, path);
                }
                finally { UnityEditor.SceneManagement.EditorSceneManager.CloseScene(scene, true); }
            }
        }
    }
}
