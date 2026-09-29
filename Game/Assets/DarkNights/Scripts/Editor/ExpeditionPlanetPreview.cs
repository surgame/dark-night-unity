using System;
using System.Threading;
using System.Threading.Tasks;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor
{
    /// <summary>
    /// 编辑态星球蓝图缩略图；直接调用纯生成器，按原生格子显示地表、矿床、保护平台和到达点。
    /// 不创建场景对象、不启动会话、不读写玩家存档；纹理由面板生命周期显式释放。
    /// </summary>
    public sealed class ExpeditionPlanetPreview : IDisposable
    {
        private Texture2D texture;
        private CancellationTokenSource cancellation;
        private Task<PlayableTerrain> pending;
        private PlanetDefinition selected;
        private Action<string> report;
        private double deadline;
        internal Image Surface { get; } = new Image { scaleMode = ScaleMode.ScaleToFit };

        internal ExpeditionPlanetPreview()
        {
            Surface.style.height = 260;
            Surface.style.minHeight = 180;
            Surface.style.flexGrow = 1;
            EditorApplication.update += Tick;
        }

        internal void Generate(PlanetDefinition planet, Action<string> completed, TerrainGenerationSettings template = null,
            TerrainGenerationPipeline modifiers = null)
        {
            Clear();
            var settings = (template ?? new TerrainGenerationSettings { ResourceProfile = TerrainGenerationSettings.CaveExplorationProfile }).CopyValidated();
            string seed = string.IsNullOrEmpty(planet.Seed) ? settings.Seed : planet.Seed;
            const string worldId = "00000000000000000000000000000001";
            cancellation = new CancellationTokenSource();
            CancellationToken token = cancellation.Token;
            selected = planet; report = completed; deadline = EditorApplication.timeSinceStartup + 15;
            pending = Task.Run(() => PlanetTerrainGenerator.GenerateCandidate(planet, seed, worldId, settings,
                () => token.IsCancellationRequested, modifiers), token);
        }

        private void Tick()
        {
            if (pending == null) return;
            if (EditorApplication.timeSinceStartup > deadline)
            {
                var callback = report; CancelPending();
                callback?.Invoke("蓝图预览超过 15 秒，已取消；可调整配置后重新生成。"); return;
            }
            if (!pending.IsCompleted) return;
            Task<PlayableTerrain> result = pending;
            var planet = selected; var completed = report;
            pending = null; selected = null; report = null;
            cancellation.Dispose(); cancellation = null;
            if (result.IsFaulted || result.IsCanceled)
            {
                completed?.Invoke("预览失败：" + (result.Exception?.GetBaseException().Message ?? "生成已取消")); return;
            }
            try { completed?.Invoke(Render(result.Result, planet)); }
            catch (Exception error) { completed?.Invoke("预览失败：" + error.Message); }
        }

        private string Render(PlayableTerrain terrain, PlanetDefinition planet)
        {
            const int width = TerrainGenerationSettings.Width;
            const int height = TerrainGenerationSettings.Height;
            var cells = terrain.CopyMaterials();
            var protection = terrain.CopyProtection();
            var pixels = new Color32[cells.Length];
            for (int row = 0; row < height; row++)
            {
                for (int x = 0; x < width; x++)
                {
                    int source = row * width + x;
                    pixels[(height - row - 1) * width + x] = protection[source] ? new Color32(73, 147, 154, 255) :
                        cells[source] == 0 ? new Color32(20, 28, 45, 255) : new Color32(111, 91, 74, 255);
                }
            }
            foreach (var deposit in terrain.Deposits)
                pixels[(height - deposit.Y - 1) * width + deposit.X] = new Color32(234, 168, 69, 255);
            int arrival = planet.DockRow - Mathf.RoundToInt(planet.ArrivalHeight / PlayableTerrain.CellPixels);
            for (int x = planet.DockColumn - 2; x <= planet.DockColumn + 2; x++)
                pixels[(height - arrival - 1) * width + x] = new Color32(148, 220, 250, 255);
            ReleaseTexture();
            texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            { name = "星球静态蓝图预览", filterMode = FilterMode.Point, hideFlags = HideFlags.HideAndDontSave };
            texture.SetPixels32(pixels); texture.Apply(false, false);
            Surface.image = texture;
            return "蓝图预览 · 种子 " + terrain.Seed + " · 矿床 " + terrain.Deposits.Count + "\n青色：保护地面；金色：矿床；亮蓝：到达点。此图不代替运行／画面验收。";
        }

        internal void Clear()
        {
            CancelPending();
            Surface.image = null;
            ReleaseTexture();
        }

        private void CancelPending()
        {
            cancellation?.Cancel(); cancellation?.Dispose(); cancellation = null;
            if (pending != null)
                _ = pending.ContinueWith(task => { _ = task.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
            pending = null; selected = null; report = null;
        }

        private void ReleaseTexture()
        {
            if (texture == null) return;
            UnityEngine.Object.DestroyImmediate(texture);
            texture = null;
        }

        public void Dispose()
        {
            EditorApplication.update -= Tick;
            Clear();
        }
    }
}
