using System;
using DarkNights.View.Terrain;
using UnityEngine;

namespace DarkNights.Entry.Terrain
{
    /// <summary>工作台唯一的编辑器资产桥；Player 不包含 AssetDatabase，保存按钮仅在编辑器注册保存能力后出现。</summary>
    public static class TerrainWorkbenchAssets
    {
        public static Func<Type, ScriptableObject[]> Query;
        public static Action<CaveStyleDraft> SaveStyle;
        public static Action<TerrainMapAsset, byte[], byte[]> SaveMap;
    }
}
