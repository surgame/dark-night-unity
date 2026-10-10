using System;
using System.Collections.Generic;
using DarkNights.Entry;
using DarkNights.View.Lighting;
using UnityEngine;

namespace DarkNights.Editor.Lighting
{
    /// <summary>场景光照草稿的本地表现租约；仅绑定引用该原资产的活动会话，记录世界代次，撤除时恢复原资源配置，不修改场景或资产。</summary>
    public sealed class FlashlightDebugScenePreview : IDisposable
    {
        private readonly UnityEngine.Object owner;
        private readonly Dictionary<HeroLightPresentation, int> bound = new Dictionary<HeroLightPresentation, int>();
        public FlashlightDebugScenePreview(UnityEngine.Object owner) { this.owner = owner; }
        public bool WorldChanged
        {
            get
            {
                foreach (var item in bound) if (item.Key == null || !item.Key.isActiveAndEnabled || item.Key.WorldEpoch != item.Value) return true;
                return false;
            }
        }

        public void Apply(SceneLightingProfile source, SceneLightingProfile draft)
        {
            if (source == null || draft == null || !Application.isPlaying) return;
            draft.Settings.Validate();
            foreach (var target in UnityEngine.Object.FindObjectsByType<HeroLightPresentation>())
            {
                if (!target.isActiveAndEnabled || target.LightingProfile != source) continue;
                target.SetDebugScene(owner, draft);
                if (!bound.ContainsKey(target)) bound.Add(target, target.WorldEpoch);
            }
        }

        public void Dispose()
        {
            foreach (var item in bound) if (item.Key != null) item.Key.SetDebugScene(owner, null);
            bound.Clear();
        }
    }
}
