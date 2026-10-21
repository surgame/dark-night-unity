using System;
using DarkNights.Runtime.Terrain;
using UnityEngine;

namespace DarkNights.Editor.Terrain
{
    /// <summary>
    /// 地图工作台的本机导航、选择与叠层状态；随窗口重载保留，不拥有生成输入或业务地图。
    /// 作者参数始终归ExpeditionFlowDraft，冻结版本引用仅决定查看对象，绝不隐式装配新局。
    /// </summary>
    [Serializable]
    internal sealed class TerrainWorkbenchState
    {
        public string Page = "source";
        public string Extra = "nest";
        public string AdvancedPage = "compat";
        public bool CandidateMode;
        public bool ViewInstalled = true;
        public bool Regions;
        public bool Grid;
        public bool LogicalGrid;
        public bool Connections;
        public bool Minerals;
        public bool Sites = true;
        public bool LandingMarkers;
        public int Pick;
        public int SelectedNest;
        public string SelectedSite = "";
        public string CheckedIdentity = "";
        public ReviewedMineMap Version;
        public ReviewedMineMap FrozenVersion;
        public MineMapAsset PreviousAuthor;
        public bool UseForNewGame;
        public bool ShowStudy;
        public bool ShowJourney;
        public byte FillMaterial = 2;
    }
}
