using System;
using DarkNights.Core.Config.Terrain;
using GameCore.Objects.Definition;
using UnityEngine;

namespace DarkNights.Runtime.Objects
{
    /// <summary>现有航程草稿中的单个人工塌方点；定义引用提供已有资源与默认配方，坐标和允许改形／退避范围归本地图，不写回样板Definition。</summary>
    [Serializable]
    public sealed class GeologyDesignPoint
    {
        public bool Enabled;
        public string SiteId = "collapse-01";
        public ObjectDefinition Definition;
        public GeologyEventConfig Configuration = new GeologyEventConfig();
        public int AllowedU, AllowedV = -100, AllowedWidth = 16, AllowedHeight = 16;
        public int RetreatU, RetreatV = -100, RetreatWidth = 3, RetreatHeight = 3;
        public string Association = "";

        public string Identity => string.Join("|", Enabled, SiteId, Definition == null ? "" : Definition.Guid.ToString(),
            Definition == null ? "" : GeologySiteConfiguration.DefinitionHash(Definition), JsonUtility.ToJson(Configuration),
            AllowedU, AllowedV, AllowedWidth, AllowedHeight, RetreatU, RetreatV, RetreatWidth, RetreatHeight, Association);
        public GeologyDesignPoint Copy() => new GeologyDesignPoint
        {
            Enabled = Enabled, SiteId = SiteId, Definition = Definition,
            Configuration = JsonUtility.FromJson<GeologyEventConfig>(JsonUtility.ToJson(Configuration)),
            AllowedU = AllowedU, AllowedV = AllowedV, AllowedWidth = AllowedWidth, AllowedHeight = AllowedHeight,
            RetreatU = RetreatU, RetreatV = RetreatV, RetreatWidth = RetreatWidth, RetreatHeight = RetreatHeight, Association = Association
        };

        public TerrainGeologySite Freeze()
        {
            if (!Enabled || Definition == null || Configuration == null) throw new InvalidOperationException("塌方点未启用或缺少Definition／配置。");
            var config = JsonUtility.FromJson<GeologyEventConfig>(JsonUtility.ToJson(Configuration));
            config.EventId = SiteId;
            config.Freeze();
            return new TerrainGeologySite(SiteId, Definition.Guid.ToString(), GeologySiteConfiguration.DefinitionHash(Definition),
                JsonUtility.ToJson(config), Association, AllowedU, AllowedV, AllowedWidth, AllowedHeight,
                RetreatU, RetreatV, RetreatWidth, RetreatHeight);
        }
    }
}
