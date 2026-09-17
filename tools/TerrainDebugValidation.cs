using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Entry.Terrain;
using DarkNights.View.Terrain;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>在已运行的独立调试场景中检查真实页面、重建生命周期和二维穿墙输入；只写本批验证报告。</summary>
public static class TerrainDebugValidation
{
    public static async Task<string> Main()
    {
        int checks = 0;
        Action<bool, string> check = (pass, name) => { if (!pass) throw new Exception(name); checks++; };
        var bootstrap = UnityEngine.Object.FindAnyObjectByType<TerrainDebugBootstrap>();
        check(Application.isPlaying && bootstrap != null, "独立调试场景已启动");
        await Wait(() => bootstrap.Generation > 0 && !bootstrap.Generating);
        var flyer = bootstrap.Flyer;
        check(bootstrap.Blueprint.Rooms.Count == 8 && bootstrap.Preview.Ready, "8 个房间与真实页面就绪");
        check(UnityEngine.Object.FindObjectsByType<DarkNights.View.LevelLayoutAuthoring>().Length == 0,
            "调试场景没有旧营地布局");
        check(bootstrap.Blueprint.CopyMaterials().SequenceEqual(TerrainGenerator.Generate(bootstrap.Settings).CopyMaterials()),
            "完整原始蓝图未被72列平地覆盖");
        check(Mathf.Abs(flyer.transform.position.x - bootstrap.Blueprint.Rooms[0].X - .5f) < .01f, "直接出生入口洞室");
        check(flyer.ViewCamera.orthographicSize == 12, "默认近距离镜头");
        var keyboard = InputSystem.AddDevice<Keyboard>();
        try
        {
            flyer.Teleport(new Vector2(1.5f, -120));
            check(bootstrap.Blueprint.MaterialAt(1, 120) != 0, "穿墙测试起点是实体地形");
            Vector3 before = flyer.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W, Key.D));
            await Task.Delay(180);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Task.Delay(40);
            Vector3 delta = flyer.transform.position - before;
            check(delta.x > .1f && delta.y > .1f && Mathf.Abs(delta.x - delta.y) < .05f, "真实W+D输入穿墙且斜向不加速");
            before = flyer.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.S, Key.A));
            await Task.Delay(120);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            await Task.Delay(40);
            delta = flyer.transform.position - before;
            check(delta.x < -.1f && delta.y < -.1f, "真实S+A输入反向飞行");
            var panel = UnityEngine.Object.FindAnyObjectByType<TerrainDebugPanel>();
            panel.enabled = false;
            flyer.InputBlocked = true;
            before = flyer.transform.position;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            await Task.Delay(80);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            check(flyer.transform.position == before, "文本输入时不飞行");
            flyer.InputBlocked = false;
            panel.enabled = true;
        }
        finally { InputSystem.RemoveDevice(keyboard); }
        flyer.CameraDistance = 7;
        await Task.Delay(60);
        check(Mathf.Abs(flyer.ViewCamera.orthographicSize - 7) < .01f &&
            Vector2.Distance(flyer.ViewCamera.transform.position, flyer.transform.position + new Vector3(0, .75f)) < .01f,
            "镜头可调并跟随二维位置");
        byte[] original = bootstrap.Blueprint.CopyMaterials();
        int generation = bootstrap.Generation;
        bootstrap.RequestRegenerate();
        await Wait(() => bootstrap.Generation > generation && !bootstrap.Generating);
        check(original.SequenceEqual(bootstrap.Blueprint.CopyMaterials()), "同种子重建确定性");
        generation = bootstrap.Generation;
        bootstrap.Settings.Seed = "DEBUG-VALIDATION-CHANGED";
        await Wait(() => bootstrap.Generation > generation && !bootstrap.Generating);
        check(!original.SequenceEqual(bootstrap.Blueprint.CopyMaterials()), "Inspector参数变化实时生成新地图");
        generation = bootstrap.Generation;
        bootstrap.Settings.Seed = "DEBUG-STALE"; bootstrap.RequestRegenerate();
        await Task.Yield();
        bootstrap.Settings.Seed = "DEBUG-LATEST"; bootstrap.RequestRegenerate();
        await Wait(() => bootstrap.Generation > generation && !bootstrap.Generating && bootstrap.Blueprint.Settings.Seed == "DEBUG-LATEST");
        await Task.Delay(120);
        check(UnityEngine.Object.FindObjectsByType<TerrainPreview>().Length == 1,
            "连续生成只保留最新预览");
        for (int i = 0; i < 8; i++)
        {
            bootstrap.VisitRoom(i);
            await Task.Delay(100);
            await Wait(() => bootstrap.Preview.Ready);
            check(bootstrap.Preview.LastError == null, "房间跳转页面完成 " + i);
        }
        bootstrap.Settings = new DarkNights.Core.Config.Terrain.TerrainGenerationSettings();
        generation = bootstrap.Generation; bootstrap.RequestRegenerate();
        await Wait(() => bootstrap.Generation > generation && !bootstrap.Generating);
        flyer.CameraDistance = 12;
        await Task.Delay(100);
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/terrain-debug/editor-validation.json"));
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        string report = "{\"passed\":true,\"checks\":" + checks + ",\"generation\":" + bootstrap.Generation + "}";
        File.WriteAllText(output, report);
        return report;
    }

    private static async Task Wait(Func<bool> ready)
    {
        DateTime deadline = DateTime.UtcNow.AddSeconds(35);
        while (!ready())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("调试场景验证等待超时。");
            await Task.Delay(20);
        }
    }
}
