using System;
using DarkNights.Core.Config;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Save;
using DarkNights.Runtime.Session;
using static DarkNights.Tests.SessionScenario;

namespace DarkNights.Tests
{
    /// <summary>
    /// 验证 Host 回环去重、旧首次快照、回执先到、相同 revision 元数据、加载及旧连接异步结果隔离。
    /// 驱动实际权威帧，未经过 transport，不能当作网络乱序或连接认证验收。
    /// </summary>
    public static class SessionReplicaScenarios
    {
        public static void Run(Action<bool, string> check, GameCatalog catalog, LevelLayout layout)
        {
            using var session = Open(catalog, layout, out var host, out var guest);
            var replica = new WorldReplica();
            var initial = session.CaptureProjection();
            check(!replica.Apply(0, initial) && !replica.HasApplied(1, 0), "Replica requires an active local connection binding");
            long connection = replica.BeginConnection();
            check(replica.Apply(connection, initial) && !replica.Apply(connection, initial), "Replica applies initial frame once and filters Host loopback duplicate");
            check(!replica.HasApplied(initial.Epoch, -1), "Replica rejects invalid receipt revision");
            var request = Request(session, SessionOperation.SetPaused, 1, value: 1);
            var receipt = Execute(session, host, request);
            check(!replica.HasApplied(receipt.Epoch, receipt.Revision), "Replica does not treat an early receipt as applied world state");
            var newer = session.CaptureProjection();
            check(replica.Apply(connection, newer) && replica.HasApplied(receipt.Epoch, receipt.Revision),
                "Replica unlocks receipt-dependent view only after corresponding world revision");
            check(!replica.Apply(connection, initial) && ReferenceEquals(replica.Current, newer),
                "Replica late initial frame cannot revert online state");
            session.Disconnect(guest);
            var disconnected = session.CaptureProjection();
            check(disconnected.Revision == newer.Revision && disconnected.ServerTick == newer.ServerTick &&
                replica.Apply(connection, disconnected) && replica.Current.PlayerCount == 1,
                "Replica same-tick same-revision publication updates connection metadata");
            var ticket = Execute(session, host, Request(session, SessionOperation.BeginLoad, 2));
            var loading = session.CaptureProjection();
            check(replica.Apply(connection, loading) && replica.Current.Loading, "Replica shows controlled loading state");
            session.CancelLoad(ticket);
            var cancelled = session.CaptureProjection();
            check(loading.ServerTick == cancelled.ServerTick && loading.Revision == cancelled.Revision &&
                replica.Apply(connection, cancelled) && !replica.Current.Loading,
                "Replica newer publication clears cancelled loading without a simulation tick");
            var codec = new GameSaveJson(catalog, layout);
            string saved = codec.Serialize(session.CaptureWorld());
            ticket = Execute(session, host, Request(session, SessionOperation.BeginLoad, 3));
            session.CompleteLoad(ticket, saved);
            var restored = session.CaptureProjection();
            check(replica.Apply(connection, restored) && replica.Current.Epoch == 2 && replica.Current.Revision == 0 &&
                !replica.HasApplied(1, 0), "Replica atomically accepts new epoch with reset revision and invalidates old receipt");
            check(!replica.Apply(connection, cancelled) && ReferenceEquals(replica.Current, restored),
                "Replica delayed old epoch cannot restore old entities");
            check(!replica.Apply(connection, Copy(restored, restored.Publication + 1, epoch: 1)),
                "Replica rejects old epoch even with greater publication");
            check(!replica.Apply(connection, Copy(restored, restored.Publication + 1, tick: restored.ServerTick - 1)),
                "Replica rejects server clock regression");
            session.AcknowledgeReady(host, 2, 0);
            session.Tick();
            var advanced = session.CaptureProjection();
            replica.Apply(connection, advanced);
            check(!replica.Apply(connection, Copy(advanced, advanced.Publication + 1, revision: 0)),
                "Replica rejects same-epoch world revision regression");
            Execute(session, host, Request(session, SessionOperation.SetControlMode, 1, value: 1));
            var policy = session.CaptureProjection();
            check(replica.Apply(connection, policy) && replica.Current.HostOnly, "Replica displays authoritative room control policy");
            check(!replica.Apply(connection, Copy(policy, policy.Publication + 1, policyRevision: 0)),
                "Replica rejects policy version regression");
            long replacement = replica.BeginConnection();
            check(replica.Current == null && !replica.Apply(connection, policy),
                "Replica replacement clears world and rejects previous connection asynchronous callbacks");
            replica.EndConnection(connection);
            using var other = new SessionAuthority(catalog, layout);
            check(replica.Apply(replacement, other.CaptureProjection()) && replica.Current.Epoch == 1,
                "Replica fresh connection accepts a new room with restarted versions despite old disconnect callback");
            replica.EndConnection(replacement);
            check(replica.Current == null && !replica.Apply(replacement, initial), "Replica closed connection cannot be revived by a late snapshot");
            check(Throws<ArgumentOutOfRangeException>(() => Copy(initial, 0)), "Projection rejects invalid publication metadata");
        }

        private static SessionViewData Copy(SessionViewData frame, long publication, int? epoch = null,
            int? revision = null, long? tick = null, int? policyRevision = null) =>
            new SessionViewData(publication, epoch ?? frame.Epoch, revision ?? frame.Revision, tick ?? frame.ServerTick,
                policyRevision ?? frame.PolicyRevision, frame.HostOnly, frame.PlayerCount, frame.ReadyCount,
                frame.Loading, frame.Paused, frame.Speed, frame.Elapsed, frame.World);
    }
}
