using System;
using System.Linq;
using UnityEngine;

namespace DarkNights.View.MapModule
{
    /// <summary>研究Scene的主题及样例目录；选择归工作台本机状态，一次运行只冻结一个样例，不创建多套会话Rig。</summary>
    public sealed class MapModuleStudyAuthoring : MonoBehaviour
    {
        public string StudyId = "entrance";
        public string DisplayName = "洞口研究";
        public MapModuleCaseAuthoring[] Cases = Array.Empty<MapModuleCaseAuthoring>();
        public MapModuleCaseAuthoring RequiredCase(string id)
        {
            if (string.IsNullOrWhiteSpace(StudyId) || Cases == null || Cases.Length == 0 || Cases.Any(c => c == null) ||
                Cases.Select(c => c.CaseId).Distinct().Count() != Cases.Length)
                throw new InvalidOperationException("研究主题或样例目录无效。");
            return Cases.SingleOrDefault(c => c.CaseId == id) ??
                throw new InvalidOperationException("研究场景没有指定样例：" + id);
        }
    }
}
