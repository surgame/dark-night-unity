using System;
using DarkNights.Editor.Lighting;
using DarkNights.View.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>独立数值积分 oracle 对照实际 HLSL 函数；同时验证旧配置默认模式、新字段保存白名单和距离合法性。</summary>
    public sealed class WallDiffuseMathTests
    {
        [Test]
        public void ExactShaderIntegralMatchesIndependentFineQuadrature()
        {
            var shader = AssetDatabase.LoadAssetAtPath<ComputeShader>("Assets/DarkNights/Scripts/Tests/WallDiffuseMathProbe.compute");
            var inputs = new[] { new Vector4(0, 1, 0, 1), new Vector4(.25f, .7f, 1, .9f),
                new Vector4(-1, 2, 2, .1f), new Vector4(0, 2, .5f, 1),
                new Vector4(2, 3, 1, 1), new Vector4(0, .2f, 0, 0),
                new Vector4(0, 4, 1, 1), new Vector4(2, .5f, 1, 0) };
            using (var source = new ComputeBuffer(8, 16)) using (var output = new ComputeBuffer(4, 16))
            {
                source.SetData(inputs); int kernel = shader.FindKernel("Probe");
                shader.SetBuffer(kernel, "_Inputs", source); shader.SetBuffer(kernel, "_Results", output); shader.Dispatch(kernel, 4, 1, 1);
                var results = new Vector4[4]; output.GetData(results);
                for (int j = 0; j < 4; j++)
                {
                    var a = inputs[j * 2]; var b = inputs[j * 2 + 1]; double sum = 0, dx = (a.y - a.x) / 200000.0;
                    for (int i = 0; i < 200000; i++)
                    {
                        double x = a.x + (i + .5) * dx;
                        double p = a.z + (a.w - a.z) * (x - a.x) / (a.y - a.x);
                        sum += p * Math.Max(0, 1 - Math.Abs(x - b.x) / b.y) * dx;
                    }
                    Assert.That(results[j].x, Is.EqualTo(sum).Within(.00001));
                    double excess = Math.Max(0, b.z - b.w), epsilon = .04 * b.z;
                    double expected = excess == 0 ? 0 : excess < epsilon ? excess * excess / (2 * epsilon) : excess - epsilon / 2;
                    Assert.That(results[j].y, Is.EqualTo(expected).Within(.00001));
                }
            }
        }

        [Test]
        public void MissingNewFieldsStayLegacyAndAllFieldsRoundTrip()
        {
            var old = JsonUtility.FromJson<ExplorationLightSettings>("{\"Ambient\":0.1,\"Bounce\":0.12}");
            Assert.That(old.BounceMode, Is.EqualTo(LightBounceMode.LegacyPoint)); old.Validate();
            old.BounceMode = LightBounceMode.Continuous; old.DiffuseGain = .75f; old.DiffuseVisibilityProbes = 4;
            var roundTrip = JsonUtility.FromJson<ExplorationLightSettings>(JsonUtility.ToJson(old));
            foreach (var field in typeof(ExplorationLightSettings).GetFields())
            {
                Assert.That(FlashlightDebugSave.SceneFields, Does.Contain(field.Name));
                Assert.That(field.GetValue(roundTrip), Is.EqualTo(field.GetValue(old)));
            }
            roundTrip.DiffuseFarDistance = roundTrip.DiffuseNearDistance;
            Assert.Throws<ArgumentException>(() => roundTrip.Validate());
        }

        [Test]
        public void SceneDraftUndoesAndRedoesNewModeAndIntegerProbeCount()
        {
            var draft = ScriptableObject.CreateInstance<SceneLightingProfile>(); var history = new FlashlightDebugUndo(new FlashlightDebugHistory());
            try
            {
                history.Change(draft, "diffuse", "连续光测试", () => { draft.Settings.BounceMode = LightBounceMode.Continuous; draft.Settings.DiffuseVisibilityProbes = 4; });
                history.UndoTemporary(); Assert.That(draft.Settings.BounceMode, Is.EqualTo(LightBounceMode.LegacyPoint));
                history.RedoTemporary(); Assert.That(draft.Settings.DiffuseVisibilityProbes, Is.EqualTo(4));
            }
            finally { history.ClearTemporary(); UnityEngine.Object.DestroyImmediate(draft); }
        }
    }
}
