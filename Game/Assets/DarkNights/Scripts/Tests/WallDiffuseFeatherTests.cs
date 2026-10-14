using System.Collections;
using System.Reflection;
using DarkNights.Core.Config;
using DarkNights.View.Lighting;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>
    /// 完整返光羽化的定向 GPU 合同；以独立的阶跃、封闭隔墙和恒定彩色场验证实际滤波 Kernel。
    /// 只在显式运行测试时创建隔离资源，覆盖单灯宽度、域拒绝及直射数据保留，不读取玩家存档。
    /// </summary>
    public sealed class WallDiffuseFeatherTests
    {
        private const int Size = 32, Grid = 8;
        private ComputeShader shader;
        private WallDiffuseFeatherResources resources;
        private Texture2D cells, seed, read;
        private Texture2DArray layers;
        private RenderTexture output, direction;
        private ComputeBuffer bounce, apertures;
        private readonly LightBounceRules[] rules = new LightBounceRules[16];
        private readonly Vector4[] positions = new Vector4[16], colors = new Vector4[16];

        [SetUp]
        public void Open()
        {
            Assume.That(SystemInfo.supportsComputeShaders && SystemInfo.supports2DArrayTextures);
            shader = Object.Instantiate(AssetDatabase.LoadAssetAtPath<ComputeShader>(
                "Assets/DarkNights/Res/Shared/Lighting/ExplorationLighting.compute"));
            resources = new WallDiffuseFeatherResources(shader, Size, Size);
            cells = new Texture2D(Grid, Grid, TextureFormat.RGBA32, false, true);
            seed = new Texture2D(Size, Size, TextureFormat.RGBAHalf, false, true);
            read = new Texture2D(Size, Size, TextureFormat.RGBAFloat, false, true);
            output = Target();
            direction = Target();
            bounce = new ComputeBuffer(16, 16);
            apertures = new ComputeBuffer(160, 4);
            shader.SetVector("_DNTargetSize", new Vector4(Size, Size, 0, 0));
            shader.SetVector("_DNLightRect", new Vector4(0, 0, Grid, Grid));
            shader.SetVector("_DNKnownBounds", new Vector4(0, 0, Grid, Grid));
            Air();
        }

        private bool Prepare(params float[] widths)
        {
            for (int i = 0; i < widths.Length; i++)
            {
                rules[i] = new LightBounceRules(LightBounceMode.Continuous,
                    diffuseOcclusionMode: WallDiffuseOcclusionMode.OutwardFeather, diffuseFeatherWidth: widths[i]);
                positions[i] = new Vector4(2, 2, 24, .9f);
                colors[i] = Vector4.one;
            }
            return resources.Prepare(rules, positions, colors, widths.Length);
        }

        [Test]
        public void WholeAirCutSoftensBothSidesAndPreservesDirectData()
        {
            Prepare(1.5f);
            Fill((group, x, y) => x >= Size / 2 ? new Color(.6f, .3f, .1f, 1) : new Color(0, 0, 0, 1));
            Filter();
            Assert.That(At(output, 3.75f, 4).r, Is.GreaterThan(.201f));
            Assert.That(At(output, 4.25f, 4).r, Is.LessThan(.799f));
            var value = At(direction, 3.75f, 4);
            Assert.That(value.r, Is.EqualTo(.25f).Within(.001f));
            Assert.That(value.g, Is.EqualTo(-.5f).Within(.001f));
            Assert.That(value.b, Is.EqualTo(.3f).Within(.001f));
        }

        [TestCase(false)]
        [TestCase(true)]
        public void AirEndpointsDoNotPermitCrossingSolidOrUnknownBarrier(bool unknown)
        {
            Prepare(4);
            for (int y = 0; y < Grid; y++) cells.SetPixel(4, y, unknown ? Color.clear : new Color(1, 0, 0, 1));
            cells.Apply();
            Fill((group, x, y) => x < Size / 2 ? new Color(.6f, .3f, .1f, 1) : new Color(0, 0, 0, 1));
            Filter();
            Assert.That(At(output, 5.25f, 4).r, Is.EqualTo(.2f).Within(.002f));
        }

        [Test]
        public void EqualWidthsShareLayersAndDifferentWidthsStayIndependent()
        {
            Prepare(1.5f, 3, 1.5f);
            Assert.That(resources.GroupCount, Is.EqualTo(2));
            var values = (Vector4[])typeof(WallDiffuseFeatherResources).GetField("assignments",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(resources);
            Assert.That(values[0].x, Is.EqualTo(values[2].x));
            Assert.That(values[0].x, Is.Not.EqualTo(values[1].x));
        }

        [Test]
        public void AirPartOfSlopeCellReceivesFeatherWhileRockPartKeepsBase()
        {
            Prepare(1.5f);
            cells.SetPixel(4, 4, new Color(1, 1f / 255, 0, 1));
            cells.Apply();
            Fill((group, x, y) => x < Size / 2 ? new Color(.6f, .3f, .1f, 1) : new Color(0, 0, 0, 1));
            Filter();
            Assert.That(At(output, 4.125f, 4.125f).r, Is.GreaterThan(.201f));
            Assert.That(At(output, 4.875f, 4.875f).r, Is.EqualTo(.2f).Within(.002f));
        }

        [Test]
        public void ZeroWidthBypassesAllocationAndFilterScheduling()
        {
            Assert.That(Prepare(0), Is.False);
            Assert.That(resources.GroupCount, Is.Zero);
            Assert.That(resources.BufferBytes, Is.Zero);
        }

        [Test]
        public void ColoredLayersRecombineAndKeepConstantFieldAtViewportEdge()
        {
            Prepare(1, 3);
            Fill((group, x, y) => group == 0 ? new Color(.6f, 0, 0, 1) : new Color(0, 0, .3f, 1));
            Filter();
            var energy = At(output, .125f, .125f);
            Assert.That(energy.r, Is.EqualTo(.8f).Within(.002f));
            Assert.That(energy.g, Is.EqualTo(.1f).Within(.002f));
            Assert.That(energy.b, Is.EqualTo(.35f).Within(.002f));
            var data = At(direction, .125f, .125f);
            Assert.That(data.a, Is.EqualTo(energy.r * .2126f + energy.g * .7152f + energy.b * .0722f).Within(.002f));
            Assert.That(data.b, Is.EqualTo(.3f).Within(.001f));
        }

        private void Air()
        {
            var values = new Color[Grid * Grid];
            for (int i = 0; i < values.Length; i++) values[i] = new Color(0, 0, 0, 1);
            cells.SetPixels(values);
            cells.Apply();
        }

        private void Fill(System.Func<int, int, int, Color> pattern)
        {
            layers = new Texture2DArray(Size, Size, resources.GroupCount, TextureFormat.RGBAHalf, false, true);
            for (int group = 0; group < resources.GroupCount; group++)
            {
                var values = new Color[Size * Size];
                for (int y = 0; y < Size; y++) for (int x = 0; x < Size; x++) values[y * Size + x] = pattern(group, x, y);
                layers.SetPixels(values, group);
            }
            layers.Apply();
            var raw = (RenderTexture)typeof(WallDiffuseFeatherResources).GetField("raw",
                BindingFlags.Instance | BindingFlags.NonPublic).GetValue(resources);
            for (int group = 0; group < resources.GroupCount; group++) Graphics.CopyTexture(layers, group, 0, raw, group, 0);
            var pixels = new Color[Size * Size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(.2f, .1f, .05f, 0);
            seed.SetPixels(pixels); seed.Apply(); Graphics.CopyTexture(seed, output);
            for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(.25f, -.5f, .3f, .3f);
            seed.SetPixels(pixels); seed.Apply(); Graphics.CopyTexture(seed, direction);
        }

        private void Filter()
        {
            resources.Bind(cells, output, direction, bounce, apertures);
            using (var commands = new CommandBuffer())
            {
                resources.Record(commands);
                Graphics.ExecuteCommandBuffer(commands);
            }
        }

        private Color At(RenderTexture target, float x, float y)
        {
            var previous = RenderTexture.active;
            try
            {
                RenderTexture.active = target;
                read.ReadPixels(new Rect(0, 0, Size, Size), 0, 0); read.Apply();
                return read.GetPixel(Mathf.FloorToInt(x / Grid * Size), Mathf.FloorToInt(y / Grid * Size));
            }
            finally { RenderTexture.active = previous; }
        }

        private static RenderTexture Target()
        {
            var value = new RenderTexture(Size, Size, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            { enableRandomWrite = true };
            value.Create();
            return value;
        }

        [UnityTearDown]
        public IEnumerator Close()
        {
            var task = resources.RetireAsync();
            while (!task.IsCompleted) yield return null;
            task.GetAwaiter().GetResult();
            bounce.Dispose(); apertures.Dispose(); output.Release(); direction.Release();
            foreach (var value in new Object[] { shader, cells, seed, read, layers, output, direction })
                if (value != null) Object.DestroyImmediate(value);
        }
    }
}
