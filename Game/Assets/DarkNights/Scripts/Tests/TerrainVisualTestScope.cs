using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using DarkNights.Core.Config.Terrain;
using DarkNights.Editor.Terrain;
using DarkNights.View.Terrain;
using AnyRules.Next;
using AnyRules.Next.Unity;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkNights.Tests
{
    /// <summary>真实离屏地形验收夹具；只修改独立蓝图和临时样式，读取实际 GPU 输出，退出销毁自己拥有的预览资源。</summary>
    internal sealed class TerrainVisualTestScope : IDisposable
    {
        internal const string Root = "Assets/DarkNights/Res/Terrain/StrataCave/";
        internal readonly TerrainStylePreviewStage Stage = new TerrainStylePreviewStage();
        internal readonly TerrainStyleDrafts Drafts = new TerrainStyleDrafts();
        internal readonly TerrainMapAsset Map;
        internal readonly CaveTerrainStyle Style;
        internal readonly TerrainBlueprint Blueprint;
        internal readonly byte[] OriginalBytes;
        internal TerrainPreview Preview => Read<TerrainPreview>(Stage, "terrain");
        internal ARDMapController Controller => Read<ARDMapController>(Preview, "controller");
        internal CaveVisualSource Visual => Read<CaveVisualSource>(Preview, "caveSource");
        internal readonly List<GridChangeSet> Changes = new List<GridChangeSet>();
        internal static readonly string Output = Path.GetFullPath("../artifacts/terrain-final-20260926");
        internal TerrainVisualTestScope(bool legacy = false, float budget = 4)
        {
            Map = AssetDatabase.LoadAssetAtPath<TerrainMapAsset>(Root + "ReferenceChamber.asset");
            Style = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(Root + "Style.asset"));
            Style.ImmediateForeground = !legacy; Style.InteractiveBakeBudgetMs = budget;
            Blueprint = Map.ReadBlueprint(); OriginalBytes = (byte[])Map.InitialCells.bytes.Clone();
            Stage.Open(Map, Blueprint, Style, Drafts, Blueprint.CopyMaterials(), Blueprint.CopyShapes(),
                new BackgroundBakeDescriptor(Guid.NewGuid().ToString("N"), Blueprint.Settings.Seed,
                    Blueprint.CopyMaterials(), Blueprint.CopyShapes()));
        }
        internal IEnumerator Settle()
        {
            double deadline = EditorApplication.timeSinceStartup + 20;
            do
            {
                Stage.Tick(); Assert.That(Stage.Error, Is.Null, Stage.Progress);
                if (Stage.Ready) yield break;
                yield return null;
            } while (EditorApplication.timeSinceStartup < deadline);
            Assert.Fail("Terrain did not settle: " + Stage.Progress);
        }
        internal void Observe() { Changes.Clear(); Controller.Logic.Changed += OnChanged; }
        private void OnChanged(GridChangeSet change) => Changes.Add(change);
        internal void Apply(int x, int row, byte value, byte shape = 0) =>
            Stage.Source.ApplyChanges(new[] { new TerrainBlueprintCellChange(x, row, value, shape) });
        internal static T Read<T>(object owner, string field) =>
            (T)owner.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
        internal long SolvedCells => (long)Read<object>(Controller.Renderer, "_pipeline").GetType()
            .GetProperty("SolvedCells").GetValue(Read<object>(Controller.Renderer, "_pipeline"));
        internal static byte[] ReadGpu(Texture texture, RectInt rect)
        {
            Assert.That(SystemInfo.supportsAsyncGPUReadback, Is.True, "A real GPU is required for this acceptance.");
            var request = AsyncGPUReadback.Request(texture, 0, rect.x, rect.width, rect.y, rect.height, 0, 1);
            request.WaitForCompletion(); Assert.That(request.hasError, Is.False);
            return request.GetData<byte>().ToArray();
        }
        internal void AssertRockMatchesCpu(RectInt rect)
        {
            object rock = Read<object>(Visual, "localRockSurface");
            object geometry = Read<object>(rock, "geometry");
            var bake = (Func<byte[]>)geometry.GetType().GetMethod("CaptureRegionBake")
                .Invoke(geometry, new object[] { rect, null });
            CollectionAssert.AreEqual(bake(), ReadGpu(Visual.Material.GetTexture("_RockSurface"), rect), "GPU rock patch differs from same-input CPU bake");
        }
        internal void SaveImage(string name)
        {
            var texture = new Texture2D(Stage.Image.width, Stage.Image.height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = (RenderTexture)Stage.Image;
                texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0); texture.Apply();
                Directory.CreateDirectory(Output); File.WriteAllBytes(Path.Combine(Output, name), texture.EncodeToPNG());
            }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
        }
        public void Dispose()
        {
            if (Preview != null && Controller != null) Controller.Logic.Changed -= OnChanged;
            Stage.Dispose(); Drafts.Dispose(); UnityEngine.Object.DestroyImmediate(Style);
            CollectionAssert.AreEqual(OriginalBytes, Map.InitialCells.bytes, "Source asset was modified by a preview test.");
        }
    }
}
