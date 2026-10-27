using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Runtime.Terrain;
using UnityEditor;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 地图目录的一条只读资源记录；编号来自作者身份或稳定资源GUID，文件名和路径保留真实来源。
    /// 每次工程资源变化后重新发现Assets内全部地图，不按文件夹、历史批次或固定数量截断。
    /// </summary>
    internal sealed class MapWorkbenchMapFile
    {
        internal UnityEngine.Object Asset;
        internal string Id, Title, Path, Kind, SearchText;

        internal static List<MapWorkbenchMapFile> Find()
        {
            var files = new List<MapWorkbenchMapFile>();
            var paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (string type in new[] { "TerrainMapDocument", "ReviewedMineMap" })
            foreach (string guid in AssetDatabase.FindAssets("t:" + type, new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!paths.Add(path)) continue;
                var asset = AssetDatabase.LoadAssetAtPath<UnityEngine.Object>(path);
                string id = guid.Substring(0, 8), title = asset != null ? asset.name : System.IO.Path.GetFileNameWithoutExtension(path);
                string kind, seed = "";
                if (asset is TerrainMapDocument document)
                {
                    kind = "地图配置";
                    seed = document.Input?.CaveMap?.Seed ?? "";
                }
                else if (asset is ReviewedMineMap)
                {
                    kind = "地图结果 · 只读";
                    title = System.IO.Path.GetFileName(System.IO.Path.GetDirectoryName(path)) + " / " + title;
                }
                else continue;
                files.Add(new MapWorkbenchMapFile
                {
                    Asset = asset, Id = id, Title = title, Path = path, Kind = kind,
                    SearchText = id + " " + guid + " " + title + " " + path + " " + kind + " " + seed
                });
            }
            return files.OrderBy(x => x.Asset is TerrainMapDocument ? 0 : 1)
                .ThenBy(x => x.Title, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.Path, StringComparer.Ordinal).ToList();
        }

        internal bool Matches(string query)
        {
            return (query ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries)
                .All(term => SearchText.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0);
        }

        internal bool Selected(MapWorkbenchWindow owner)
        {
            if (owner.State.ViewedMap != null) return Asset == owner.State.ViewedMap;
            return owner.State.Document != null && Asset == owner.State.Document;
        }

        internal void Open(MapWorkbenchWindow owner)
        {
            if (Selected(owner)) return;
            if (Asset is TerrainMapDocument document) MapWorkbenchFiles.Open(owner, document);
            else if (Asset is ReviewedMineMap frozen) MapWorkbenchFiles.ViewFrozen(owner, frozen);
            else MapWorkbenchFiles.Import(owner, Asset);
        }
    }
}
