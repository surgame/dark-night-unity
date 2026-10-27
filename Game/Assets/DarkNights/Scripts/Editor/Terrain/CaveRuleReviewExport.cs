using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using GameCore.Objects.Definition;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>规则制作对照的有界导出入口；输出同生成器的五地图、九入口和七矿室，记录来源及失败。只写指定空产物目录，不安装地图或启动会话。</summary>
    public static class CaveRuleReviewExport
    {
        public static string Export(string directory)
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
                throw new InvalidOperationException("规则对照仅在编辑器就绪且非Play时生成。");
            string root = Path.GetFullPath(directory);
            if (Directory.Exists(root) && Directory.EnumerateFileSystemEntries(root).Any())
                throw new IOException("对照输出必须为新的空目录。");
            Directory.CreateDirectory(root);
            var source = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/WorldSession/WorldSession.asset");
            var original = source.SharedConfigs.OfType<ExpeditionFlowConfig>().Single();
            var config = ExpeditionFlowDraft.Clone(original);
            config.ReviewedMap = null;
            config.CaveMap.SeedSelection = new TerrainSeedSettings();
            config.CaveMap.Seed = "MG-REVIEW-20261021";
            config.CaveMap.Layout = TerrainGenerationSettings.ConnectedRooms;
            config.CaveMap.Structure = new CaveLayoutSettings();
            config.CaveMap.Minerals = new TerrainMineralSettings { Algorithm = TerrainMineralSettings.RegionalVeins, Seed = "MG-CONTENT-20261021" };
            config.CaveMap.Rules = null;
            var outputs = new JArray();
            foreach (string road in CaveLayoutSettings.Roads)
            {
                config.CaveMap.Structure.Road = road;
                var messages = new List<string>();
                var settings = config.FreezeCaveMap();
                var generated = PlanetTerrainGenerator.GenerateResult(config.PreviewPlanet(), settings.Seed,
                    TerrainGenerationPreview.WorldId, settings, contentReport: messages.Add);
                var plan = generated.Plan;
                var map = generated.Terrain;
                string json = ReviewedMineMap.Encode(map, CaveLayoutSettings.TemplateVersion + "|" + TerrainAuthoringIdentity.Json(settings));
                File.WriteAllText(Path.Combine(root, road + ".json"), json, new UTF8Encoding(false));
                WriteMap(Path.Combine(root, road + ".png"), map);
                var regions = new JArray();
                for (int i = 0; i < plan.Rooms.Count; i++)
                {
                    var room = plan.Rooms[i]; var ore = map.Deposits.Where(d => d.Id.StartsWith("rule-r" + i + "-")).ToArray();
                    int visible = 0, hidden = 0;
                    foreach (var deposit in ore) foreach (var cell in deposit.Cells)
                        if (map.Material(cell.U, -cell.V) == 0) visible += cell.Capacity; else hidden += cell.Capacity;
                    regions.Add(new JObject { ["room_id"] = i, ["shape"] = room.Kind, ["x"] = room.X, ["y"] = room.Y,
                        ["declared_distance"] = plan.Distances[i], ["branch_level"] = plan.BranchLevels[i],
                        ["capacity"] = ore.Sum(d => d.Capacity), ["exposed_capacity"] = visible, ["buried_capacity"] = hidden });
                }
                using var hash = SHA256.Create();
                outputs.Add(new JObject { ["road"] = road, ["input"] = TerrainAuthoringIdentity.Json(settings),
                    ["source_identity"] = ReviewedMineMap.SourceFingerprint(config),
                    ["sha256"] = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-", "").ToLowerInvariant(),
                    ["room_count"] = map.Rooms.Count, ["connections"] = plan.Connections.Count, ["nests"] = map.Nests.Count,
                    ["regions"] = regions, ["diagnostics"] = new JArray(plan.Diagnostics.Concat(messages)) });
            }
            WriteRooms(Path.Combine(root, "rooms.png"));
            WriteEntrances(Path.Combine(root, "entrances.png"), config);
            var summary = new JObject { ["kind"] = "authoring-preview", ["template_version"] = CaveLayoutSettings.TemplateVersion,
                ["unity"] = Application.unityVersion, ["original_source_identity"] = ReviewedMineMap.SourceFingerprint(original),
                ["outputs"] = outputs, ["boundaries"] = "制作生成数据对照；未执行自动测试、人物通行、Play、Player、联机或存档恢复，未安装新局引用。" };
            string path = Path.Combine(root, "summary.json");
            File.WriteAllText(path, summary.ToString(Formatting.Indented), new UTF8Encoding(false));
            return path;
        }

        private static void WriteMap(string path, PlayableTerrain map)
        {
            int w = TerrainGenerationSettings.Width, h = TerrainGenerationSettings.Height;
            var pixels = MapPixels(map);
            foreach (var deposit in map.Deposits) foreach (var cell in deposit.Cells)
                pixels[(h - 1 + cell.V) * w + cell.U] = deposit.Rarity == "rare" ? new Color32(255, 200, 50, 255) :
                    map.Material(cell.U, -cell.V) == 0 ? new Color32(70, 230, 140, 255) : new Color32(80, 145, 180, 255);
            foreach (var nest in map.Nests)
                for (int y = nest.Row - 2; y <= nest.Row; y++) for (int x = nest.U; x <= nest.U + 4; x++)
                    pixels[(h - 1 - y) * w + x] = new Color32(230, 75, 85, 255);
            Write(path, w, h, pixels);
        }

        private static Color32[] MapPixels(PlayableTerrain map)
        {
            int w = TerrainGenerationSettings.Width, h = TerrainGenerationSettings.Height;
            var cells = map.CopyMaterials(); var protection = map.CopyProtection(); var pixels = new Color32[cells.Length];
            for (int y = 0; y < h; y++) for (int x = 0; x < w; x++)
            {
                int i = y * w + x;
                pixels[(h - 1 - y) * w + x] = cells[i] == 0 ? new Color32(18, 23, 30, 255) :
                    protection[i] ? new Color32(90, 90, 105, 255) : new Color32(111, 102, 83, 255);
            }
            return pixels;
        }

        private static void WriteRooms(string path)
        {
            const int tile = 48, w = tile * 4, h = tile * 2;
            var pixels = Enumerable.Repeat(new Color32(30, 34, 41, 255), w * h).ToArray();
            for (int n = 0; n < CaveLayoutSettings.Shapes.Length; n++)
            {
                var cells = Enumerable.Repeat((byte)2, 320 * 192).ToArray();
                var protection = new bool[cells.Length];
                var room = new TerrainRoom(CaveLayoutSettings.Shapes[n], 100, 90, n == 3 ? 20 : 36, n == 0 ? 14 : 34);
                CaveRuleGeometry.Room(cells, protection, room, n);
                for (int y = 0; y < tile; y++) for (int x = 0; x < tile; x++)
                    pixels[(h - 1 - (n / 4 * tile + y)) * w + n % 4 * tile + x] = cells[(90 + y - 24) * 320 + 100 + x - 24] == 0 ?
                        new Color32(18, 23, 30, 255) : new Color32(111, 102, 83, 255);
            }
            Write(path, w, h, pixels);
        }

        private static void WriteEntrances(string path, ExpeditionFlowConfig config)
        {
            const int tileW = 112, tileH = 72, w = tileW * 3, h = tileH * 3;
            var pixels = new Color32[w * h];
            for (int n = 0; n < CaveLayoutSettings.Entrances.Length; n++)
            {
                var cells = Enumerable.Repeat((byte)2, 320 * 192).ToArray(); var protection = new bool[cells.Length];
                for (int y = 0; y < 40; y++) for (int x = 0; x < 320; x++) cells[y * 320 + x] = 0;
                var rules = config.CaveMap.Structure.CopyValidated(); rules.Entrance = CaveLayoutSettings.Entrances[n];
                CaveRuleEntrance.Carve(cells, protection, rules, new TerrainRoom("low-seam", 104, 77, 28, 16),
                    Enumerable.Repeat(40, TerrainGenerationSettings.Width).ToArray());
                for (int y = 0; y < tileH; y++) for (int x = 0; x < tileW; x++)
                    pixels[(h - 1 - (n / 3 * tileH + y)) * w + n % 3 * tileW + x] = cells[(y + 18) * 320 + x + 28] == 0 ?
                        new Color32(18, 23, 30, 255) : new Color32(111, 102, 83, 255);
            }
            Write(path, w, h, pixels);
        }

        private static void Write(string path, int width, int height, Color32[] pixels)
        {
            var image = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
            try { image.SetPixels32(pixels); image.Apply(); File.WriteAllBytes(path, image.EncodeToPNG()); }
            finally { UnityEngine.Object.DestroyImmediate(image); }
        }
    }
}
