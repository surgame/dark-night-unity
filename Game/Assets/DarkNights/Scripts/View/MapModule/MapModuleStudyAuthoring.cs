using System;
using System.Linq;
using UnityEngine;

namespace DarkNights.View.MapModule
{
    /// <summary>MapModule试验场的地图布局目录；选择归工作台本机状态，一次运行只冻结一个布局，共用正式场景装配。</summary>
    public sealed class MapModuleStudyAuthoring : MonoBehaviour
    {
        public string StudyId = "entrance";
        public string DisplayName = "洞口与浅厅";
        public MapModuleCaseAuthoring[] Cases = Array.Empty<MapModuleCaseAuthoring>();
        public MapModuleCaseAuthoring RequiredCase(string id)
        {
            if (string.IsNullOrWhiteSpace(StudyId) || Cases == null || Cases.Length == 0 || Cases.Any(c => c == null) ||
                Cases.Select(c => c.CaseId).Distinct().Count() != Cases.Length)
                throw new InvalidOperationException("地图配置或布局列表无效，请检查名称和布局引用。");
            return Cases.SingleOrDefault(c => c.CaseId == id) ??
                throw new InvalidOperationException("地图中找不到指定布局，请检查布局标识 " + id);
        }
    }
}
