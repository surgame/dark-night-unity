using System;
using System.Linq;
using AnyRules.Next;
using AnyRules.Next.Networking;

namespace DarkNights.Runtime.Terrain
{
    /// <summary>地图网络接线入口；复用 AnyRuleD 协议，客户端只持有副本。权限版本由可信宿主在权限变化前推进。</summary>
    public static class TerrainMapNetworking
    {
        public static MapHandshake Handshake(TerrainMapAuthority authority, ServerGameplayCatalog gameplay, string visualDigest)
        {
            if (authority == null || gameplay == null) throw new ArgumentNullException(nameof(authority));
            return new MapHandshake(authority.Descriptor, gameplay.ContentDigest, visualDigest,
                gameplay.Definitions.Select(d => d.Identity.Guid).ToArray());
        }
        public static MapInterestService OpenStream(TerrainMapAuthority authority, MapHandshake handshake,
            ulong sessionToken, Func<CellCoord, bool> authorize, Func<ulong> permissionRevision)
        {
            if (permissionRevision == null) throw new ArgumentNullException(nameof(permissionRevision));
            return new MapInterestService(authority, authority.Tiles, handshake, sessionToken, authorize,
                () => authority.CommitId, permissionRevision);
        }
        public static ChunkReplicaStateMachine CreateReplica(ServerGameplayCatalog gameplay, string visualDigest)
        {
            if (gameplay == null) throw new ArgumentNullException(nameof(gameplay));
            return new ChunkReplicaStateMachine(gameplay.Tiles, gameplay.ContentDigest, visualDigest);
        }
    }
}
