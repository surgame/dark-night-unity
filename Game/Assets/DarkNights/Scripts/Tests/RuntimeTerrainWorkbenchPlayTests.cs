using System.Collections;
using System.IO;
using DarkNights.Core.Config.Terrain;
using DarkNights.Editor.Terrain;
using DarkNights.Entry.Terrain;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace DarkNights.Tests
{
    /// <summary>真实工作台 Play 验证；调参只更换表现，保留权威格子，捕获各页实际 GUI 后退出且不保存场景。</summary>
    public sealed class RuntimeTerrainWorkbenchPlayTests
    {
        private static EditorWindow Game => EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
        private static string Output => Path.GetFullPath("../artifacts/runtime-terrain-tuner-20260926");
        [UnityTest]
        public IEnumerator TabsRenderAndStyleRefreshPreservesEditedMap()
        {
            EditorSceneManager.OpenScene(TerrainScenePaths.ReferenceChamber);
            Game.Focus();
            yield return new EnterPlayMode();
            BeginRendering(); var game = Game;
            var boot = Object.FindFirstObjectByType<TerrainDebugBootstrap>();
            var panel = Object.FindFirstObjectByType<TerrainDebugPanel>();
            Assert.That(boot, Is.Not.Null); Assert.That(panel, Is.Not.Null);
            float deadline = Time.realtimeSinceStartup + 45;
            while (boot.Preview == null || boot.Generating)
            {
                Assert.That(boot.LastError, Is.Null); Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), boot.Status);
                game.Repaint(); EditorApplication.QueuePlayerLoopUpdate();
                yield return null;
            }
            var session = boot.Workshop;
            panel.MapTool = 1;
            var cell = Editable(boot.Blueprint);
            byte material = boot.Blueprint.MaterialAt(cell.x, cell.y) == 2 ? (byte)3 : (byte)2;
            Assert.That(session.Edits.Paint(cell.x, cell.y, true, material), Is.True);
            session.Edits.EndStroke(); boot.Preview.NotifyReplicaChanged();
            Assert.That(session.Edits.ChangedCells, Is.GreaterThan(0));
            var before = session.Edits.CaptureCells(); int generation = boot.Generation;
            boot.StyleDraft.Style.OutlineAmplitude += .25f; panel.StyleChanged();
            yield return new WaitForSecondsRealtime(.6f);
            deadline = Time.realtimeSinceStartup + 45;
            while (boot.Generating)
            { Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), boot.Status); game.Repaint(); yield return null; }
            Assert.That(boot.LastError, Is.Null); Assert.That(boot.Workshop, Is.SameAs(session));
            Assert.That(boot.Generation, Is.EqualTo(generation)); CollectionAssert.AreEqual(before, session.Edits.CaptureCells());
            string output = Output; Directory.CreateDirectory(output);
            for (int tab = 0; tab < 5; tab++)
            {
                panel.ActiveTab = tab;
                yield return null; yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "tab-" + tab + ".png"));
                yield return null;
            }
            panel.ActiveTab = 3; panel.UiScale = 1.75f; panel.PanelWidth = 280;
            yield return null; yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Path.Combine(output, "large-ui-narrow-panel.png"));
            yield return null;
            using (new TerrainGameViewTestSize(game, 640, 360))
            {
                panel.ActiveTab = 1;
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(Screen.width, Is.EqualTo(640)); Assert.That(Screen.height, Is.EqualTo(360));
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "640x360-rock.png")); yield return null;
                panel.ActiveTab = 3;
                boot.Flyer.FitWorkbenchMap(panel.Layout.Panel.xMax * panel.Layout.Scale);
                float distance = boot.Flyer.CameraDistance;
                yield return null; yield return null;
                Assert.That(boot.Flyer.CameraDistance, Is.EqualTo(distance), "显示页不应截断全图镜头");
                yield return new WaitForEndOfFrame();
                ScreenCapture.CaptureScreenshot(Path.Combine(output, "640x360-fit-map.png")); yield return null;
            }
        }
        [UnityTest]
        public IEnumerator RandomCaveRendersAt720p()
        {
            EditorSceneManager.OpenScene(TerrainScenePaths.RandomCave); Game.Focus();
            yield return new EnterPlayMode(); BeginRendering();
            var boot = Object.FindFirstObjectByType<TerrainDebugBootstrap>();
            var panel = Object.FindFirstObjectByType<TerrainDebugPanel>();
            float deadline = Time.realtimeSinceStartup + 45;
            while (boot.Preview == null || boot.Generating)
            {
                Assert.That(boot.LastError, Is.Null); Assert.That(Time.realtimeSinceStartup, Is.LessThan(deadline), boot.Status);
                Game.Repaint(); EditorApplication.QueuePlayerLoopUpdate(); yield return null;
            }
            Assert.That(boot.FixedMap, Is.Null); Assert.That(boot.Blueprint.Rooms.Count, Is.GreaterThan(1));
            using (new TerrainGameViewTestSize(Game, 1280, 720))
            {
                panel.ActiveTab = 0; panel.UiScale = 1; panel.PanelWidth = 380;
                yield return new WaitForSecondsRealtime(.3f);
                Assert.That(Screen.width, Is.EqualTo(1280)); Assert.That(Screen.height, Is.EqualTo(720));
                yield return new WaitForEndOfFrame(); Directory.CreateDirectory(Output);
                ScreenCapture.CaptureScreenshot(Path.Combine(Output, "1280x720-random.png")); yield return null;
            }
        }
        private static void BeginRendering()
        {
            SessionState.SetBool("DarkNights.TunerTest.Background", Application.runInBackground);
            SessionState.SetBool("DarkNights.TunerTest.Maximized", Game.maximized);
            Application.runInBackground = true; Game.maximized = true; Game.Focus(); Game.Repaint();
        }
        private static Vector2Int Editable(TerrainBlueprint blueprint)
        {
            for (int y = 10; y < 180; y++) for (int x = 10; x < 300; x++)
                if (!blueprint.IsProtected(x, y) && blueprint.MaterialAt(x, y) != 8) return new Vector2Int(x, y);
            throw new System.InvalidOperationException("No editable fixture cell.");
        }
        [UnityTearDown]
        public IEnumerator ExitAfterFailure()
        {
            if (Application.isPlaying) Application.runInBackground = SessionState.GetBool("DarkNights.TunerTest.Background", false);
            var game = EditorWindow.GetWindow(typeof(EditorWindow).Assembly.GetType("UnityEditor.GameView"));
            game.maximized = SessionState.GetBool("DarkNights.TunerTest.Maximized", false);
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
    }
}
