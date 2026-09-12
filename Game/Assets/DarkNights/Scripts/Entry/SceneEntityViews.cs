using System;
using System.Collections.Generic;
using DarkNights.Runtime.Framework;
using DarkNights.View;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.Entry
{
    /// <summary>
    /// 接管 Loader 已装配的场景表现实例，以定义类型提供可重复借还的本地外观。
    /// 身份始终由当前权威投影显式绑定，不以 Loader 激活次序猜测 EntityId。
    /// 归还只隐藏、解绑；场景对象随场景释放，动态对象仍由工厂路径负责销毁。
    /// </summary>
    public sealed class SceneEntityViews
    {
        private readonly Dictionary<ObjectView, string> owners = new Dictionary<ObjectView, string>();
        private readonly HashSet<ObjectView> borrowed = new HashSet<ObjectView>();

        public SceneEntityViews(LevelLayoutAuthoring authoring, Transform parent)
        {
            foreach (LevelPlacementMarker placement in authoring.GetComponentsInChildren<LevelPlacementMarker>(true))
            {
                if (placement.Loader == null || !placement.Loader.IsActivated || placement.View == null)
                    throw new InvalidOperationException("Scene placement loader did not activate: " + placement.name);
                ObjectView view = placement.View;
                SessionEntityViews.RequiredPresentation(view).Unbind();
                owners.Add(view, DefinitionRuleIndex.Kind(placement.Loader.ResolveDefinition()));
                view.transform.SetParent(parent, true);
                view.gameObject.SetActive(false);
            }
        }

        public ObjectView Borrow(string kind)
        {
            foreach (var pair in owners)
            {
                if (pair.Key == null || pair.Value != kind || borrowed.Contains(pair.Key)) continue;
                borrowed.Add(pair.Key);
                pair.Key.gameObject.SetActive(true);
                return pair.Key;
            }
            return null;
        }

        public bool Return(ObjectView view)
        {
            if (view == null || !owners.ContainsKey(view)) return false;
            if (view.Owner != null) SessionEntityViews.RequiredPresentation(view).Unbind();
            borrowed.Remove(view);
            view.gameObject.SetActive(false);
            return true;
        }
    }
}
