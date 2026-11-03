using System;
using System.Linq;
using DarkNights.Core.Config;
using UnityEngine;

namespace DarkNights.Runtime.Physics
{
    /// <summary>
    /// 正式关卡显式选择物理候选的场景装配点；空引用沿用格运动，不在打开场景或导入时改写配置。
    /// 创建布局时冻结Profile，进入现有握手和存档内容身份，后端不能中途切换。
    /// </summary>
    public sealed class PhysicsSceneAuthoring : MonoBehaviour
    {
        public PhysicsProfile Profile;
        public PhysicsPropPlacement[] Props = Array.Empty<PhysicsPropPlacement>();
        public HangingRopePlacement[] HangingRopes = Array.Empty<HangingRopePlacement>();
        public LevelLayout Freeze(LevelLayout source)
        {
            if (Profile == null) throw new InvalidOperationException("物理场景装配缺少Profile。");
            var ropes = HangingRopes.Select(p => p == null
                ? throw new InvalidOperationException("环境吊绳场景引用缺失。") : p.Freeze()).ToArray();
            return new LevelLayout(source.WorldWidth, source.GroundY, source.BuildMinX, source.BuildMaxX,
                source.SpawnX, source.CameraX, source.Buildings, source.Worksites, source.Actors, source.Platforms,
                source.RandomTerrain, source.Expedition, source.LocalWhitebox, source.MapModuleIdentity,
                source.MapModuleSpawnHeight, Profile.Freeze(Props.Select(p =>
                    p == null ? throw new InvalidOperationException("物理场景道具引用缺失。") : p.Freeze(Profile))
                    .Concat(ropes.SelectMany(r => r.InitialBodies())).ToArray(), ropes));
        }
        public void HideAuthoring()
        {
            foreach (var prop in Props) prop.gameObject.SetActive(false);
            foreach (var rope in HangingRopes) rope.gameObject.SetActive(false);
        }
    }
}
