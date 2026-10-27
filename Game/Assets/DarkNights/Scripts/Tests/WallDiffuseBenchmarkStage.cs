using System;
using System.IO;
using System.Linq;
using System.Reflection;
using AnyRules.Next.Authoring;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Lighting;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;
using DarkNights.Editor.Terrain;
using DarkNights.Runtime.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>性能与画面对照的单一临时地形宿主；使用正式作者地图和材质，确定性选受光姿态，所有编辑只进入隔离预览，不保存作者资产或场景。</summary>
    public sealed class WallDiffuseBenchmarkStage : IDisposable
    {
        private readonly TerrainStylePreviewStage stage = new TerrainStylePreviewStage();
        private readonly TerrainStyleDrafts drafts = new TerrainStyleDrafts();
        public Camera Camera { get; private set; }
        public TerrainPreview Terrain { get; private set; }
        public Vector2 Mouth { get; private set; }
        public float Angle { get; private set; }
        public bool Ready => stage.Ready;
        public Exception Error => stage.Error;
        public TerrainBlueprint Blueprint { get; private set; }

        public void Open(string map)
        {
            Blueprint = DarkNights.Editor.Lighting.LightingPreviewMap.Load(map);
            Pose();
            const string root = "Assets/DarkNights/Res/Terrain/StrataCave/";
            stage.Open(AssetDatabase.LoadAssetAtPath<ARDMapDefinition>(root + "MapDefinition.asset"), Blueprint,
                AssetDatabase.LoadAssetAtPath<CaveTerrainStyle>(root + "Style.asset"), drafts, Blueprint.CopyMaterials(), Blueprint.CopyShapes(),
                new BackgroundBakeDescriptor(Guid.NewGuid().ToString("N"), Blueprint.Settings.Seed, Blueprint.CopyMaterials(), Blueprint.CopyShapes()));
            Camera = Read<Camera>("camera"); Terrain = Read<TerrainPreview>("terrain");
            ResetCamera(); Camera.orthographicSize = 6;
        }

        public void ResetCamera() => Camera.transform.position = new Vector3(Mouth.x - .5f, -Mouth.y + .5f, -10);
        public void Tick() { stage.Tick(); }
        public void Region(int frame)
        {
            Camera.transform.position = new Vector3(Mouth.x - .5f + (frame % 2 == 0 ? 0 : 8), -Mouth.y + .5f, -10);
            Terrain.TickFromEditor();
        }

        public void Dig(int frame)
        {
            int x = Mathf.Clamp(Mathf.FloorToInt(Mouth.x + Mathf.Cos(Angle * Mathf.Deg2Rad) * 2.1f), 0, 319);
            int y = Mathf.Clamp(Mathf.FloorToInt(Mouth.y - Mathf.Sin(Angle * Mathf.Deg2Rad) * 2.1f), 0, 191);
            byte original = Blueprint.MaterialAt(x, y), shape = (byte)Blueprint.ShapeAt(x, y);
            if (original == 0) throw new InvalidOperationException("挖墙姿态未命中岩体。");
            stage.Source.ApplyChanges(new[] { new TerrainBlueprintCellChange(x, y, frame % 2 == 0 ? (byte)0 : original, frame % 2 == 0 ? (byte)0 : shape) });
            Terrain.TickFromEditor();
        }

        private void Pose()
        {
            var room = Blueprint.Rooms.First();
            int centerX = room.X, centerY = room.Y;
            int x0 = Mathf.Clamp(centerX - 32, 0, 256), y0 = Mathf.Clamp(centerY - 32, 0, 128);
            var cells = new byte[64 * 64];
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++)
                cells[y * 64 + x] = (byte)(128 | (Blueprint.MaterialAt(x0 + x, y0 + y) == 0 ? 0 : 64) | (int)Blueprint.ShapeAt(x0 + x, y0 + y));
            var boundary = LightBoundaryBuilder.Build(new LightGeometrySnapshot(64, 64, cells), null); float best = float.MaxValue;
            for (int i = 0; i < boundary.Count; i++)
            {
                var edge = boundary[i]; var normal = new Vector2(edge.Nx, edge.Ny);
                var mid = new Vector2((edge.Ax + edge.Bx) * .5f + x0, (edge.Ay + edge.By) * .5f + y0);
                var candidate = mid + normal * 2;
                if (Blueprint.MaterialAt((int)candidate.x, (int)candidate.y) != 0 || Vector2.Distance(new Vector2(edge.Ax, edge.Ay), new Vector2(edge.Bx, edge.By)) < 1) continue;
                float distance = Vector2.Distance(candidate, new Vector2(centerX, centerY));
                if (distance >= best) continue;
                best = distance; Mouth = candidate; Angle = Mathf.Atan2(normal.y, -normal.x) * Mathf.Rad2Deg;
            }
            if (best == float.MaxValue) throw new InvalidOperationException("作者区域没有可重放的受光墙段。");
        }

        public static Color[] Pixels(Texture target)
        {
            var texture = new Texture2D(target.width, target.height, TextureFormat.RGBAFloat, false, true);
            var previous = RenderTexture.active;
            try { RenderTexture.active = (RenderTexture)target; texture.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0); texture.Apply(); return texture.GetPixels(); }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(texture); }
        }

        public void Save(string path)
        {
            Camera.Render(); var image = new Texture2D(stage.Image.width, stage.Image.height, TextureFormat.RGBA32, false);
            var previous = RenderTexture.active;
            try { RenderTexture.active = (RenderTexture)stage.Image; image.ReadPixels(new Rect(0, 0, image.width, image.height), 0, 0); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); }
            finally { RenderTexture.active = previous; UnityEngine.Object.DestroyImmediate(image); }
        }

        private T Read<T>(string member) => (T)stage.GetType().GetField(member, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(stage);
        public void Dispose() { stage.Dispose(); drafts.Dispose(); }
    }
}
