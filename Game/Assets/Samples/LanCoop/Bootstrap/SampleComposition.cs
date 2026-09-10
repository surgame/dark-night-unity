using DarkNights.Samples.LanCoop.Runtime;
using DarkNights.Samples.LanCoop.Presentation;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using UnityEngine;

namespace DarkNights.Samples.LanCoop.Bootstrap
{
    /// <summary>独立 Sample 场景的显式装配点；本地 ObjectView 只表现，退出样板后不为正式场景注入服务。</summary>
    public sealed class SampleComposition : MonoBehaviour
    {
        public SampleNetwork Network;
        public SamplePanel Panel;
        public ObjectInstance Worksite;
        public ObjectDefinition WorksiteDefinition;
        private void Start()
        {
            Panel.Client = Network;
            Worksite.Initialize("sample-worksite-1", WorksiteDefinition);
        }
    }
}
