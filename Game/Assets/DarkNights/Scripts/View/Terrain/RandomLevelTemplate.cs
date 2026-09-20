using AnyRules.Next.Authoring;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>每关一个可编辑随机场景模板；显式绑定原生 DualGrid 配置，运行生成只替换地图，不反写人工场景。</summary>
    public sealed class RandomLevelTemplate : MonoBehaviour
    {
        public ARDMapDefinition Definition;
        public CaveTerrainStyle CaveStyle;
        public bool Expedition;
    }
}
