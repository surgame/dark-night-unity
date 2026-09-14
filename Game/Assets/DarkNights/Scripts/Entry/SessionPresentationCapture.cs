using System;
using System.IO;
using Cysharp.Threading.Tasks;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.View;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DarkNights.Entry
{
    /// <summary>
    /// 由显式 Player 验收驱动调用的固定时刻捕获，复用已加载的暂停世界、原生视图与 UI。
    /// 可注入有界表现通知以检查效果资源，不创建业务实体、不推进模拟或修改资源；普通游戏入口不调用。
    /// </summary>
    internal static class SessionPresentationCapture
    {
        internal static async UniTask Save(SessionNetwork network, JObject command, string directory)
        {
            if (QualitySettings.activeColorSpace != ColorSpace.Linear)
                throw new InvalidOperationException("Linear color space is required.");
            var frame = network.Client.Replica.Current;
            if (frame != null && (!network.Client.Ready || !frame.Paused || frame.Elapsed != 0))
                throw new InvalidOperationException("Sample capture requires the frozen paused fixture.");
            double time = (double)command["time"];
            float night = (float)command["night"], zoom = (float)command["zoom"];
            if (float.IsNaN(zoom) || zoom < 1.8f || zoom > 4.5f) throw new ArgumentOutOfRangeException(nameof(zoom));
            var stage = UnityEngine.Object.FindAnyObjectByType<PinewatchStage>();
            var effects = network.GetComponent<SessionEffects>();
            if (command["cues"] is JArray cues)
            {
                if (cues.Count > 6) throw new InvalidOperationException("A fixture can emit at most six cues.");
                foreach (JToken row in cues)
                {
                    VisualCue cue = row.ToObject<VisualCue>();
                    if (cue.Kind != "corpse" && cue.Kind != "rubble" && cue.Kind != "resource" && cue.Kind != "damage" && cue.Kind != "command")
                        throw new InvalidOperationException("Unsupported presentation cue.");
                    if (cue.Kind == "command") effects.PresentLocalCommand(cue.X);
                    else
                    {
                        if (!network.Hosting) throw new InvalidOperationException("Only the fixture host can emit world effects.");
                        network.ObjectWorld.Feedback.Emit(cue);
                    }
                }
            }
            if (command["effects"] != null)
            {
                int expected = (int)command["effects"];
                double until = Time.realtimeSinceStartupAsDouble + 15;
                while (effects.EffectCount != expected && Time.realtimeSinceStartupAsDouble < until) await UniTask.Yield();
                if (effects.EffectCount != expected) throw new TimeoutException("Expected effects were not presented.");
            }
            var ui = network.GetComponent<SessionUiController>();
            ui.SamplePresentation();
            ui.Input.SelectEntity((int?)command["selected"] ?? 0);
            stage.ChangeZoom(zoom / stage.Zoom); stage.Focus((float)command["x"]);
            stage.SamplePresentation(time, night);
            network.GetComponent<SessionEntityViews>().SamplePresentation();
            effects.SamplePresentation(0);
            ui.SamplePresentation();
            Canvas.ForceUpdateCanvases();
            string path = Path.Combine(directory, Path.GetFileName((string)command["file"]));
            SessionRenderCapture.Save(stage, path);
            frame = network.Client.Replica.Current;
            File.WriteAllText(path + ".json", new JObject
            {
                ["colorSpace"] = QualitySettings.activeColorSpace.ToString(), ["batchmode"] = Application.isBatchMode,
                ["width"] = Screen.width, ["height"] = Screen.height,
                ["time"] = stage.PresentationTime, ["night"] = stage.NightAmount,
                ["cameraX"] = stage.CameraX, ["zoom"] = stage.Zoom,
                ["effects"] = effects.EffectCount, ["selected"] = JArray.FromObject(ui.Input.Selected),
                ["entities"] = network.GetComponent<SessionEntityViews>().Count,
                ["frame"] = frame == null ? null : JObject.FromObject(frame)
            }.ToString());
        }
    }
}
