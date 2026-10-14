using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DarkNights.Core.Config;
using UnityEngine;
using UnityEngine.Rendering;

namespace DarkNights.View.Lighting
{
    /// <summary>
    /// 单相机完整返光羽化的分层资源；同宽度灯共享一层，异宽度保持独立，最多十六层。
    /// 只拥有表现纹理与上传参数，按需扩容并复用；替换及退休等待既有 GPU 使用完成。
    /// </summary>
    public sealed class WallDiffuseFeatherResources
    {
        private const int MaximumGroups = 16;
        private const float TransitionToSigma = 1f / 2.563103f;
        private readonly ComputeShader shader;
        private readonly int width, height, illuminate, horizontal, compose;
        private readonly Vector4[] assignments = new Vector4[MaximumGroups];
        private readonly Vector4[] widths = new Vector4[MaximumGroups];
        private readonly List<Task> retiring = new List<Task>();
        private RenderTexture raw, temporary;
        private Texture boundCells;
        private RenderTexture boundOutput, boundDirection;
        private bool bindingsReady;
        private int capacity;
        private bool uploaded;
        private Task retirement;
        public int GroupCount { get; private set; }
        public int IlluminateKernel => illuminate;
        public long BufferBytes => (long)width * height * capacity * 16;

        public WallDiffuseFeatherResources(ComputeShader shader, int width, int height)
        {
            this.shader = shader;
            this.width = width;
            this.height = height;
            illuminate = shader.HasKernel("IlluminateDiffuseFeather") ? shader.FindKernel("IlluminateDiffuseFeather") : -1;
            horizontal = shader.HasKernel("FeatherHorizontal") ? shader.FindKernel("FeatherHorizontal") : -1;
            compose = shader.HasKernel("FeatherCompose") ? shader.FindKernel("FeatherCompose") : -1;
        }

        public bool Prepare(LightBounceRules[] rules, Vector4[] positions, Vector4[] colors, int count)
        {
            if (retirement != null) throw new ObjectDisposedException(nameof(WallDiffuseFeatherResources));
            int groups = 0;
            bool changed = !uploaded;
            for (int lamp = 0; lamp < MaximumGroups; lamp++)
            {
                int group = -1;
                if (lamp < count && rules[lamp].Continuous && positions[lamp].w > -.9f && colors[lamp].w > 0 &&
                    rules[lamp].DiffuseOcclusionMode == WallDiffuseOcclusionMode.OutwardFeather && rules[lamp].DiffuseFeatherWidth > 0)
                {
                    float requested = rules[lamp].DiffuseFeatherWidth;
                    for (int existing = 0; existing < groups; existing++)
                        if (widths[existing].y == requested) { group = existing; break; }
                    if (group < 0)
                    {
                        group = groups++;
                        var value = new Vector4(requested * TransitionToSigma, requested, 0, 0);
                        changed |= !widths[group].Equals(value);
                        widths[group] = value;
                    }
                }
                var assignment = new Vector4(group, 0, 0, 0);
                changed |= !assignments[lamp].Equals(assignment);
                assignments[lamp] = assignment;
            }
            changed |= GroupCount != groups;
            GroupCount = groups;
            if (groups == 0) return false;
            if (!SystemInfo.supports2DArrayTextures || illuminate < 0 || horizontal < 0 || compose < 0)
                throw new InvalidOperationException("完整返光羽化需要纹理数组及三个正式计算 Kernel。");
            EnsureCapacity(groups);
            if (changed)
            {
                shader.SetInt("_DNFeatherCount", groups);
                shader.SetVectorArray("_DNFeatherGroups", assignments);
                shader.SetVectorArray("_DNFeatherWidths", widths);
                uploaded = true;
            }
            return true;
        }

