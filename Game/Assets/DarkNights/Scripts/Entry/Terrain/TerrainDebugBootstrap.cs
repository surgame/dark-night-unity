using System;
using System.Threading;
using System.Threading.Tasks;
using AnyRules.Next.Authoring;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;
using DarkNights.View.Terrain;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DarkNights.Entry.Terrain
{
    /// <summary>
    /// 独立离线地图调试入口，直接使用参考生成器的房间蓝图，不加载正式平地、营地会话或波次。
    /// 参数防抖后在后台生成，主线程完成真实 DualGrid 可见页才替换旧预览；退出取消并释放临时表现。
    /// </summary>
    public sealed class TerrainDebugBootstrap : MonoBehaviour
    {
        public ARDMapDefinition Definition;
        public TerrainDebugFlyer Flyer;
        public TerrainGenerationSettings Settings = new TerrainGenerationSettings();
        public bool LiveRegenerate = true;
        public TerrainBlueprint Blueprint { get; private set; }
        public TerrainPreview Preview { get; private set; }
        public bool Generating { get; private set; }
        public int Generation { get; private set; }
        public string Status { get; private set; } = "准备生成房间地图…";
        public string LastError { get; private set; }
        private CancellationTokenSource lifetime;
        private string observedSettings;
        private bool pending;
        private float due;
        private int request;

        private void OnEnable()
        {
            lifetime = new CancellationTokenSource();
            observedSettings = JsonUtility.ToJson(Settings);
            RequestRegenerate();
        }

        public void RequestRegenerate()
        {
            pending = true; due = Time.unscaledTime; request++;
        }

        public void NewSeed()
        {
            Settings.Seed = "DEBUG-" + Guid.NewGuid().ToString("N").Substring(0, 12);
            RequestRegenerate();
        }

        public void VisitRoom(int index)
        {
            if (Blueprint == null || index < 0 || index >= Blueprint.Rooms.Count) return;
            var room = Blueprint.Rooms[index];
            Flyer.Teleport(new Vector2(room.X + .5f, -room.Y + .5f));
        }

        private void Update()
        {
            string current = JsonUtility.ToJson(Settings);
            if (current != observedSettings)
            {
                observedSettings = current;
                if (LiveRegenerate) { RequestRegenerate(); due = Time.unscaledTime + .35f; }
            }
            var keyboard = Keyboard.current;
            if (Flyer != null && !Flyer.InputBlocked && keyboard != null)
            {
                if (keyboard.rKey.wasPressedThisFrame) NewSeed();
                if (keyboard.fKey.wasPressedThisFrame) VisitRoom(0);
            }
            if (pending && !Generating && Time.unscaledTime >= due) GenerateAsync();
        }

        private async void GenerateAsync()
        {
            pending = false; Generating = true; LastError = null;
            int version = request;
            CancellationToken token = lifetime.Token;
            TerrainPreview candidate = null;
            try
            {
                if (Definition == null || Flyer == null || Flyer.ViewCamera == null)
                    throw new InvalidOperationException("Debug Bootstrap 缺少明确的地形、角色或镜头引用。");
                var settings = Settings.CopyValidated();
                Status = "生成中：" + settings.Seed;
                var blueprint = await Task.Run(() => TerrainGenerator.Generate(settings), token);
                token.ThrowIfCancellationRequested();
                if (version != request) return;
                var root = new GameObject("Generated room terrain");
                root.transform.SetParent(transform, false);
                candidate = root.AddComponent<TerrainPreview>();
                candidate.ViewCamera = Flyer.ViewCamera;
                candidate.ShowBlueprint(Definition, blueprint);
                float deadline = Time.realtimeSinceStartup + 30;
                while (!candidate.Ready)
                {
                    token.ThrowIfCancellationRequested();
                    if (version != request) return;
                    if (candidate.LastError != null) throw candidate.LastError;
                    if (Time.realtimeSinceStartup > deadline) throw new TimeoutException("地图可见页未在 30 秒内就绪。");
                    await Task.Yield();
                }
                token.ThrowIfCancellationRequested();
                if (version != request) return;
                Release(Preview);
                Blueprint = blueprint; Preview = candidate; candidate = null;
                Generation++; Flyer.Ready = true; VisitRoom(0);
                Status = settings.Seed + " · 8 房间 / 7 通道 · 第 " + Generation + " 次生成";
            }
            catch (OperationCanceledException) { }
            catch (Exception error)
            {
                LastError = error.Message; Status = "生成失败：" + error.Message;
                Debug.LogException(error, this);
            }
            finally { Release(candidate); Generating = false; }
        }

        private void Release(TerrainPreview preview)
        {
            if (preview == null) return;
            preview.gameObject.SetActive(false);
            Destroy(preview.gameObject);
        }

        private void OnDisable()
        {
            lifetime?.Cancel(); lifetime?.Dispose(); lifetime = null;
            pending = false; Release(Preview); Preview = null; Blueprint = null;
            if (Flyer != null) Flyer.Ready = false;
        }
    }
}
