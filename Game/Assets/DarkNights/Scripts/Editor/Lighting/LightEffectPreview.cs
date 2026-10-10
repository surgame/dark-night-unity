using System;
using DarkNights.View;
using DarkNights.View.Lighting;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor.Lighting
{
    /// <summary>Prefab 制作的临时光效与范围预览；读取同一配置和灯口，实例不保存，不启动业务、网络或随机数，退出面板即退休。</summary>
    public sealed class LightEffectPreview : IDisposable
    {
        private readonly LightEffectBinding binding = new LightEffectBinding();
        private readonly UnityEngine.Object context = ScriptableObject.CreateInstance<LightProfile>();
        private Transform anchor;
        private LightProfileSnapshot profile;
        private bool disposed;
        public LightEffectPreview() { context.hideFlags = HideFlags.HideAndDontSave; SceneView.duringSceneGui += Draw; }

        public void Bind(LightProfileSnapshot value, FlashlightView owner) => Bind(value, owner.Emitter);
        public void Bind(LightProfileSnapshot value, Transform owner)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LightEffectPreview));
            if (anchor != owner) binding.Dispose();
            profile = value; anchor = owner;
            binding.Bind(value, context, owner, owner, owner, Array.Empty<SpriteRenderer>());
            binding.Effect.gameObject.hideFlags = HideFlags.HideAndDontSave;
            foreach (var component in binding.Effect.GetComponentsInChildren<Transform>(true)) component.gameObject.hideFlags = HideFlags.HideAndDontSave;
            binding.SetOn(true);
        }

        private void Draw(SceneView scene)
        {
            if (anchor == null || profile == null || binding.Effect == null) return;
            var source = binding.Effect.Environment.Sample(null);
            float distance = profile.Rules.Range * .08f;
            using (new Handles.DrawingScope(new Color(profile.Rules.Red, profile.Rules.Green, profile.Rules.Blue)))
            {
                if (profile.LocalFillEnabled) Handles.DrawWireDisc(source.Position, Vector3.forward, profile.FillRadius * .08f);
                if (!profile.EnvironmentEnabled) return;
                if (!profile.Directional)
                {
                    Handles.DrawWireDisc(source.Position, Vector3.forward, distance);
                    Handles.Label(source.Position, "光效安装预览（1 格 = 0.08 世界单位）"); return;
                }
                float half = profile.Rules.Cone * .5f;
                Vector3 Direction(float angle) => new Vector3(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad), 0);
                Handles.DrawLine(source.Position, source.Position + Direction(source.Angle - half) * distance);
                Handles.DrawLine(source.Position, source.Position + Direction(source.Angle + half) * distance);
                Handles.DrawWireArc(source.Position, Vector3.forward, Direction(source.Angle - half), profile.Rules.Cone, distance);
                Handles.Label(source.Position, "光效安装预览（1 格 = 0.08 世界单位）");
            }
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            SceneView.duringSceneGui -= Draw; binding.Dispose(); UnityEngine.Object.DestroyImmediate(context); anchor = null; profile = null;
        }
    }
}
