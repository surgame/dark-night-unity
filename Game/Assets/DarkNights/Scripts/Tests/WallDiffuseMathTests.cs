using DarkNights.Core.Config;
using System;
using System.Linq;
using DarkNights.Editor.Lighting;
using DarkNights.Runtime.Objects;
using DarkNights.View.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Tests
{
    /// <summary>独立数值积分 oracle 对照实际 HLSL 函数；验证单灯默认返光、覆盖字段序列化、距离合法性及临时撤销，不写作者资产。</summary>
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
            var old = new LightOverrides();
            JsonUtility.FromJsonOverwrite("{\"Range\":18}", old);
            Assert.That(old.BounceMode, Is.EqualTo(LightBounceMode.LegacyPoint));
            old.BounceMode = LightBounceMode.Continuous; old.DiffuseGain = .75f; old.DiffuseVisibilityProbes = 4;
            var roundTrip = JsonUtility.FromJson<LightOverrides>(JsonUtility.ToJson(old));
            foreach (var field in typeof(LightOverrides).GetFields().Where(field => field.Name != "Mask"))
            {
                Assert.That(LightParameterSchema.Fields.Select(item => item.Name), Does.Contain(field.Name));
                Assert.That(field.GetValue(roundTrip), Is.EqualTo(field.GetValue(old)));
            }
            roundTrip.DiffuseFarDistance = roundTrip.DiffuseNearDistance;
            Assert.Throws<ArgumentException>(() => new LightBounceRules(diffuseNearDistance: roundTrip.DiffuseNearDistance, diffuseFarDistance: roundTrip.DiffuseFarDistance));
            foreach (var field in typeof(ExplorationLightSettings).GetFields())
                Assert.That(FlashlightDebugSave.SceneFields, Does.Contain(field.Name));
            Assert.That(FlashlightDebugSave.SceneFields, Does.Not.Contain("BounceMode"));
        }

        [Test]
        public void LightDraftUndoesAndRedoesNewModeAndIntegerProbeCount()
        {
            var draft = ScriptableObject.CreateInstance<FlashlightDebugDraft>(); var history = new FlashlightDebugUndo(new FlashlightDebugHistory());
            try
            {
                history.Change(draft, "diffuse", "连续光测试", () => { draft.Values.BounceMode = LightBounceMode.Continuous; draft.Values.DiffuseVisibilityProbes = 4;
                    draft.Values.Mask = LightOverrideMask.BounceMode | LightOverrideMask.DiffuseVisibilityProbes; });
                history.UndoTemporary(); Assert.That(draft.Values.BounceMode, Is.EqualTo(LightBounceMode.LegacyPoint));
                Assert.That(draft.Values.Mask, Is.EqualTo(LightOverrideMask.None));
                history.RedoTemporary(); Assert.That(draft.Values.DiffuseVisibilityProbes, Is.EqualTo(4));
                Assert.That(draft.Values.Has(LightOverrideMask.DiffuseVisibilityProbes), Is.True);
            }
            finally { history.ClearTemporary(); UnityEngine.Object.DestroyImmediate(draft); }
        }
    }
}
