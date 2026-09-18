using System;
using System.Linq;
using AnyRules.Next;
using AnyRules.Next.FishNet;
using GameCore.NetworkCommands;
using VitalRouter;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>正式 Tag 3 命令路由；只接受可信 NetworkCommandContext，Host 与远端共用同一张授权和幂等路径。</summary>
    public sealed class TerrainActionRoute : IDisposable
    {
        private readonly Func<TerrainMapAuthority> map;
        private readonly Func<NetworkCommandContext, TerrainEditCommand, TerrainActionAuthorization> authorize;
        private readonly Action<NetworkCommandContext, TerrainActionResult> respond;
        private readonly Subscription subscription;

        public TerrainActionRoute(Func<TerrainMapAuthority> map,
            Func<NetworkCommandContext, TerrainEditCommand, TerrainActionAuthorization> authorize,
            Action<NetworkCommandContext, TerrainActionResult> respond = null)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            this.authorize = authorize ?? throw new ArgumentNullException(nameof(authorize));
            this.respond = respond;
            subscription = CommandRouters.LocalInput.SubscribeAwait<TerrainEditCommand>((command, publication) =>
            {
                if (!NetworkCommandContext.TryGet(command, out var context) || !context.IsServerExecution) return default;
                TerrainActionResult result;
                try
                {
                    TerrainMapAuthority authority = this.map();
                    TerrainActionAuthorization grant = this.authorize(context, command);
                    if (authority == null || grant == null) throw new InvalidOperationException("地图或动作授权尚未就绪。");
                    var center = new CellCoord(command.U, command.V);
                    if (authority.TryGetCached(grant.ConnectionGeneration, command.RequestId, grant.Action,
                        command.ExpectedRevision, center, out var cached))
                    {
                        result = new TerrainActionResult(command.RequestId, grant.Action, cached.CommitId, true, "Accepted");
                        respond?.Invoke(context, result);
                        return default;
                    }
                    var targets = authority.BuildTargets(grant.Action, center);
                    var resources = targets.Select(authority.ResourceAt).Where(value => value.Length != 0).ToArray();
                    bool applied;
                    var receipt = authority.DestroyTrusted(grant.ConnectionGeneration, command.RequestId, grant.Action,
                        grant.World, command.ExpectedRevision, center, targets, grant.AllowTarget, out applied);
                    if (applied) grant.OnCommitted?.Invoke(resources);
                    result = new TerrainActionResult(command.RequestId, grant.Action, receipt.CommitId, true, "Accepted");
                }
                catch (ArgumentException error) { result = Reject(command, error.Message); }
                catch (InvalidOperationException error) { result = Reject(command, error.Message); }
                respond?.Invoke(context, result);
                return default;
            });
        }

        private static TerrainActionResult Reject(TerrainEditCommand command, string reason) =>
            new TerrainActionResult(command.RequestId, default, 0, false, reason);
        public void Dispose() => subscription.Dispose();
    }
}
