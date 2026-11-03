using System;
using AnyRules.Next;
using AnyRules.Next.Networking;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Network;
using DarkNights.View.MapModule;
using UnityEngine;

namespace DarkNights.Entry.MapModule
{
    /// <summary>
    /// 固定模块研究的本端白模显示，消费既有局部副本及真实坡形，不把Unknown当空地或提前展示完整作者网格。
    /// 网格实际安装后才上报PresentationReady；退出释放自己生成的网格及副本订阅，原生材质和资产保持。
    /// </summary>
    public sealed class MapModuleTerrainView : MonoBehaviour
    {
        private SessionNetwork network;
        private MapModuleCaseAuthoring sample;
        private ChunkReplicaStateMachine observed;
        private Mesh owned;
        private Mesh original;
        private bool originallyEnabled;
        private bool dirty = true;
        public void Initialize(SessionNetwork session, MapModuleCaseAuthoring source, bool authorVisibility)
        {
            network = session; sample = source;
            original = sample.PreviewMesh.sharedMesh; originallyEnabled = authorVisibility;
            sample.PreviewRenderer.enabled = false;
        }
        private void Update()
        {
            if (network == null || network.Terrain == null) return;
            try
            {
                var terrain = network.Terrain; terrain.Pump();
                var replica = terrain.Replica;
                if (replica != observed)
                {
                    if (observed != null) observed.Applied -= Changed;
                    observed = replica;
                    if (observed != null) observed.Applied += Changed;
                    dirty = true; terrain.PresentationReady = false; sample.PreviewRenderer.enabled = false;
                }
                var frame = network.Client.Replica.Current;
                if (!terrain.DataReady || !terrain.LocalDataReady || frame == null || terrain.Epoch != frame.Epoch) return;
                if (!dirty) return;
                int count = TerrainGenerationSettings.Width * TerrainGenerationSettings.Height;
                var materials = new byte[count]; var shapes = new byte[count];
                string[] keys = { "loam", "slate", "basalt", "copper", "iron", "gold", "moss", "bedrock" };
                var palette = new System.Collections.Generic.Dictionary<uint, byte>();
                for (byte i = 0; i < keys.Length; i++) palette.Add(terrain.Tiles.ByKey(keys[i]), (byte)(i + 1));
                for (int row = 0; row < TerrainGenerationSettings.Height; row++) for (int x = 0; x < TerrainGenerationSettings.Width; x++)
                {
                    var result = replica.Read(new CellCoord(x, -row));
                    if (!result.TryGetCell(out var cell) || cell.IsEmpty) continue;
                    int index = row * TerrainGenerationSettings.Width + x;
                    materials[index] = palette[cell.TileId]; shapes[index] = (byte)((cell.Flags >> 1) & 15);
                }
                var next = MapModuleMesh.Build(materials, shapes, new Color32(132, 143, 111, 255));
                var previous = owned; owned = next; sample.PreviewMesh.sharedMesh = next;
                sample.PreviewRenderer.enabled = true; dirty = false;
                if (previous != null) Destroy(previous);
                terrain.PresentationReady = true;
            }
            catch (Exception error) { network.Fail(error); enabled = false; }
        }
        private void Changed(MapReplicaChange change) { dirty = true; }
        private void OnDestroy()
        {
            if (observed != null) observed.Applied -= Changed;
            if (sample != null && sample.PreviewMesh != null)
            {
                sample.PreviewMesh.sharedMesh = original;
                if (sample.PreviewRenderer != null) sample.PreviewRenderer.enabled = originallyEnabled;
            }
            if (owned != null) Destroy(owned);
        }
    }
}
