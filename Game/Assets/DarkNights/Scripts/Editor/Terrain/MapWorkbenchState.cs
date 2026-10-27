using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 地图工作台的本机导航、选择与叠层状态；随窗口重载保留，不拥有生成输入或业务地图。
    /// 编辑参数始终归ExpeditionFlowDraft；文件引用及冲突基线随窗口保留，不隐式装配新局。
    /// </summary>
    [Serializable]
    [MovedFrom(true, "DarkNights.Editor.Terrain", "DarkNights.Editor", "TerrainWorkbenchState")]
    internal sealed class MapWorkbenchState
    {
        public string Page = "recipe";
        public int ProductionStage = 7;
        public bool ProductionFlowExpanded;
        public bool HighlightStageChanges = true;
        public int SelectedRegion;
        public TerrainMapDocument Document;
        public DarkNights.Runtime.Terrain.ReviewedMineMap ViewedMap;
        public string MapSearch = "";
        public string DocumentBaseline = "";
        public string Extra = "nest";
        public string AdvancedPage = "compat";
        public bool Regions;
        public bool Grid;
        public bool LogicalGrid;
        public bool Connections;
        public bool Minerals = true;
        public bool Sites = true;
        public bool LandingMarkers;
        public bool RenderMode;
        public bool DesignScenery = true;
        public int Pick;
        public int SelectedNest;
        public string SelectedSite = "";
        public string CheckedIdentity = "";
        public bool ShowStudy;
        public bool ShowJourney;
        public byte FillMaterial = 2;
    }
}
