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
    /// 创建建造预览和残骸所需的被动实体主视图，复用正式 Definition、PrefabRef 与对象装配入口。
    /// 被动视图没有会话身份或写权限，调用者拥有其取消及释放；活实体仍由对象世界创建。
    /// </summary>
    public static class EntityViewFactory
    {
        public static async UniTask<EntityView> Create(string ruleKey, Transform parent)
        {
            var definition = new DefinitionRuleIndex(ObjectDefinitionDatabase.Instance).GetRequired(ruleKey);
            ObjectView owner = await ObjectInstanceFactory.CreateObjectInstanceAsync(
                definition, Vector3.zero, Quaternion.identity, parent);
            try
            {
                EntityView view = owner as EntityView;
                if (view == null) throw new InvalidOperationException("Definition does not use an EntityView: " + ruleKey);
                if (RequiredPresentation(view).IsBound)
                    throw new InvalidOperationException("Passive entity view already has a live identity: " + ruleKey);
                return view;
            }
            catch { Release(owner); throw; }
        }

        public static EntityPresentationBehaviour RequiredPresentation(EntityView owner)
        {
            if (owner == null || owner.Owner == null) throw new InvalidOperationException("Entity view owner is not assembled.");
            EntityPresentationBehaviour value = owner.Owner.GetAllBehaviors().OfType<EntityPresentationBehaviour>().Single();
            if (value.Visual != owner) throw new InvalidOperationException("Presentation does not reference its primary EntityView.");
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
