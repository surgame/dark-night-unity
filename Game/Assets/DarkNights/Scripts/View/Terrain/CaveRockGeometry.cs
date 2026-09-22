using System;
using System.Threading;
using System.Threading.Tasks;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.View.Terrain
{
    /// <summary>当前格子副本的后台轮廓任务；同一时刻只有一个生成任务，连续编辑合并到最新版本，过期结果不发布，无权修改地图。</summary>
    internal sealed class CaveRockGeometry
    {
        private readonly CaveOutlineSettings outline;
        private readonly CaveModifierStack modifiers;
        private readonly string seed;
        private readonly CancellationToken token;
        private byte[] materials, shapes;
        private int revision, bakedRevision, pendingRevision;
        private Task<(CaveMaskField Field, bool[] Dirty)> task;
        public CaveMaskField Field { get; private set; }
        public bool Ready => Field != null && revision == bakedRevision;
        public CaveRockGeometry(string seed, CaveOutlineSettings outline, CaveModifierStack modifiers, CancellationToken token)
        { this.seed = seed; this.outline = outline; this.modifiers = modifiers; this.token = token; }
        public void Replace(byte[] materials, byte[] shapes)
        { this.materials = materials; this.shapes = shapes; revision++; }
        public bool[] Tick()
        {
            if (task != null)
            {
                if (!task.IsCompleted) return null;
                var result = task.GetAwaiter().GetResult(); task = null;
                if (pendingRevision == revision)
                { Field = result.Field; bakedRevision = revision; return result.Dirty; }
            }
            if (Ready || materials == null) return null;
            var ownMaterials = materials; var ownShapes = shapes; var previous = Field;
            pendingRevision = revision;
            task = Task.Run(() =>
            {
                var next = CaveModifiedTerrain.Bake(Solid, 2560, 1536, seed, outline, modifiers, 43 * 8, token.ThrowIfCancellationRequested);
                return (next, CaveMaskChanges.DirtyPages(previous, next, checkpoint: token.ThrowIfCancellationRequested));
                bool Solid(int x, int y)
                {
                    int p = y / 8 * 320 + x / 8;
                    return ownMaterials[p] != 0 && TerrainShapeGeometry.Contains((TerrainCellShape)ownShapes[p],
                        (x % 8 + .5f) / 8, 1 - (y % 8 + .5f) / 8);
                }
            }, token);
            return null;
        }
    }
}
