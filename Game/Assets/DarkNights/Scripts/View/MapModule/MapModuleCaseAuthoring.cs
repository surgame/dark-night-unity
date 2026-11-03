using System;
using UnityEngine;

namespace DarkNights.View.MapModule
{
    /// <summary>
    /// 一个研究样例的原生Scene布局；模块位置、连接和出生锚点由此Scene唯一编辑。
    /// Bake仅引用独立冻结派生资源，源变化后由工作台阻止沿用旧版本启动。
    /// </summary>
    public sealed class MapModuleCaseAuthoring : MonoBehaviour
    {
        public string CaseId;
        public string DisplayName;
        public MapModuleAuthoring[] Modules = Array.Empty<MapModuleAuthoring>();
        public MapModuleConnection[] Connections = Array.Empty<MapModuleConnection>();
        public Transform Spawn;
        public MeshFilter PreviewMesh;
        public MeshRenderer PreviewRenderer;
        public MapModuleBake Bake;
    }
}
