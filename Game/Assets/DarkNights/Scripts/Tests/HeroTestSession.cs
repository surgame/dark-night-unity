using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;

namespace DarkNights.Tests
{
    /// <summary>
    /// 主角回归的真实 YYGC 会话夹具，复用正式资源与可信连接入口；不复制模拟或替代能力。
    /// 测试平台使用独立布局，不修改场景及冻结原布局证据，清理交给既有资源范围。
    /// </summary>
    public sealed class HeroTestSession : IDisposable
    {
        private UnifiedSessionScope scope;
        private long sequence, inputSequence;
        private bool assignDefaultHeroes;
        public ObjectSession World { get; private set; }
        public SessionAuthority Authority { get; private set; }
        public SessionConnection Host { get; private set; }
        public SessionConnection Guest { get; private set; }
        public int ActorId { get; private set; }
        public ActorBehaviour Actor => World.Index.Find<ActorBehaviour>(ActorId);
        public ActorState State => Actor.CaptureState();

        public static async UniTask<HeroTestSession> Create(bool assignDefaultHeroes = false)
        {
            var f = new HeroTestSession();
            try
            {
                f.scope = await UnifiedSessionScope.Create();
                var catalog = RuleScenario.Catalog(); var old = RuleScenario.Layout();
                var layout = new LevelLayout(old.WorldWidth, old.GroundY, old.BuildMinX, old.BuildMaxX, old.SpawnX,
                    old.CameraX, old.Buildings, old.Worksites, old.Actors, new[] { new PlatformDefinition(1, 150, 198, 12) });
                f.World = f.scope.NewWorld(catalog, layout, false);
                f.assignDefaultHeroes = assignDefaultHeroes;
                f.Authority = new SessionAuthority(f.World);
                f.Host = f.Authority.Connect(0); f.Guest = f.Authority.Connect(1);
                f.Ready(); f.ActorId = f.World.Index.Actors[0].Id;
                return f;
            }
            catch { f.Dispose(); throw; }
        }
        public void Ready()
        {
            Authority.AcknowledgeReady(Host, Authority.Epoch, Authority.Revision, assignDefaultHeroes);
            Authority.AcknowledgeReady(Guest, Authority.Epoch, Authority.Revision, assignDefaultHeroes);
        }
        public SessionReceipt Command(SessionOperation operation, SessionConnection sender = null,
            int target = 0, string kind = "", int value = 0, int? lease = null)
        {
            bool hero = operation == SessionOperation.ClaimHero || operation == SessionOperation.ReleaseHero ||
                operation == SessionOperation.SelectHeroItem || operation == SessionOperation.UseHeroItem;
            var request = new SessionRequest(operation, SessionAuthority.ProtocolVersion, Authority.Epoch, Authority.PolicyRevision,
                ++sequence, hero ? new[] { ActorId } : null, target, kind: kind, value: value,
                controlLease: hero && operation != SessionOperation.ClaimHero ? lease ?? State.ControlLease : 0);
            var result = Authority.Submit(sender ?? Host, request);
            return result.Code == SessionResultCode.Pending ? Authority.Tick().Single(r => r.Sequence == request.Sequence) : result;
        }
        public HeroInputRequest Packet(int horizontal = 0, bool jumpHeld = false, bool useHeld = false,
            bool jumpPressed = false, bool dropPressed = false, int? lease = null) =>
            new HeroInputRequest(SessionAuthority.ProtocolVersion, Authority.Epoch, Authority.PolicyRevision, ActorId,
                lease ?? State.ControlLease, ++inputSequence, Authority.ServerTick, horizontal, jumpHeld, useHeld, jumpPressed, dropPressed);
        public bool Input(int horizontal = 0, bool jumpHeld = false, bool useHeld = false,
            bool jumpPressed = false, bool dropPressed = false) =>
            Authority.SubmitInput(Host, Packet(horizontal, jumpHeld, useHeld, jumpPressed, dropPressed));
        public void Step(int ticks, int horizontal = 0, bool jumpHeld = false, bool useHeld = false, bool keepAlive = false)
        {
            for (int i = 0; i < ticks; i++)
            {
                if (keepAlive && i % 6 == 0) Input(horizontal, jumpHeld, useHeld);
                Authority.Tick();
            }
        }
        public void Dispose() { Authority?.Dispose(); scope?.Dispose(); }
    }
}
