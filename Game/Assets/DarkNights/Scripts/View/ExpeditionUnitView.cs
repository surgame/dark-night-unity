using System;
using DarkNights.Core.ViewData;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>搬运机器人与侦察机的原生逐帧表现；通过 Prefab 引用替代旧居民美术，位置和工作阶段不在本地推进。</summary>
    public sealed class ExpeditionUnitView : MonoBehaviour
    {
        public SpriteRenderer Body;
        public SpriteRenderer[] Legacy = Array.Empty<SpriteRenderer>();
        public Sprite[] Frames = Array.Empty<Sprite>();
        public bool Drone;
        public void Present(ActorViewData actor)
        {
            foreach (var renderer in Legacy) renderer.enabled = false;
            Body.enabled = true;
            Body.sprite = Frames[actor != null && (Drone || actor.Walking) ? (int)(actor.ActionTime * 8) % Frames.Length : 0];
        }
    }
}
