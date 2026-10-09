using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace DarkNights.View.Lighting
{
    /// <summary>相机照明的后端切换与资源所有者；保留统一调参，旧后端先停用再异步退休，失败不擅自改变用户选择。</summary>
    public sealed class EnvironmentLighting
    {
        private readonly ComputeShader shader;
        private readonly Light2D lightTemplate;
        private readonly ShadowCaster2D shadowTemplate;
        private readonly Func<Core.ViewData.LightGeometrySnapshot, Func<bool>, float[]> wallCalculator;
        private readonly Func<float, float, float, float, float, float, bool, float> profile;
        private readonly List<Task> retiring = new List<Task>();
        private IEnvironmentLightBackend backend;
        private LightingBackendKind? installed;
        private bool retired;
        public ExplorationLightSettings Settings { get; }
        public int SourceCount => backend?.SourceCount ?? 0;

        public EnvironmentLighting(LightEnvironmentEmitter source,
            Func<Core.ViewData.LightGeometrySnapshot, Func<bool>, float[]> calculator,
            Func<float, float, float, float, float, float, bool, float> beamProfile,
            ExplorationLightSettings settings = null)
        {
            wallCalculator = calculator ?? throw new ArgumentNullException(nameof(calculator));
            profile = beamProfile ?? throw new ArgumentNullException(nameof(beamProfile));
            Settings = settings ?? new ExplorationLightSettings { Backend = InitialBackend(System.Environment.GetCommandLineArgs()) };
            shader = source.LightingShader;
            lightTemplate = source.UrpLightTemplate; shadowTemplate = source.UrpShadowTemplate;
        }

        public static LightingBackendKind InitialBackend(IEnumerable<string> args)
        {
            var choice = LightingBackendKind.PrivateField;
            foreach (string arg in args)
                if (arg == "--dn-lighting-urp") choice = LightingBackendKind.Urp2D;
                else if (arg == "--dn-lighting-private") choice = LightingBackendKind.PrivateField;
            return choice;
        }

        public void Render(Camera camera, Terrain.TerrainPreview preview, IReadOnlyList<LightEmitterData> emitters)
        {
            if (retired) return;
            if (!Enum.IsDefined(typeof(LightingBackendKind), Settings.Backend))
                throw new InvalidOperationException("未知照明后端。");
            if (installed != Settings.Backend)
            {
                backend?.Suspend();
                if (backend != null) retiring.Add(backend.RetireAsync());
                backend = null; installed = null;
                if (Settings.Backend == LightingBackendKind.PrivateField)
                    backend = new ExplorationLightField(shader, wallCalculator, Settings);
                else backend = new UrpLightBackend(lightTemplate, shadowTemplate, Settings, profile);
                installed = Settings.Backend;
            }
            retiring.RemoveAll(task => task.IsCompletedSuccessfully);
            backend.Render(camera, preview, emitters);
        }

        public void Suspend()
        {
            backend?.Suspend(); ExplorationLightField.Suspend();
            Shader.SetGlobalFloat("_DNLightingBackend", 0);
        }

        public async Task RetireAsync()
        {
            if (retired) return;
            retired = true; Suspend();
            if (backend != null) retiring.Add(backend.RetireAsync());
            backend = null; installed = null;
            await Task.WhenAll(retiring);
        }
    }
}
