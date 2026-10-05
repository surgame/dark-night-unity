using System;
using System.Collections.Generic;
using System.Reflection;
using AnyRules.Next;
using DarkNights.Core.Config;
using DarkNights.Core.Config.Terrain;
using DarkNights.Entry;
using DarkNights.Entry.Terrain;
using DarkNights.Runtime.Network;
using Newtonsoft.Json;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>开始记录时冻结附近65列×23行的碰撞格和运动配置；限定体积、不引用状态池，用于离线定位与重放。</summary>
    internal static class HeroGroundTraceContext
    {
        internal static string Capture()
        {
            var boot = UnityEngine.Object.FindAnyObjectByType<TerrainDebugBootstrap>();
            var network = UnityEngine.Object.FindAnyObjectByType<SessionNetwork>();
            var player = UnityEngine.Object.FindAnyObjectByType<HeroPlayerController>();
            IReadOnlyGrid map;
            float x, height;
            HeroControlDefinition rules;
            if (boot?.Workshop != null)
            {
                map = boot.Workshop.Map; x = boot.Workshop.X * PlayableTerrain.CellPixels;
                height = PlayableTerrain.OriginY + boot.Workshop.Y * PlayableTerrain.CellPixels;
                rules = (HeroControlDefinition)typeof(DarkNights.Runtime.Terrain.CaveWorkshopSession)
                    .GetField("rules", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(boot.Workshop);
            }
            else
            {
                map = network.ObjectWorld.Terrain.Map; x = player.Current.X; height = player.Current.Height;
                rules = network.ObjectWorld.Catalog.Balance.HeroControl;
            }
            int center = (int)Math.Floor(x / PlayableTerrain.CellPixels + .5f);
            int row = (int)Math.Floor((PlayableTerrain.OriginY - height) / PlayableTerrain.CellPixels + .5f);
            int left = Math.Max(0, center - 32), right = Math.Min(TerrainGenerationSettings.Width - 1, center + 32);
            int top = Math.Max(0, row - 6), bottom = Math.Min(TerrainGenerationSettings.Height - 1, row + 16);
            var cells = new List<int[]>();
            for (int y = top; y <= bottom; y++) for (int u = left; u <= right; u++)
            {
                var sample = map.Read(new CellCoord(u, -y));
                cells.Add(sample.TryGetCell(out var cell) ? new[] { u, y, (int)cell.TileId, (int)cell.Flags } : new[] { u, y, -1, 0 });
            }
            return JsonConvert.SerializeObject(new { x, height, left, right, top, bottom, map.CommitId,
                cellPixels = PlayableTerrain.CellPixels, originY = PlayableTerrain.OriginY, rules,
                speedMultiplier = network?.ObjectWorld?.DebugHeroSpeedMultiplier ?? 1,
                workshop = boot?.Workshop != null, workshopJumpStrategy = boot?.Workshop?.JumpStrategy,
                workshopJetpack = boot?.Workshop?.JetpackEnabled, cells }, Formatting.None);
        }
    }
}
