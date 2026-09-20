using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using DarkNights.Editor.Terrain;
using DarkNights.Entry.Terrain;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

/// <summary>本次角色修复的显式资产绑定及实际 Play 输入验收；不生成美术，不覆盖场景或其它 Prefab 内容。</summary>
public static class CaveCharacterRepair
{
    public static string Bind()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("先退出 Play。");
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty)
            throw new InvalidOperationException("保留未保存场景，停止修复绑定。");
        string path = TerrainDebugSetup.Root + "/DebugFlyer.prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            var flyer = root.GetComponent<TerrainDebugFlyer>();
            flyer.IdleFrame = flyer.Art.sprite;
            flyer.WalkFrames = Enumerable.Range(0, 12).Select(i => AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/DarkNights/Res/Art/Original/sprites/spr_worker_walking/spr_worker_walking_" + i + ".png")).ToArray();
            if (flyer.WalkFrames.Any(s => s == null)) throw new InvalidOperationException("缺少已有行走帧。");
            flyer.PresentMovement(1, false, 0);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        var scene = EditorSceneManager.OpenScene(CaveExplorationSetup.ScenePath);
        var bootstrap = UnityEngine.Object.FindAnyObjectByType<TerrainDebugBootstrap>();
        bootstrap.Flyer.PresentMovement(1, false, 0);
        PrefabUtility.RecordPrefabInstancePropertyModifications(bootstrap.Flyer.Art.transform);
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets(); EditorSceneManager.OpenScene(CaveExplorationSetup.ScenePath);
        return "已绑定12帧，保存Prefab和洞穴场景，并重开场景。";
    }

    public static async Task<string> Validate()
    {
        string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../../artifacts/cave-character-repair"));
        Directory.CreateDirectory(output);
        int checks = 0;
        Action<bool, string> check = (pass, name) => { if (!pass) throw new Exception(name); checks++; };
        var b = UnityEngine.Object.FindAnyObjectByType<TerrainDebugBootstrap>();
        var input = UnityEngine.Object.FindAnyObjectByType<CaveWorkshopInput>();
        var flyer = b.Flyer;
        Application.runInBackground = true;
        float deadline = Time.realtimeSinceStartup + 30;
        while (b.Generating || b.Workshop == null)
        { if (Time.realtimeSinceStartup > deadline) throw new Exception("落地超时"); await Task.Yield(); }
        FindWalkingFloor(b);
        check(flyer.enabled && !flyer.FlightInputEnabled, "行走只保留一个输入入口且镜头更新仍启用");
        var keyboard = InputSystem.AddDevice<Keyboard>();
        var background = InputSystem.settings.backgroundBehavior;
        var editorInput = InputSystem.settings.editorInputBehaviorInPlayMode;
        InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
        InputSystem.settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
        InputSystem.EnableDevice(keyboard);
        var panel = UnityEngine.Object.FindAnyObjectByType<TerrainDebugPanel>();
        bool panelEnabled = panel.enabled; panel.enabled = false; flyer.InputBlocked = false;
        try
        {
            foreach (var key in new[] { Key.D, Key.A, Key.D, Key.A })
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(key));
                Sprite first = null; bool changed = false;
                float start = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - start < .45f)
                {
                    await Task.Delay(20);
                    if (first == null) first = flyer.Art.sprite;
                    changed |= first != flyer.Art.sprite;
                    check(Mathf.Abs(flyer.Art.bounds.center.x - flyer.transform.position.x) < .001f, "转身水平锚点");
                    check(Mathf.Abs(flyer.Art.bounds.min.y - flyer.transform.position.y) < .001f, "脚底锚点");
                    check(flyer.Art.enabled && flyer.Art.sprite != null, "角色持续可见");
                }
                check(changed, "真实按键行走动画推进: key=" + key + ", pressed=" + keyboard[key].isPressed +
                    ", current=" + (Keyboard.current == keyboard) + ", enabled=" + keyboard.enabled +
                    ", grounded=" + b.Workshop.Grounded + ", walking=" + input.Walking + ", sprite=" + flyer.Art.sprite.name);
                check(flyer.Art.flipX == (key == Key.A), "真实按键朝向");
                Capture(flyer.ViewCamera, Path.Combine(output, key + ".png"));
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Task.Delay(100);
            check(flyer.Art.sprite == flyer.IdleFrame, "松键恢复静止帧");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab)); await Task.Delay(100);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Task.Delay(50);
            check(!input.Walking && flyer.FlightInputEnabled, "Tab切观察");
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Tab)); await Task.Delay(100);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); await Task.Delay(50);
            check(input.Walking && !flyer.FlightInputEnabled && flyer.enabled, "Tab回行走");
            float before = b.Workshop.Y;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.Space)); await Task.Delay(180);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            check(b.Workshop.Y > before, "跳跃保留");
            string report = "{\"passed\":true,\"checks\":" + checks + "}";
            File.WriteAllText(Path.Combine(output, "play-validation.json"), report);
            return report;
        }
        finally
        {
            InputSystem.RemoveDevice(keyboard); InputSystem.settings.backgroundBehavior = background;
            InputSystem.settings.editorInputBehaviorInPlayMode = editorInput;
            panel.enabled = panelEnabled;
        }
    }

    private static void FindWalkingFloor(TerrainDebugBootstrap b)
    {
        foreach (var room in b.Blueprint.Rooms)
        {
            b.Workshop.Teleport(room.X, -room.Y);
            for (int i = 0; i < 600; i++) b.Workshop.Tick(0, false, false, 1f / 60);
            if (!b.Workshop.Grounded) continue;
            var start = new Vector2(b.Workshop.X, b.Workshop.Y);
            bool supported = true;
            for (int i = 0; i < 180; i++)
            {
                b.Workshop.Tick(i < 60 || i >= 120 ? 1 : -1, false, false, 1f / 60);
                supported &= b.Workshop.Grounded;
            }
            if (!supported || Mathf.Abs(b.Workshop.X - start.x) < .5f) continue;
            b.Workshop.Teleport(start.x, start.y);
            b.Workshop.Tick(0, false, false, 1f / 60);
            b.Flyer.Teleport(start);
            return;
        }
        throw new Exception("未找到可连续步行的洞底");
    }

    private static void Capture(Camera camera, string path)
    {
        var target = RenderTexture.GetTemporary(1280, 720, 24);
        var previous = camera.targetTexture; var active = RenderTexture.active;
        var image = new Texture2D(1280, 720, TextureFormat.RGB24, false);
        try
        {
            camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
            image.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); image.Apply();
            File.WriteAllBytes(path, image.EncodeToPNG());
        }
        finally
        {
            camera.targetTexture = previous; RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(target); UnityEngine.Object.Destroy(image);
        }
    }
}
