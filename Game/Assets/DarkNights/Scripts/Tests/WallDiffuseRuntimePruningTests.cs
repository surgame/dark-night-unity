using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using DarkNights.Core.Config;
using DarkNights.Editor.Lighting;
using DarkNights.Entry;
using DarkNights.View.Lighting;
using GameCore.Objects.Definition;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>真实地形和后端实例上的连续缓存失效回归；灯数、参数、几何与世界变化沿同一实例对照冻结实现，所有修改仅在隔离预览中。</summary>
    public sealed class WallDiffuseRuntimePruningTests
    {
        private readonly WallDiffuseBenchmarkStage stage = new WallDiffuseBenchmarkStage();
        private readonly List<LightEmitterData> emitters = new List<LightEmitterData>();
        private readonly List<object> comparisons = new List<object>();
        private ExplorationLightField candidate;
        private WallDiffuseMeasuredReferenceField reference;
        private LightEmissionRules original;

        [UnityTest]
        public IEnumerator ReusedBackendMatchesFrozenPixelsAcrossAllInvalidations()
        {
            stage.Open("folded-expanded"); yield return Ready();
            var definition = AssetDatabase.LoadAssetAtPath<ObjectDefinition>("Assets/DarkNights/Res/Objects/Flashlight/Flashlight.asset");
            original = LightProfileResolver.Resolve(LightDefinitionEditor.Preset(definition), LightDefinitionEditor.Config(definition).Overrides, LightDefinitionEditor.DefaultTemplate).Rules;
            var profile = AssetDatabase.LoadAssetAtPath<SceneLightingProfile>("Assets/DarkNights/Res/Shared/Lighting/Profiles/PinewatchLighting.asset");
            candidate = new ExplorationLightField(AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/DarkNights/Res/Shared/Lighting/ExplorationLighting.compute"),
                Core.Logic.Lighting.LightWallDistance.Build, profile.Settings, Core.Logic.Lighting.LightBoundaryBuilder.Build);
            reference = new WallDiffuseMeasuredReferenceField(AssetDatabase.LoadAssetAtPath<ComputeShader>(WallDiffusePruningTests.ReferenceShader),
                Core.Logic.Lighting.LightWallDistance.Build, profile.Settings, Core.Logic.Lighting.LightBoundaryBuilder.Build);
            foreach (string input in new[] { "one", "zero-lamps", "one", "four", "zero-lamps", "four", "zero-intensity", "four",
                "color", "direction", "shape", "hard-shadow", "softness", "aperture", "feather", "range", "cone", "near", "near-position",
                "mixed", "off", "four", "region", "four", "dig", "restore", "new-world", "four" })
            {
                if (input == "region") stage.Region(1);
                else if (input == "dig") stage.Dig(0);
                else if (input == "restore") stage.Dig(1);
                else if (input == "new-world") { stage.Open("rift-expanded"); yield return Ready(); }
                else stage.ResetCamera();
                Emit(input); yield return Settle();
                Compare(input);
                Render(candidate); long uploaded = candidate.DiffuseResources.UploadedBytes;
                Render(candidate);
                Assert.That(candidate.DiffuseResources.UploadedBytes, Is.Zero, "静止输入不应重复上传候选。");
                if (emitters.Count > 0 && input != "off") Assert.That(uploaded, Is.Zero);
            }
        }

        private IEnumerator Ready()
        {
            double deadline = EditorApplication.timeSinceStartup + 120;
            while (!stage.Ready)
            {
                stage.Tick(); if (stage.Error != null) throw stage.Error;
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline)); yield return null;
            }
        }

        private IEnumerator Settle()
        {
            double deadline = EditorApplication.timeSinceStartup + 30;
            for (int i = 0; i < 3 || !candidate.GeometrySettled || !reference.GeometrySettled ||
                candidate.DiffuseResources?.Settled == false || !reference.BoundarySettled; i++)
            {
                stage.Tick(); Render(reference); Render(candidate); yield return null;
                Assert.That(EditorApplication.timeSinceStartup, Is.LessThan(deadline));
            }
        }

        private void Emit(string input)
        {
            emitters.Clear(); int count = input == "zero-lamps" ? 0 : input == "one" ? 1 : 4;
            for (int i = 0; i < count; i++)
            {
                var mode = input == "off" ? LightBounceMode.Off : input == "mixed" && i < 2 ? (LightBounceMode)i : LightBounceMode.Continuous;
                var b = original.Bounce;
                var bounce = new LightBounceRules(mode, b.Bounce, input == "shape" || i % 2 == 1 ? WallDiffuseShape.SoftBlob : WallDiffuseShape.WallBand,
                    b.DiffuseMatchIncident, b.DiffuseCornerRadius, b.DiffuseGain, b.DiffuseNearDistance, b.DiffuseFarDistance,
                    b.DiffuseMaxReach, b.DiffuseLateralSpread, b.DiffuseVisibilityProbes);
                var rules = new LightEmissionRules(input == "range" ? 12 : original.Range, input == "cone" ? 90 : original.Cone,
                    input == "zero-intensity" ? 0 : original.Intensity, input == "near" ? 3 : original.NearRange,
                    input == "near" ? 1 : original.NearIntensity, input == "color" ? .3f : original.Red, original.Green, original.Blue,
                    input == "aperture" ? .6f : original.ApertureWidth, input != "hard-shadow" && original.SoftShadows,
                    input == "softness" ? .4f : original.Softness, input == "feather" ? .1f : original.ConeFeather, bounce);
                Vector3 position = new Vector3(stage.Mouth.x - .5f, -stage.Mouth.y + .5f, 0);
                emitters.Add(new LightEmitterData(position, stage.Angle + i * 2 + (input == "direction" ? 8 : 0), rules,
                    nearPosition: input == "near-position" ? position + new Vector3(.5f, 0, 0) : position));
            }
        }

        private void Render(IEnvironmentLightBackend backend) => backend.Render(stage.Camera, stage.Terrain, emitters);
        private void Compare(string input)
        {
            Render(reference); var rgb = WallDiffuseBenchmarkStage.Pixels(Shader.GetGlobalTexture("_DNLightField"));
            var direction = WallDiffuseBenchmarkStage.Pixels(Shader.GetGlobalTexture("_DNLightDirection"));
            Render(candidate); var next = WallDiffuseBenchmarkStage.Pixels(Shader.GetGlobalTexture("_DNLightField"));
            var nextDirection = WallDiffuseBenchmarkStage.Pixels(Shader.GetGlobalTexture("_DNLightDirection"));
            float rgbError = WallDiffusePruningTests.Maximum(rgb, next), directionError = WallDiffusePruningTests.Maximum(direction, nextDirection);
            comparisons.Add(new { input, lights = emitters.Count, rgbError, directionError,
                tolerance = "3e-5 absolute or one RGBAHalf ULP per channel, whichever is larger" });
            QuantizedEqual(rgb, next, input + " RGB"); QuantizedEqual(direction, nextDirection, input + " direction");
        }

        private static void QuantizedEqual(Color[] first, Color[] next, string input)
        {
            for (int i = 0; i < first.Length; i++) for (int channel = 0; channel < 4; channel++)
            {
                float magnitude = Mathf.Max(Mathf.Abs(first[i][channel]), Mathf.Abs(next[i][channel]));
                float step = Mathf.Pow(2, Mathf.Max(-14, Mathf.Floor(Mathf.Log(Mathf.Max(magnitude, .00006103515625f), 2))) - 10);
                Assert.That(Mathf.Abs(first[i][channel] - next[i][channel]), Is.LessThanOrEqualTo(Mathf.Max(.00003f, step)), input);
            }
        }

        [UnityTearDown]
        public IEnumerator Retire()
        {
            var tasks = new[] { candidate?.RetireAsync(), reference?.RetireAsync() };
            foreach (var task in tasks)
            {
                if (task == null) continue;
                while (!task.IsCompleted) yield return null;
                task.GetAwaiter().GetResult();
            }
            stage.Dispose();
            string path = Path.GetFullPath("../artifacts/light-pruning-20261013/runtime-invalidations-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path, Newtonsoft.Json.JsonConvert.SerializeObject(comparisons, Newtonsoft.Json.Formatting.Indented));
        }
    }
}
