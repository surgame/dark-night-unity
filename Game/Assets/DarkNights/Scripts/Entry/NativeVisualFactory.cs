using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Runtime.Framework;
using DarkNights.View;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.Entry
{
    /// <summary>
    /// 创建建造预览和残骸所需的被动原生外观，复用正式 Definition、PrefabRef 与生成绑定。
    /// 被动外观没有会话身份或写权限，调用者拥有其取消及释放；游戏实体不经此处创建。
    /// </summary>
    public static class NativeVisualFactory
    {
        public static async UniTask<ObjectView> Create(string ruleKey, Transform parent)
        {
            var definition = new DefinitionRuleIndex(ObjectDefinitionDatabase.Instance).GetRequired(ruleKey);
            ObjectView owner = await ObjectInstanceFactory.CreateObjectInstanceAsync(
                definition, Vector3.zero, Quaternion.identity, parent);
            try
            {
                if (RequiredPresentation(owner).IsBound)
                    throw new InvalidOperationException("Passive visual already has a live entity: " + ruleKey);
                return owner;
            }
            catch { Release(owner); throw; }
        }

        public static EntityPresentationBehaviour RequiredPresentation(ObjectView owner)
        {
            if (owner == null || owner.Owner == null) throw new InvalidOperationException("Native visual owner is not assembled.");
            EntityPresentationBehaviour value = owner.Owner.GetAllBehaviors().OfType<EntityPresentationBehaviour>().Single();
            if (value.Visual == null) throw new InvalidOperationException("Missing generated native visual binding.");
            return value;
        }

        public static void Release(ObjectView owner)
        {
            if (owner == null) return;
            owner.Owner?.Release();
            if (Application.isPlaying) UnityEngine.Object.Destroy(owner.gameObject);
            else UnityEngine.Object.DestroyImmediate(owner.gameObject);
        }
    }
}
