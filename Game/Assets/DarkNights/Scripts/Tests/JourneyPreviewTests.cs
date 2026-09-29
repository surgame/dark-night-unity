using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using DarkNights.Core.Config.Expedition;
using DarkNights.Editor;
using GameCore.Objects.Runner;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UIElements;

namespace DarkNights.Tests
{
    /// <summary>
    /// 编辑态真实后台预览验收；通过反射调用内部入口，等待实际 Task 和 Editor 更新后检查主线程纹理发布。
    /// 使用临时窗口／纹理，不安装资产、不启动会话、不修改当前场景；每个退出路径均释放预览。
    /// </summary>
    public sealed class JourneyPreviewTests
    {
        private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        private static ExpeditionPlanetPreview Preview() =>
            (ExpeditionPlanetPreview)Activator.CreateInstance(typeof(ExpeditionPlanetPreview), true);
        private static PlanetDefinition Planet(string seed) => new PlanetDefinition("preview", "预览", seed: seed);
        private static object Get(object target, string name) => target.GetType().GetField(name, Private).GetValue(target);
        private static Image Surface(ExpeditionPlanetPreview target) =>
            (Image)target.GetType().GetProperty("Surface", Private).GetValue(target);
        private static object Call(object target, string name, params object[] args)
        {
            try { return target.GetType().GetMethod(name, Private).Invoke(target, args); }
            catch (TargetInvocationException error) { throw error.InnerException; }
        }
        private static void Generate(ExpeditionPlanetPreview target, string seed, Action<string> completed) =>
            Call(target, "Generate", Planet(seed), completed, null, null);

        [UnityTest]
        public IEnumerator ActualTaskPublishesOnMainThreadAndFixedSeedRepeats()
        {
            var preview = Preview();
            string sceneBefore = SceneState(); int objectsBefore = Resources.FindObjectsOfTypeAll<ObjectInstance>().Length;
            int mainThread = Thread.CurrentThread.ManagedThreadId, callbackThread = -1, count = 0;
            string message = "";
            try
            {
                Generate(preview, "EDITOR-PREVIEW-SAME", value =>
                { count++; message = value; callbackThread = Thread.CurrentThread.ManagedThreadId; });
                Assert.That(Get(preview, "pending"), Is.InstanceOf<Task>());
                Assert.That(Surface(preview).image, Is.Null, "后台结果尚未回到 Editor 更新时不能提前建立纹理");
                yield return Until(() => count == 1);
                Assert.That(callbackThread, Is.EqualTo(mainThread));
                Assert.That(message, Does.Contain("EDITOR-PREVIEW-SAME"));
                var first = (Texture2D)Surface(preview).image;
                Assert.That(first, Is.Not.Null); Assert.That(first.width, Is.EqualTo(320)); Assert.That(first.height, Is.EqualTo(192));
                Color32[] pixels = first.GetPixels32();
                Generate(preview, "EDITOR-PREVIEW-SAME", _ => count++);
                Assert.That(first == null, Is.True, "重新生成必须释放上一次纹理");
                yield return Until(() => count == 2);
                var second = (Texture2D)Surface(preview).image;
                Assert.That(second.GetPixels32().SequenceEqual(pixels), Is.True);
                preview.Dispose();
                Assert.That(second == null, Is.True); Assert.That(Surface(preview).image, Is.Null);
                Assert.That(SceneState(), Is.EqualTo(sceneBefore));
                Assert.That(Resources.FindObjectsOfTypeAll<ObjectInstance>().Length, Is.EqualTo(objectsBefore));
                Assert.That(EditorApplication.isPlaying, Is.False);
            }
            finally { preview.Dispose(); }
        }

        [UnityTest]
        public IEnumerator ClearCancelsRealPendingCandidateWithoutPublishing()
        {
            var preview = Preview(); int callbacks = 0;
            try
            {
                Generate(preview, "EDITOR-CANCEL", _ => callbacks++);
                var task = (Task)Get(preview, "pending");
                Call(preview, "Clear");
                yield return Until(() => task.IsCompleted);
                yield return null;
                Call(preview, "Tick");
                Assert.That(callbacks, Is.Zero); Assert.That(Surface(preview).image, Is.Null);
                Assert.That(Get(preview, "pending"), Is.Null); Assert.That(Get(preview, "cancellation"), Is.Null);
            }
            finally { preview.Dispose(); }
        }

