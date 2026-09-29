using System.Linq;
using GameCore.Objects.Definition;
using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>船体 Prefab 的可开关服务子物体；显式引用 YYGC Definition，局部锚点用于权威距离检查。</summary>
    public sealed class ShipServiceAnchor : MonoBehaviour
    {
        [SerializeField] private DefinitionReference definition;
        public DefinitionReference Definition => definition;
        public ShipServiceConfig Config => definition.Resolve()?.SharedConfigs.OfType<ShipServiceConfig>().SingleOrDefault();

#if UNITY_EDITOR
        public void EditorSetDefinition(ObjectDefinition value) =>
            definition = new DefinitionReference(value.Guid);
#endif
    }
}
