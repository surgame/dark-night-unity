using DarkNights.View.Terrain;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using UnityEngine;

namespace DarkNights.Entry.Terrain
{
    /// <summary>功能场景对地质事件的明确装配引用；定义、原生ObjectInstance与视图来自场景，不按节点名、子索引或GetComponent补齐缺失绑定。</summary>
    public sealed class GeologyLabEventSlot : MonoBehaviour
    {
        public ObjectDefinition Definition;
        public ObjectInstance Instance;
        public GeologyEventView View;
    }
}
