using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using FishNet.Transporting;
using GameCore.Objects.NetworkStates;
using GameCore.Objects.Runner;
using UnityEngine;

namespace YYGC.IdentityValidation
{
    /// <summary>使用真正的独立 FishNet 进程验证定义和状态；Host、客户端、晚加入及重连均走正式框架入口。</summary>
    public static class IdentityNetworkProbe
    {
        public static async UniTask Run(IdentityPlayerProbe probe, string scenario)
        {
            bool server = scenario == "host";
            bool mismatch = scenario.StartsWith("mismatch-", StringComparison.Ordinal);
#if !YYGC_IDENTITY_BASELINE
            DefinitionNetworkAuthenticator auth = null;
            if (DefinitionNetworkProfile.WireVersion == 2 || IdentityProbeReport.Argument("-identityHandshake", "0") == "1")
            {
                auth = probe.Manager.gameObject.AddComponent<DefinitionNetworkAuthenticator>();
                auth.Configure(probe.Database, scenario == "mismatch-catalog" ? "fixture-other" : "fixture-1");
                auth.OnCompatibilityRejected += _ => probe.Report.rejectedConnections++;
                probe.Manager.ServerManager.SetAuthenticator(auth);
            }
#endif
            probe.Manager.gameObject.SetActive(true);
            var transport = probe.Manager.TransportManager.Transport;
            transport.SetPort(ushort.Parse(IdentityProbeReport.Argument("-identityPort", "27831")));
            transport.SetClientAddress("127.0.0.1");
            transport.SetServerBindAddress("127.0.0.1", IPAddressType.IPv4);
            probe.Manager.ServerManager.OnAuthenticationResult += (connection, accepted) => { if (accepted) probe.Report.acceptedConnections++; };
            if (server)
            {
#if YYGC_IDENTITY_BASELINE
                // 旧 Loader 在 FishNet Awake 缓存前跳过场景对象；wire 对照由旧公开入口明确提供旧 ID。
                var sceneObject = probe.SceneObject.GetComponent<FishNet.Object.NetworkObject>();
                FishNet.Managing.Object.ManagedObjects.InitializePrefab(sceneObject, sceneObject.PrefabId, sceneObject.SpawnableCollectionId);
                probe.SceneObject.Initialize(probe.Network.Id);
#endif
                probe.Report.Check(probe.Manager.ServerManager.StartConnection(), "独立进程服务端开始连接");
                await Until(() => probe.Manager.ServerManager.Started);
#if YYGC_IDENTITY_BASELINE
                // 旧包对照只验证 V1 wire；使用 FishNet 已注册 Prefab，避免旧 Addressables 副本缺少缓存的已知限制。
                var canonical = probe.Manager.SpawnablePrefabs.GetObject(true, 0);
                var spawned = UnityEngine.Object.Instantiate(canonical);
                spawned.GetComponent<StateSynchronizer>().Initialize(probe.Network.Id);
                probe.Manager.ServerManager.Spawn(spawned);
                probe.Report.Check(spawned != null, "冻结旧 V1 使用注册 Prefab 与旧整数入口生成业务对象");
#else
                var spawned = await ObjectInstanceFactory.CreateObjectInstanceAsync(probe.Network, Vector3.zero, Quaternion.identity, null, probe.Manager.ServerManager);
                probe.Report.Check(spawned != null, "网络工厂从打包 Prefab 生成业务对象");
#endif
            }
            probe.Report.Check(probe.Manager.ClientManager.StartConnection(), "独立客户端开始连接");
            if (mismatch)
            {
#if !YYGC_IDENTITY_BASELINE
                await Until(() => auth != null && auth.LastFailure != null);
                await UniTask.Delay(TimeSpan.FromSeconds(1));
                probe.Report.initializedObjects = Instances().Length;
                probe.Report.Check(probe.Report.initializedObjects == 0, "不匹配在业务对象初始化之前拒绝");
                probe.Report.Check(!probe.Manager.IsClientStarted, "不匹配客户端未通过连接鉴权");
                probe.Report.checks.Add(auth.LastFailure);
                return;
#else
                throw new InvalidOperationException("旧基线不提供新协议握手。");
#endif
            }
            await Until(() => ReadyObjects());
            Validate(probe, server);
            if (scenario == "reconnect")
            {
                probe.Manager.ClientManager.StopConnection();
                await Until(() => !probe.Manager.ClientManager.Started);
                await UniTask.Delay(TimeSpan.FromSeconds(1));
                probe.Report.Check(probe.Manager.ClientManager.StartConnection(), "断线后重新连接");
                await Until(() => ReadyObjects());
                Validate(probe, false);
                probe.Report.checks.Add("重连后场景对象和动态对象恢复同一定义及状态");
            }
            if (server)
            {
                Debug.Log("IDENTITY_NETWORK_READY");
                await UniTask.Delay(TimeSpan.FromSeconds(5));
                foreach (var instance in Instances())
                {
                    instance.GetComponent<ObjectInstance>().GetBehaviour<IdentityStateBehaviour>().SetValue(5151);
#if !YYGC_IDENTITY_BASELINE
                    instance.InitializeDefinition(probe.Network);
#else
                    instance.Initialize(probe.Network.Id);
#endif
                }
                Debug.Log("IDENTITY_NETWORK_UPDATED");
                float seconds = float.Parse(IdentityProbeReport.Argument("-identitySeconds", "25"), System.Globalization.CultureInfo.InvariantCulture);
                await UniTask.Delay(TimeSpan.FromSeconds(seconds));
                Validate(probe, true);
                probe.Report.Check(Instances().All(s => s.GetComponent<IdentityNetworkObserver>().Completed == 1),
                    "Host 双角色与重复定义通知仍只有一次初始化事件");
                probe.Manager.ClientManager.StopConnection();
                probe.Manager.ServerManager.StopConnection(true);
            }
            else
            {
                // 等待服务器的第二次状态，区分只有初始快照与增量 RPC 均可用。
                await Until(() => ReadyObjects() && Instances().All(s => s.GetComponent<ObjectInstance>().GetBehaviour<IdentityStateBehaviour>().State.Value == 5151));
                probe.Report.checks.Add("收到权威状态 5151，晚加入 / 增量更新路径有效");
                probe.Manager.ClientManager.StopConnection();
            }
        }

