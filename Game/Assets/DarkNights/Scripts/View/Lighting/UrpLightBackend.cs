using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace DarkNights.View.Lighting
{
    /// <summary>原生 URP 2D 回退后端；冻结光源转成真实 Light2D，复用模板法线设置，拥有灯形与局部地形遮挡实例。</summary>
    public sealed class UrpLightBackend : IEnvironmentLightBackend
    {
        private const int Capacity = 16;
        private readonly Light2D template;
        private readonly ExplorationLightSettings settings;
        private readonly UrpTerrainShadows shadows;
        private readonly Func<float, float, float, float, float, float, bool, float> profile;
        private readonly List<(Light2D Light, UrpLightCookie Cookie)> lights = new List<(Light2D, UrpLightCookie)>();
        private readonly List<(Light2D Light, UrpLightCookie Cookie)> nearLights = new List<(Light2D, UrpLightCookie)>();
        private Light2D ambient;
        private Transform root;
        private bool retired;
        public int SourceCount { get; private set; }

        public UrpLightBackend(Light2D lightTemplate, ShadowCaster2D shadowTemplate, ExplorationLightSettings parameters,
            Func<float, float, float, float, float, float, bool, float> beamProfile)
        {
            if (lightTemplate == null || shadowTemplate == null || shadowTemplate.GetComponent<PolygonCollider2D>() == null)
                throw new InvalidOperationException("URP 回退模板未安装；执行 Dark Nights/Tools/安装照明后端资源。");
            template = lightTemplate; settings = parameters; shadows = new UrpTerrainShadows(shadowTemplate);
            profile = beamProfile ?? throw new ArgumentNullException(nameof(beamProfile));
        }

        public void Render(Camera camera, Terrain.TerrainPreview preview, IReadOnlyList<LightEmitterData> emitters)
        {
            if (retired || preview?.LightingSource == null || !preview.LightingLoadedBounds.IsValid) { Suspend(); return; }
            if (root == null)
            {
                root = new GameObject("Dark Nights URP lighting") { hideFlags = HideFlags.DontSave }.transform;
                root.SetParent(preview.transform, false);
                ambient = Object.Instantiate(template, root, false); ambient.name = "URP ambient";
                ambient.lightType = Light2D.LightType.Global; ambient.shadowsEnabled = false; ambient.lightCookieSprite = null;
            }
            root.gameObject.SetActive(true); ambient.gameObject.SetActive(true);
            ambient.color = Color.white; ambient.intensity = Mathf.Clamp01(settings.Ambient);
            SourceCount = Math.Min(Capacity, emitters.Count);
            while (lights.Count < SourceCount)
            {
                var light = Object.Instantiate(template, root, false);
                light.name = "URP environment " + lights.Count; light.lightType = Light2D.LightType.Sprite;
                lights.Add((light, new UrpLightCookie(profile)));
            }
            for (int n = 0; n < lights.Count; n++)
            {
                var slot = lights[n]; slot.Light.gameObject.SetActive(n < SourceCount);
                if (n >= SourceCount) continue;
                var source = emitters[n]; slot.Cookie.Update(source, settings);
                slot.Light.gameObject.SetActive(CanEmit(preview, source.Position));
                slot.Light.lightCookieSprite = slot.Cookie.Sprite;
                slot.Light.transform.position = source.Position;
                Vector3 local = preview.transform.InverseTransformVector(new Vector3(Mathf.Cos(source.Angle * Mathf.Deg2Rad), Mathf.Sin(source.Angle * Mathf.Deg2Rad), 0));
                slot.Light.transform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg);
                slot.Light.color = new Color(source.Rules.Red, source.Rules.Green, source.Rules.Blue);
                slot.Light.intensity = source.Rules.Intensity * Mathf.Clamp(settings.IntensityScale, 0, 2);
                slot.Light.shadowsEnabled = true; slot.Light.shadowIntensity = 1;
                slot.Light.shadowSoftness = settings.SoftShadows ? Mathf.Clamp(settings.Softness, 0, .75f) : 0;
            }
            UpdateNear(preview, emitters);
            shadows.Update(preview);
            ExplorationLightField.Suspend();
            Shader.SetGlobalFloat("_DNLightingBackend", 2); Shader.SetGlobalFloat("_DNLightingActive", 1);
        }

        public void Suspend()
        {
            if (root != null) root.gameObject.SetActive(false);
            shadows.Suspend(); SourceCount = 0;
            ExplorationLightField.Suspend(); Shader.SetGlobalFloat("_DNLightingBackend", 0);
        }

        public Task RetireAsync()
        {
            if (retired) return Task.CompletedTask;
            retired = true; Suspend(); shadows.Dispose();
            foreach (var slot in lights) { slot.Light.lightCookieSprite = null; slot.Cookie.Dispose(); }
            foreach (var slot in nearLights) { slot.Light.lightCookieSprite = null; slot.Cookie.Dispose(); }
            lights.Clear();
            nearLights.Clear();
            if (root != null) { if (Application.isPlaying) Object.Destroy(root.gameObject); else Object.DestroyImmediate(root.gameObject); }
            root = null; ambient = null; return Task.CompletedTask;
        }

        private void UpdateNear(Terrain.TerrainPreview preview, IReadOnlyList<LightEmitterData> emitters)
        {
            for (int n = 0; n < SourceCount; n++)
            {
                var source = emitters[n];
                float strength = source.Rules.NearIntensity * Mathf.Clamp(settings.NearStrength, 0, 2);
                if (strength <= 0 || !CanEmit(preview, source.NearPosition))
                { if (n < nearLights.Count) nearLights[n].Light.gameObject.SetActive(false); continue; }
                while (nearLights.Count <= n)
                {
                    var light = Object.Instantiate(template, root, false); light.name = "URP near " + nearLights.Count;
                    light.lightType = Light2D.LightType.Sprite;
                    nearLights.Add((light, new UrpLightCookie(profile)));
                }
                var slot = nearLights[n]; slot.Cookie.Update(source, settings, true);
                slot.Light.lightCookieSprite = slot.Cookie.Sprite;
                slot.Light.transform.position = source.NearPosition; slot.Light.transform.localRotation = Quaternion.identity;
                slot.Light.color = new Color(source.Rules.Red, source.Rules.Green, source.Rules.Blue);
                slot.Light.shadowSoftness = settings.SoftShadows ? Mathf.Clamp(settings.Softness, 0, .75f) : 0;
                slot.Light.intensity = strength; slot.Light.gameObject.SetActive(true);
            }
            for (int n = SourceCount; n < nearLights.Count; n++) nearLights[n].Light.gameObject.SetActive(false);
        }

        private static bool CanEmit(Terrain.TerrainPreview preview, Vector3 position)
        {
            Vector3 local = preview.transform.InverseTransformPoint(position);
            float u = local.x + .5f, row = -local.y + .5f;
            var bounds = preview.LightingLoadedBounds; int top = 1 - (int)bounds.MaxVExclusive;
            if (u < bounds.MinU || u >= bounds.MaxUExclusive || row < top || row >= top + bounds.Height) return false;
            byte cell = preview.LightingSource.LightingCell(Mathf.FloorToInt(u), Mathf.FloorToInt(row));
            return (cell & 128) != 0 && ((cell & 64) == 0 || !Core.Logic.Terrain.TerrainShapeGeometry.Contains(
                (Core.Config.Terrain.TerrainCellShape)(cell & 15), u - Mathf.Floor(u), 1 - (row - Mathf.Floor(row))));
        }
    }
}
