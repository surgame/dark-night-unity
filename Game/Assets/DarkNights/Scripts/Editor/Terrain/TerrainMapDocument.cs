using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Terrain;
using DarkNights.View.Terrain;
using UnityEngine;
using UnityEngine.Serialization;

namespace DarkNights.Editor.Terrain
{
    /// <summary>地图制作的独立作者文档；保存完整配方、同次冻结地图和私有表现副本，不依赖正式新局引用或玩家存档。</summary>
    public sealed class TerrainMapDocument : ScriptableObject
    {
        public int Version = 2;
        public ExpeditionFlowConfig Input;
        public ReviewedMineMap Map;
        public CaveTerrainStyle Style;
        public string PlanetId;
        [HideInInspector] public TextAsset DesignPreview;
        [HideInInspector] public string DesignPreviewSource;
        [HideInInspector] public string GenerationMetadata;
        [SerializeField, HideInInspector, FormerlySerializedAs("LegacyVeinCount")] private int importedVeinCount = 6;
        [SerializeField, HideInInspector, FormerlySerializedAs("LegacyNests")] private bool importedNests = true;
    }
}