        private static void Validate(IdentityPlayerProbe probe, bool server)
        {
            var objects = Instances();
            probe.Report.initializedObjects = objects.Length;
            probe.Report.Check(objects.Length == 2, "场景对象与动态对象各一个");
            foreach (var synchronizer in objects)
            {
                var instance = synchronizer.GetComponent<ObjectInstance>();
                var behaviour = instance.GetBehaviour<IdentityStateBehaviour>();
                probe.Report.Check(instance.Definition == probe.Network, "网络定义解析到相同目录资产：" + instance.name);
                probe.Report.Check(behaviour.State.Value == 4242 || behaviour.State.Value == 5151, "初始状态快照成功：" + behaviour.State.Value);
                if (server)
                    probe.Report.Check(behaviour.Initializations == 1 && behaviour.ServerStarts == 1 && behaviour.ClientStarts == 1,
                        "Host 装配 / Server Start / Client Start 各一次：" + instance.name +
                        $" ({behaviour.Initializations}/{behaviour.ServerStarts}/{behaviour.ClientStarts})");
#if YYGC_GUID_DEFINITION_WIRE_V2
                probe.Report.Check(probe.Network.Id == 0 && synchronizer.GetDefinitionGuid() == probe.Network.Guid, "GuidV2 完整 GUID 同步且无旧整数依赖");
#endif
            }
        }

        private static StateSynchronizer[] Instances() => UnityEngine.Object.FindObjectsByType<StateSynchronizer>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).Where(s => s.IsInitialized).ToArray();

        private static bool ReadyObjects()
        {
            var objects = Instances();
            return objects.Length == 2 && objects.All(s =>
            {
                var behaviour = s.GetComponent<ObjectInstance>().GetBehaviour<IdentityStateBehaviour>();
                return s.IsClientStarted && behaviour != null && behaviour.ClientStarts > 0 &&
                    behaviour.State != null && (behaviour.State.Value == 4242 || behaviour.State.Value == 5151);
            });
        }

        private static async UniTask Until(Func<bool> predicate)
        {
            using (var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20)))
                await UniTask.WaitUntil(predicate, cancellationToken: timeout.Token);
        }
    }
}
