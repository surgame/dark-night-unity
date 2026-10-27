using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Runtime.Terrain;

namespace DarkNights.Editor.Terrain
{
    /// <summary>单次编辑预览的冻结输入与后台纯计算结果；不读Unity资产、不修改作者输入，过期结果由主线程丢弃。</summary>
    internal sealed class TerrainGenerationWork
    {
        internal string Identity { get; }
        internal Task<TerrainGenerationResult> Task { get; }
        internal List<string> Report { get; } = new List<string>();
        internal string GeologyConflict { get; private set; } = "";

        internal TerrainGenerationWork(TerrainGenerationPreview model)
        {
            MapWorkbenchImport.NormalizeDraft(model.Draft);
            Identity = model.Identity;
            var config = model.Draft.Config;
            var settings = config.FreezeCaveMap();
            var planet = model.Selected.Freeze();
            var geology = config.Geology?.Copy();
            bool capture = model.CaptureStages;
            Task = System.Threading.Tasks.Task.Run(() =>
            {
                var generated = PlanetTerrainGenerator.GenerateResult(planet, settings.Seed,
                    TerrainGenerationPreview.WorldId, settings, contentReport: Report.Add, captureStages: capture);
                var result = generated.Terrain;
                try { result = GeologyMapDesign.Attach(result, geology); }
                catch (Exception error)
                {
                    GeologyConflict = error.Message;
                }
                return new TerrainGenerationResult(result, generated.Plan, generated.Version,
                    generated.SpaceIdentity, generated.Steps.ToArray());
            });
        }
    }
}
