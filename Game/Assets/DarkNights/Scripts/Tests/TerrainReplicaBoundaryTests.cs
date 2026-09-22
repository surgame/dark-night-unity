using System;
using System.Collections.Generic;
using System.Threading;
using AnyRules.Next;
using AnyRules.Next.Authoring;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Terrain;
using DarkNights.View.Terrain;
using NUnit.Framework;
using UnityEditor;

namespace DarkNights.Tests
{
    /// <summary>工作台权威源的世界边界指纹回归；外部 padding 必须与加载时一致，首次通知不得误报整行区块。</summary>
    public sealed class TerrainReplicaBoundaryTests
    {
        [Test]
        public void AuthorityPaddingIsStableAndEdgeEditsRemainLocal()
        {
            var cells = new byte[320 * 192]; for (int i = 0; i < cells.Length; i++) cells[i] = 2;
            var blueprint = new TerrainBlueprint(new TerrainGenerationSettings(), cells, new bool[cells.Length],
                new int[320], new[] { new TerrainRoom("gallery", 40, 45, 10, 10) });
            var definition = AssetDatabase.LoadAssetAtPath<ARDMapDefinition>("Assets/DarkNights/Res/Terrain/StrataCave/MapDefinition.asset");
            var game = Runtime.Config.GameCatalogJson.Parse(
                AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>("Assets/DarkNights/Res/Config/balance.json").text,
                AssetDatabase.LoadAssetAtPath<UnityEngine.TextAsset>("Assets/DarkNights/Res/Config/pinewatch.json").text);
            using var session = new CaveWorkshopSession(blueprint, definition.LoadGameplayCatalog(), game);
            var source = new TerrainReplicaSource(session.Map);
            for (int v = -6; v <= 0; v++) for (int u = 0; u < 10; u++)
                source.LoadAsync(session.Map.Descriptor, new ChunkCoord(u, v), CancellationToken.None).GetAwaiter().GetResult();
            IReadOnlyList<ChunkCoord> changed = null; source.Changed += chunks => changed = chunks;
            source.NotifyChanged(); Assert.That(changed, Is.Null, "未编辑的边界不能误报。");
            foreach (int row in new[] { 0, 191 })
            {
                var center = new CellCoord(15, -row);
                session.Map.DestroyTrusted(1, "edge:" + row, TerrainEditAction.Explosive, session.Map.World,
                    center, session.Map.BuildTargets(TerrainEditAction.Explosive, center), _ => true);
                source.NotifyChanged(); Assert.That(changed.Count, Is.EqualTo(row == 0 ? 2 : 1));
                Assert.That(changed, Does.Contain(new ChunkCoord(0, GridMath.FloorDiv(-row, 32))));
                if (row == 0) Assert.That(changed, Does.Contain(new ChunkCoord(0, -1)));
                changed = null; source.NotifyChanged(); Assert.That(changed, Is.Null);
            }
        }
    }
}
