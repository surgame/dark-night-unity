using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

namespace DarkNights.View.Lighting
{
    /// <summary>环境照明后端的有限生命周期合同；只消费同一帧冻结光源，切换先停止旧后端，退休释放其私有资源。</summary>
    public interface IEnvironmentLightBackend
    {
        int SourceCount { get; }
        void Render(Camera camera, Terrain.TerrainPreview preview, IReadOnlyList<LightEmitterData> emitters);
        void Suspend();
        Task RetireAsync();
    }
}
