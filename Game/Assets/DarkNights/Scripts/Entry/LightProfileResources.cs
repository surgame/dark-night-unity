using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using DarkNights.Runtime.Framework;
using DarkNights.Runtime.Objects;
using DarkNights.View.Lighting;
using GameCore.Objects.Definition;

namespace DarkNights.Entry
{
    /// <summary>一局已预加载光照预设的所有者；同预设只加载一次，按 Definition 合成缓存，所有实例退休后归还资源，不缓存业务状态。</summary>
    public sealed class LightProfileResources : IDisposable
    {
        private readonly Dictionary<string, LightProfileLease> presets = new Dictionary<string, LightProfileLease>();
        private readonly Dictionary<ObjectDefinition, LightProfileSnapshot> resolved = new Dictionary<ObjectDefinition, LightProfileSnapshot>();
        private readonly Dictionary<ObjectDefinition, int> revisions = new Dictionary<ObjectDefinition, int>();
        private readonly Dictionary<ObjectDefinition, LightProfile> sources = new Dictionary<ObjectDefinition, LightProfile>();
        private readonly Dictionary<string, LightProfile> editorPresets = new Dictionary<string, LightProfile>();
        private bool disposed;
        private readonly LightEffect defaultTemplate;

        private LightProfileResources(LightEffect template)
        {
            defaultTemplate = template ?? throw new ArgumentNullException(nameof(template));
            defaultTemplate.ValidateStructure();
        }

        public static async UniTask<LightProfileResources> Prepare(IReadOnlyList<ObjectDefinition> definitions, LightEffect defaultTemplate, CancellationToken cancellationToken)
        {
            string[] guids = definitions.Select(RequireConfig).Where(HasPreset).Select(value => value.Profile.AssetGUID).Distinct().ToArray();
            var result = new LightProfileResources(defaultTemplate);
            try
            {
                var leases = await StartupResourceBatch.Load(guids, LightProfileLease.Load, cancellationToken);
                for (int i = 0; i < leases.Length; i++) result.presets.Add(guids[i], leases[i]);
                foreach (var definition in definitions) result.Resolve(definition);
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        public LightProfileSnapshot Resolve(ObjectDefinition definition)
        {
            if (disposed) throw new ObjectDisposedException(nameof(LightProfileResources));
            var config = RequireConfig(definition);
            LightProfile source = HasPreset(config) ? Preset(config.Profile.AssetGUID) : null;
            int revision = source != null ? source.Revision : 0;
            if (resolved.TryGetValue(definition, out var cached) && sources[definition] == source && revisions[definition] == revision) return cached;
            cached = LightProfileResolver.Resolve(source, config.Overrides, defaultTemplate);
            resolved[definition] = cached; sources[definition] = source; revisions[definition] = revision; return cached;
        }

        public void Invalidate() { resolved.Clear(); revisions.Clear(); sources.Clear(); }

        public void SetEditorPreset(string guid, LightProfile preset)
        {
            if (preset == null) throw new ArgumentNullException(nameof(preset));
            preset.Freeze(); editorPresets[guid] = preset; Invalidate();
        }

        public static FlashlightToolConfig RequireConfig(ObjectDefinition definition)
        {
            var config = definition?.SharedConfigs.OfType<FlashlightToolConfig>().SingleOrDefault();
            if (config == null || config.Overrides == null ||
                (!string.IsNullOrEmpty(config.Profile?.AssetGUID) && !HasPreset(config)))
                throw new InvalidOperationException("照明 Definition 缺少覆盖配置或引用了无效预设：" + definition?.Key);
            return config;
        }

        public static bool HasPreset(FlashlightToolConfig config) => config?.Profile != null && config.Profile.RuntimeKeyIsValid();

        private LightProfile Preset(string guid)
        {
            if (editorPresets.TryGetValue(guid, out var author)) return author;
            if (presets.TryGetValue(guid, out var lease)) return lease.Profile;
            throw new InvalidOperationException("Definition 更换了未预加载的光照预设，请重开当前会话：" + guid);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true; Invalidate(); editorPresets.Clear();
            foreach (var lease in presets.Values) lease.Dispose(); presets.Clear();
        }
    }
}
