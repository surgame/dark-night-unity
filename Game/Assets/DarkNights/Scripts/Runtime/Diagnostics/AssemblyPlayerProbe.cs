using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using GameCore.Objects.Runner.DI;
using Newtonsoft.Json;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.Runtime.Diagnostics
{
    /// <summary>
    /// 显式 --dn-assembly-probe 参数启用的独立 Player 装配验收；正常启动立即返回。
    /// 只读取正式定义并修改其内存副本，检查完成后写报告并退出，不访问存档或改动制作资产。
    /// </summary>
    public static class AssemblyPlayerProbe
    {
        public static async UniTask RunIfRequested(CancellationToken cancellationToken)
        {
            string[] args = System.Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, "--dn-assembly-probe");
            if (index < 0) return;
            if (index + 1 >= args.Length) throw new ArgumentException("Probe report path is required.");
            string path = Path.GetFullPath(args[index + 1]);
            var checks = new Dictionary<string, bool>();
            string failure = null;
            bool coldLoadPending = false;
            var container = new DIContainer();
            container.Initialize();
            var source = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("unit.worker");
            var definition = UnityEngine.Object.Instantiate(source);
            var archetype = ScriptableObject.CreateInstance<ObjectArchetype>();
            var owner = new GameObject("AssemblyValidationProbe").AddComponent<ObjectInstance>();
            ObjectView worker = null;
            using var session = ObjectSessionContext.CreateReplica(container);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                using (var loading = ObjectSessionContext.CreateReplica(container))
                {
                    var pending = ObjectInstanceFactory.PrepareAsync(source, loading.Lifetime);
                    coldLoadPending = pending.Status == UniTaskStatus.Pending;
                    loading.Dispose();
                    bool cancelled = false;
                    try { using var unexpected = await pending; }
                    catch (OperationCanceledException) { cancelled = true; }
                    Check(checks, "cancel_inflight_load", coldLoadPending && cancelled);
                }
                definition.BehaviourTypes.Clear();
                definition.BehaviourTypes.Add(typeof(AssemblyProbeBehaviour).FullName);
                definition.SharedConfigs.Clear();
                Reject(checks, "missing_config", () => owner.Initialize("missing", definition, session: session, activate: false));
                Check(checks, "missing_config_not_assembled", !owner.IsAssembled && owner.GetBehaviourCount() == 0);
                definition.SharedConfigs.Add(new AssemblyProbeConfig());
                definition.SharedConfigs.Add(new AssemblyProbeConfig());
                Reject(checks, "duplicate_config", () => owner.Initialize("duplicate", definition, session: session, activate: false));
                definition.SharedConfigs.RemoveAt(1);
                archetype.RequiredCapabilityInterfaces.Add(typeof(IComparable).AssemblyQualifiedName);
                definition.Archetype = archetype;
                Reject(checks, "missing_capability", () => owner.Initialize("capability", definition, session: session, activate: false));
                definition.Archetype = null;
                owner.Initialize("valid", definition, session: session, activate: false);
                Check(checks, "generated_injection", owner.GetBehaviour<AssemblyProbeBehaviour>().Observed == 7);
                Check(checks, "prepared_not_active", owner.IsAssembled && !owner.IsActive);
                Reject(checks, "activation_before_session", owner.Activate);
                session.Activate();
                owner.Activate();
                Check(checks, "valid_activation", owner.IsActive);
                owner.Release();
                Reject(checks, "missing_worker_binding", () => owner.Initialize("bindings", source, session: session, activate: false));
                using (var prepared = await ObjectInstanceFactory.PrepareAsync(source, cancellationToken))
                {
                    worker = prepared.Create(Vector3.zero, Quaternion.identity, session);
                    Check(checks, "real_prefab_prepared", worker.Owner.IsAssembled && !worker.Owner.IsActive);
                    worker.Owner.Activate();
                    Check(checks, "real_prefab_activated", worker.Owner.IsActive);
                    session.Dispose();
                    Check(checks, "retired_immediately", !worker.Owner.IsActive);
                    Reject(checks, "retired_creation", () => prepared.Create(Vector3.zero, Quaternion.identity, session));
                    worker.Owner.Release();
                    UnityEngine.Object.Destroy(worker.gameObject);
                    worker = null;
                    await UniTask.Yield();
                }
            }
            catch (Exception error) { failure = error.ToString(); }
            finally
            {
                if (worker != null) { worker.Owner.Release(); UnityEngine.Object.Destroy(worker.gameObject); }
                owner.Release();
                UnityEngine.Object.Destroy(owner.gameObject);
                UnityEngine.Object.Destroy(definition);
                UnityEngine.Object.Destroy(archetype);
                session.Dispose();
                container.OnReturnToPool();
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, JsonConvert.SerializeObject(new
                {
                    passed = failure == null, checks, failure, coldLoadPending,
                    player = !Application.isEditor, unity = Application.unityVersion
                }, Formatting.Indented));
                Debug.Log("DARK_NIGHTS_ASSEMBLY_PROBE=" + path);
                Application.Quit(failure == null ? 0 : 1);
            }
        }

        private static void Check(Dictionary<string, bool> checks, string key, bool success)
        {
            checks.Add(key, success);
            if (!success) throw new InvalidOperationException("Assembly probe failed: " + key);
        }

        private static void Reject(Dictionary<string, bool> checks, string key, Action action)
        {
            bool rejected = false;
            try { action(); }
            catch (InvalidOperationException) { rejected = true; }
            Check(checks, key, rejected);
        }
    }
}