        public void Bind(Texture cells, RenderTexture output, RenderTexture direction, ComputeBuffer bounce, ComputeBuffer apertures)
        {
            if (bindingsReady && cells == boundCells && output == boundOutput && direction == boundDirection) return;
            shader.SetTexture(illuminate, "_DNFeatherRaw", raw);
            shader.SetTexture(illuminate, "_DNLightCells", cells);
            shader.SetTexture(illuminate, "_DNLightOutput", output);
            shader.SetTexture(illuminate, "_DNDirectionOutput", direction);
            shader.SetBuffer(illuminate, "_DNBounceSources", bounce);
            shader.SetBuffer(illuminate, "_DNApertureVisibility", apertures);
            shader.SetTexture(horizontal, "_DNLightCells", cells);
            shader.SetTexture(horizontal, "_DNFeatherRaw", raw);
            shader.SetTexture(horizontal, "_DNFeatherTemporary", temporary);
            shader.SetTexture(compose, "_DNLightCells", cells);
            shader.SetTexture(compose, "_DNFeatherTemporary", temporary);
            shader.SetTexture(compose, "_DNLightOutput", output);
            shader.SetTexture(compose, "_DNDirectionOutput", direction);
            boundCells = cells;
            boundOutput = output;
            boundDirection = direction;
            bindingsReady = true;
        }

        public void Record(CommandBuffer commands)
        {
            commands.BeginSample("DN.Diffuse.FeatherHorizontal");
            commands.DispatchCompute(shader, horizontal, (width + 7) / 8, (height + 7) / 8, GroupCount);
            commands.EndSample("DN.Diffuse.FeatherHorizontal");
            commands.BeginSample("DN.Diffuse.FeatherCompose");
            commands.DispatchCompute(shader, compose, (width + 7) / 8, (height + 7) / 8, 1);
            commands.EndSample("DN.Diffuse.FeatherCompose");
        }

        private void EnsureCapacity(int groups)
        {
            if (groups <= capacity) return;
            int nextCapacity = Mathf.NextPowerOfTwo(groups);
            RenderTexture nextRaw = null, nextTemporary = null;
            try
            {
                nextRaw = Target("Raw diffuse feather layers", nextCapacity);
                nextTemporary = Target("Horizontal diffuse feather layers", nextCapacity);
            }
            catch
            {
                Release(nextRaw);
                Release(nextTemporary);
                throw;
            }
            if (raw != null) retiring.Add(ReleaseAfterGpuAsync(raw, temporary));
            raw = nextRaw;
            temporary = nextTemporary;
            capacity = nextCapacity;
            bindingsReady = false;
            retiring.RemoveAll(task => task.IsCompletedSuccessfully);
        }

        private RenderTexture Target(string name, int layers)
        {
            var target = new RenderTexture(width, height, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear)
            {
                name = name, dimension = TextureDimension.Tex2DArray, volumeDepth = layers,
                enableRandomWrite = true, filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp,
                useMipMap = false, autoGenerateMips = false
            };
            if (target.Create()) return target;
            Release(target);
            throw new InvalidOperationException("GPU 无法建立完整返光羽化纹理数组。");
        }

        public Task RetireAsync()
        {
            if (retirement != null) return retirement;
            if (raw != null) retiring.Add(ReleaseAfterGpuAsync(raw, temporary));
            raw = null;
            temporary = null;
            return retirement = Task.WhenAll(retiring);
        }

        private static async Task ReleaseAfterGpuAsync(RenderTexture first, RenderTexture second)
        {
            try
            {
                if (SystemInfo.supportsGraphicsFence)
                {
                    var fence = Graphics.CreateGraphicsFence(GraphicsFenceType.AsyncQueueSynchronisation,
                        SynchronisationStageFlags.AllGPUOperations);
                    while (!fence.passed) await Task.Yield();
                }
                else
                {
                    var request = AsyncGPUReadback.Request(first, 0, 0, 1, 0, 1, 0, 1);
                    while (!request.done) await Task.Yield();
                }
            }
            finally
            {
                Release(first);
                Release(second);
            }
        }

        private static void Release(RenderTexture target)
        {
            if (target == null) return;
            target.Release();
            if (Application.isPlaying) UnityEngine.Object.Destroy(target);
            else UnityEngine.Object.DestroyImmediate(target);
        }
    }
}
