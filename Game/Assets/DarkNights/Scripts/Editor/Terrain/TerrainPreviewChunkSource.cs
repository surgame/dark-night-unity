using System;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next;
using AnyRules.Next.Unity;

namespace DarkNights.Editor.Terrain
{
    /// <summary>局部 Editor 预览的冻结区块输入；只转交装载，不发布整批输入基线，避免为不可见边界排队等待未知邻格，换区域由宿主整体退休。</summary>
    public sealed class TerrainPreviewChunkSource : IMapChunkSource
    {
        private readonly IMapChunkSource source;
        public TerrainPreviewChunkSource(IMapChunkSource source)
            => this.source = source ?? throw new ArgumentNullException(nameof(source));

        public Task<MapChunkData> LoadAsync(WorldDescriptor world, ChunkCoord coordinate, CancellationToken cancellation)
            => source.LoadAsync(world, coordinate, cancellation);
    }
}
