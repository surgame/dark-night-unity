using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>CL02效果的固定资源位置及混合版制作所需标记；路径供现有原生装配和定向用例复用，不再提供旧版首建或覆盖作者资源的入口。</summary>
    public static class GeologyLabAssets
    {
        public const string EffectsRoot = "Assets/DarkNights/Res/Effects/Geology";
        public const string Root = EffectsRoot + "/Event";
        public const string PrefabPath = Root + "/GeologyEvent.prefab";

        internal static SpriteRenderer Marker(string name, Transform parent, Sprite sprite, Color color)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            var art = root.AddComponent<SpriteRenderer>();
            art.sprite = sprite;
            art.color = color;
            art.sortingOrder = -20;
            return art;
        }
    }
}
