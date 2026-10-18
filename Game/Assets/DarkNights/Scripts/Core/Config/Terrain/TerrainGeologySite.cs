using System;

namespace DarkNights.Core.Config.Terrain
{
    /// <summary>随完整地图冻结的人工塌方站点；身份、定义摘要和配置文本只读，不含世界代次或事件阶段。配置由Runtime适配解析并在导出、装配和恢复前校验。</summary>
    public sealed class TerrainGeologySite
    {
        public string SiteId { get; }
        public string DefinitionGuid { get; }
        public string DefinitionHash { get; }
        public string Configuration { get; }
        public string Association { get; }
        public int AllowedU { get; }
        public int AllowedV { get; }
        public int AllowedWidth { get; }
        public int AllowedHeight { get; }
        public int RetreatU { get; }
        public int RetreatV { get; }
        public int RetreatWidth { get; }
        public int RetreatHeight { get; }

        public TerrainGeologySite(string siteId, string definitionGuid, string definitionHash, string configuration,
            string association, int allowedU, int allowedV, int allowedWidth, int allowedHeight,
            int retreatU, int retreatV, int retreatWidth, int retreatHeight)
        {
            if (string.IsNullOrWhiteSpace(siteId) || siteId.Length > 64 || !Guid.TryParse(definitionGuid, out var guid) || guid == Guid.Empty ||
                definitionHash == null || definitionHash.Length != 64 || configuration == null || configuration.Length > 8192 ||
                association == null || association.Length > 128 || !Bounds(allowedU, allowedV, allowedWidth, allowedHeight) ||
                !Bounds(retreatU, retreatV, retreatWidth, retreatHeight) || retreatWidth < 3 || retreatHeight < 3)
                throw new ArgumentException("塌方站点身份、范围或配置无效。");
            foreach (char c in definitionHash) if (!Uri.IsHexDigit(c)) throw new ArgumentException("塌方定义摘要无效。");
            SiteId = siteId; DefinitionGuid = definitionGuid; DefinitionHash = definitionHash;
            Configuration = configuration; Association = association;
            AllowedU = allowedU; AllowedV = allowedV; AllowedWidth = allowedWidth; AllowedHeight = allowedHeight;
            RetreatU = retreatU; RetreatV = retreatV; RetreatWidth = retreatWidth; RetreatHeight = retreatHeight;
        }

        private static bool Bounds(int u, int v, int width, int height) => u >= 0 && v > -TerrainGenerationSettings.Height &&
            width > 0 && height > 0 && width <= TerrainGenerationSettings.Width && height <= TerrainGenerationSettings.Height &&
            u <= TerrainGenerationSettings.Width - width && v <= 1 - height;
    }
}
