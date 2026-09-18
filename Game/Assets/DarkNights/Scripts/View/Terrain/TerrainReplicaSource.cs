using System;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Unity;

namespace DarkNights.View.Terrain
{
    /// <summary>把已经原子提交的网络只读格子复制为本地页面输入；所有渲染区块标记只读，不生成地图或开放编辑能力。</summary>
    public sealed class TerrainReplicaSource : IMapChunkSource
    {
        private readonly IReadOnlyGrid source;
        public event Action Changed;
        public TerrainReplicaSource(IReadOnlyGrid source)
        {
            this.source = source ?? throw new ArgumentNullException(nameof(source));
        }
        public Task<MapChunkData> LoadAsync(WorldDescriptor descriptor, ChunkCoord coordinate, CancellationToken cancellation)
        {
            cancellation.ThrowIfCancellationRequested();
            int size = descriptor.ChunkSize;
            var cells = new GridCell[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                var position = new CellCoord(coordinate.U * size + x, coordinate.V * size + y);
                if (!descriptor.Bounds.Contains(position)) continue;
                if (!source.Read(position).TryGetCell(out cells[y * size + x])) throw new InvalidOperationException("地图区块尚未完整提交。");
            }
            return Task.FromResult(new MapChunkData(coordinate, cells, readOnly: true));
        }

        public void NotifyChanged() => Changed?.Invoke();
    }
}
