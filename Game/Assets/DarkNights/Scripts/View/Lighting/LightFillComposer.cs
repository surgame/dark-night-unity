using System;
using System.Collections.Generic;
using UnityEngine;

namespace DarkNights.View.Lighting
{
    /// <summary>相机拥有的局部补光合成器；每个受光对象最多四个补光贡献，保留其他材质属性并在移除或退休时清除本组件数据。</summary>
    public sealed class LightFillComposer
    {
        private const int Capacity = 4;
        private static readonly int CountKey = Shader.PropertyToID("_DNLocalFillCount");
        private static readonly int OriginKey = Shader.PropertyToID("_DNLocalFillOrigins");
        private static readonly int ColorKey = Shader.PropertyToID("_DNLocalFillColors");
        private readonly Dictionary<SpriteRenderer, Buffer> buffers = new Dictionary<SpriteRenderer, Buffer>();
        private readonly List<SpriteRenderer> removed = new List<SpriteRenderer>();
        private MaterialPropertyBlock properties;

        /// <summary>单个 Renderer 的有界临时贡献；只缓存视图参数，每帧重新计数，不成为装备或照明状态所有者。</summary>
        private sealed class Buffer
        {
            internal int Count;
            internal readonly Vector4[] Origins = new Vector4[Capacity];
            internal readonly Vector4[] Colors = new Vector4[Capacity];
        }

        public void Apply(IReadOnlyList<LightEffect> sources, float strength, Vector2? gridScale = null)
        {
            foreach (var buffer in buffers.Values) buffer.Count = 0;
            foreach (var effect in sources)
            {
                if (!effect.IsOn || !effect.LocalFillEnabled || !effect.LocalFill.isActiveAndEnabled) continue;
                var fill = effect.LocalFill;
                foreach (var receiver in fill.Targets)
                {
                    if (receiver == null) throw new InvalidOperationException("局部补光受光对象已失效。");
                    if (!buffers.TryGetValue(receiver, out var buffer)) buffers.Add(receiver, buffer = new Buffer());
                    if (buffer.Count >= Capacity) continue;
                    Vector4 origin = fill.OriginRadius;
                    Vector2 scale = gridScale ?? Vector2.one;
                    origin.z *= Mathf.Abs(scale.x); origin.w *= Mathf.Abs(scale.y);
                    buffer.Origins[buffer.Count] = origin;
                    Vector4 energy = fill.Energy; energy.w *= Mathf.Clamp(strength, 0, 2);
                    buffer.Colors[buffer.Count++] = energy;
                }
            }
            removed.Clear();
            // 合成器可作为 MonoBehaviour 的托管字段；原生属性块只在主线程实际合成时创建。
            properties = properties ?? new MaterialPropertyBlock();
            foreach (var entry in buffers)
            {
                if (entry.Key == null) { removed.Add(entry.Key); continue; }
                properties.Clear(); entry.Key.GetPropertyBlock(properties);
                properties.SetFloat(CountKey, entry.Value.Count);
                properties.SetVectorArray(OriginKey, entry.Value.Origins);
                properties.SetVectorArray(ColorKey, entry.Value.Colors);
                entry.Key.SetPropertyBlock(properties);
                if (entry.Value.Count == 0) removed.Add(entry.Key);
            }
            foreach (var receiver in removed) buffers.Remove(receiver);
        }
        public void Clear() { Apply(Array.Empty<LightEffect>(), 0); }
    }
}