        [UnityTest]
        public IEnumerator DeadlineCancelsPendingTaskAndReportsOnce()
        {
            var preview = Preview(); var messages = new List<string>();
            try
            {
                Generate(preview, "EDITOR-TIMEOUT", messages.Add);
                var task = (Task)Get(preview, "pending");
                typeof(ExpeditionPlanetPreview).GetField("deadline", Private).SetValue(preview, EditorApplication.timeSinceStartup - 1);
                Call(preview, "Tick");
                yield return Until(() => task.IsCompleted);
                yield return null;
                Call(preview, "Tick");
                Assert.That(messages.Count, Is.EqualTo(1)); Assert.That(messages[0], Does.Contain("15 秒"));
                Assert.That(Surface(preview).image, Is.Null); Assert.That(Get(preview, "pending"), Is.Null);
            }
            finally { preview.Dispose(); }
        }

        [UnityTest]
        public IEnumerator ReplacementPublishesOnlyTheLatestSeed()
        {
            var preview = Preview(); int first = 0, second = 0; string message = "";
            try
            {
                Generate(preview, "EDITOR-ROW-A", _ => first++);
                var oldTask = (Task)Get(preview, "pending");
                Generate(preview, "EDITOR-ROW-B", value => { second++; message = value; });
                yield return Until(() => oldTask.IsCompleted && second == 1);
                yield return null;
                Assert.That(first, Is.Zero); Assert.That(second, Is.EqualTo(1));
                Assert.That(message, Does.Contain("EDITOR-ROW-B")); Assert.That(Surface(preview).image, Is.Not.Null);
            }
            finally { preview.Dispose(); }
        }

        [UnityTest]
        public IEnumerator DisposePendingTaskNeverPublishesLater()
        {
            var preview = Preview(); int callbacks = 0;
            try
            {
                Generate(preview, "EDITOR-DISPOSE", _ => callbacks++);
                var task = (Task)Get(preview, "pending");
                preview.Dispose();
                yield return Until(() => task.IsCompleted);
                yield return null;
                Call(preview, "Tick");
                Assert.That(callbacks, Is.Zero); Assert.That(Surface(preview).image, Is.Null);
                Assert.That(Get(preview, "report"), Is.Null); Assert.That(Get(preview, "pending"), Is.Null);
            }
            finally { preview.Dispose(); }
        }

        [UnityTest]
        public IEnumerator WindowRowChangeAndCloseCancelItsActualPreview()
        {
            var window = ScriptableObject.CreateInstance<ExpeditionFlowWindow>();
            string sceneBefore = SceneState();
            try
            {
                window.CreateGUI();
                var preview = (ExpeditionPlanetPreview)Get(window, "preview");
                Call(window, "GeneratePreview");
                var first = (Task)Get(preview, "pending");
                Call(window, "AddPlanet");
                yield return Until(() => first.IsCompleted);
                Assert.That(Surface(preview).image, Is.Null);
                Call(window, "GeneratePreview");
                var second = (Task)Get(preview, "pending");
                typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false);
                Undo.ClearUndo((UnityEngine.Object)Get(window, "draft"));
                UnityEngine.Object.DestroyImmediate(window); window = null;
                yield return Until(() => second.IsCompleted);
                Assert.That(Surface(preview).image, Is.Null); Assert.That(Get(preview, "pending"), Is.Null);
                Assert.That(SceneState(), Is.EqualTo(sceneBefore));
            }
            finally
            {
                if (window != null)
                {
                    typeof(EditorWindow).GetProperty("hasUnsavedChanges").SetValue(window, false);
                    var draft = (UnityEngine.Object)Get(window, "draft"); if (draft != null) Undo.ClearUndo(draft);
                    UnityEngine.Object.DestroyImmediate(window);
                }
            }
        }

        private static IEnumerator Until(Func<bool> condition)
        {
            double deadline = EditorApplication.timeSinceStartup + 12;
            while (!condition())
            {
                if (EditorApplication.timeSinceStartup >= deadline) Assert.Fail("预览后台任务未在 12 秒内完成。");
                yield return null;
            }
        }

        private static string SceneState()
        {
            var rows = new List<string> { SceneManager.GetActiveScene().handle.ToString() };
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                rows.Add(scene.handle + ":" + scene.path + ":" + scene.isDirty + ":" +
                    string.Join(",", scene.GetRootGameObjects().Select(root => root.GetInstanceID()).OrderBy(id => id)));
            }
            return string.Join("|", rows);
        }
    }
}
