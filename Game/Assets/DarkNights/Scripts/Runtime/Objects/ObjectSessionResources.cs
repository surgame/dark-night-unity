using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkNights.Runtime.Framework;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 一批已准备定义的 Addressables 租约；会话对象释放后才能归还资源。
    /// 加载在会话事务之外，创建只走 YYGC 的同步准备入口，不在支付中间 await。
    /// </summary>
    public sealed class ObjectSessionResources : IDisposable
    {
        private readonly Dictionary<string, PreparedObjectDefinition> prepared =
            new Dictionary<string, PreparedObjectDefinition>(StringComparer.Ordinal);
        private bool disposed;
        public IReadOnlyList<ObjectDefinition> Definitions { get; private set; }
        public EquipmentDefinitionCatalog Equipment { get; private set; }

        private ObjectSessionResources() { }

        public static async UniTask<ObjectSessionResources> Prepare(
            IReadOnlyList<ObjectDefinition> definitions, CancellationToken cancellationToken)
        {
            var result = new ObjectSessionResources();
            try
            {
                result.Equipment = new EquipmentDefinitionCatalog(ObjectDefinitionDatabase.Instance);
                ObjectDefinition[] all = definitions.Concat(result.Equipment.MiningDefinitions).Distinct().ToArray();
                PreparedObjectDefinition[] leases = await StartupResourceBatch.Load(all,
                    ObjectInstanceFactory.PrepareAsync, cancellationToken);
                int stored = 0;
                try
                {
                    for (; stored < leases.Length; stored++)
                        result.prepared.Add(all[stored].Guid.ToString(), leases[stored]);
                }
                catch
                {
                    for (int i = stored; i < leases.Length; i++) leases[i].Dispose();
                    throw;
                }
                result.Definitions = definitions.ToArray();
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        public ObjectView Create(ObjectDefinition definition, ObjectSessionContext context, Transform parent)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ObjectSessionResources));
            if (!prepared.TryGetValue(definition.Guid.ToString(), out var lease) || lease.Definition != definition)
                throw new InvalidOperationException("Definition has not been prepared: " + definition.Key);
            return lease.Create(Vector3.zero, Quaternion.identity, context, parent);
        }

        public ObjectDefinition Find(string ruleKey)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ObjectSessionResources));
            return Definitions.SingleOrDefault(d => Rule(d) == ruleKey) ??
                throw new InvalidOperationException("Rule capability is not prepared: " + ruleKey);
        }

        public static string Rule(ObjectDefinition definition)
        {
            var configs = definition.SharedConfigs.Where(value => value is ActorRuleConfig ||
                value is BuildingRuleConfig || value is WorksiteRuleConfig || value is MineralDepositRuleConfig).ToArray();
            if (configs.Length != 1) throw new InvalidOperationException("Definition requires exactly one family RuleKey: " + definition.Key);
            if (configs[0] is ActorRuleConfig actor) return actor.RuleKey;
            if (configs[0] is BuildingRuleConfig building) return building.RuleKey;
            if (configs[0] is MineralDepositRuleConfig deposit) return deposit.RuleKey;
            return ((WorksiteRuleConfig)configs[0]).RuleKey;
        }

        public ObjectDefinition FindOptional(string ruleKey)
        {
            if (disposed) throw new ObjectDisposedException(nameof(ObjectSessionResources));
            return Definitions.SingleOrDefault(d => Rule(d) == ruleKey);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            foreach (PreparedObjectDefinition value in prepared.Values) value.Dispose();
            prepared.Clear();
        }
    }
}
