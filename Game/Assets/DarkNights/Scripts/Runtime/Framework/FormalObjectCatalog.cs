using System;
using System.Linq;
using DarkNights.Runtime.Network;
using GameCore.NetworkCommands;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Definition;
using GameCore.Objects.NetworkStates;

namespace DarkNights.Runtime.Framework
{
    /// <summary>
    /// 固定首批正式定义的发布身份，并在 AppStartup 完成前验证定义、内容映射和生成注册。
    /// 常量是外部合同；显示名、资产路径和 Addressable 地址由编辑器资产层独立维护。
    /// </summary>
    public static class FormalObjectCatalog
    {
        public const int Protocol = 1;
        public const int SessionLegacyId = 930001;
        public const string RegistryProject = "DNights";
        public const string SessionKey = "session.pinewatch";
        public const string WorkerKey = "unit.worker";
        public const string WorkerContentId = "worker";

        public static void ValidateRuntime(ObjectDefinitionDatabase database = null)
        {
            database = database ?? ObjectDefinitionDatabase.Instance;
            if (database == null) throw new InvalidOperationException("ObjectDefinitionDatabase is not loaded.");
            if (database.IdentityMode != DefinitionIdentityMode.LegacyCompatible || database.EnableOnlineIdService ||
                !string.Equals(database.ProjectName, RegistryProject, StringComparison.Ordinal))
                throw new InvalidOperationException("Formal definition identity settings are not frozen for offline LegacyV1.");
            ObjectDefinition session = database.GetDefinitionByKey(SessionKey);
            ObjectDefinition worker = database.GetDefinitionByKey(WorkerKey);
            if (session == null || worker == null || database.GetDefinition(SessionLegacyId) != session)
                throw new InvalidOperationException("Formal object definitions are missing from the runtime catalog.");
            if (!session.isNetwork || session.Id != SessionLegacyId || session.PrefabRef == null || !session.PrefabRef.RuntimeKeyIsValid())
                throw new InvalidOperationException("Pinewatch session definition is not a valid LegacyV1 network definition.");
            if (!worker.isLocal || worker.Id != 0 || worker.PrefabRef == null || !worker.PrefabRef.RuntimeKeyIsValid())
                throw new InvalidOperationException("Worker definition is not a valid GUID-first local definition.");
            if (session.BehaviourTypes.Count(value => value == typeof(WorldSessionBehaviour).FullName) != 1 ||
                BehaviourTypeResolver.GetFactoryFor(typeof(WorldSessionBehaviour)) == null)
                throw new InvalidOperationException("WorldSessionBehaviour generated registration is missing.");
            ContentDefinitionMap map = session.SharedConfigs.OfType<ContentDefinitionMap>().SingleOrDefault();
            if (map == null) throw new InvalidOperationException("Session definition content map is missing.");
            map.Validate(database);
            if (map.GetRequired(WorkerContentId, database) != worker)
                throw new InvalidOperationException("Worker ContentId resolves to the wrong definition.");
            GenericTypeRegistry<INetworkCommand>.GetId(typeof(SetReadyCommand));
            GenericTypeRegistry<IStateData>.GetId(typeof(SessionStatusState));
        }
    }
}
