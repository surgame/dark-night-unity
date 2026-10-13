using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace DarkNights.Editor.Lighting
{
    /// <summary>右侧真实预览的 UITK 控件与有限刷新；所有用户参数写预览草稿，旋转相位只属表现，错误暂停当前预览避免重复重建。</summary>
    public sealed class FlashlightPreviewPane : IDisposable
    {
        private readonly FlashlightDebugWindow window;
        private readonly FlashlightPreviewDraft draft;
        private readonly FlashlightDebugUndo undo;
        private readonly FlashlightPreviewStage stage = new FlashlightPreviewStage();
        private readonly Image image;
        private readonly Label status;
        private readonly PopupField<string> map, rooms;
        private readonly Toggle enabled, rotate, roomStart, compare;
        private readonly FloatField speed, zoom;
        private readonly Slider direction;
        private readonly Vector2Field position, cameraOffset;
        private readonly TextField recordName;
        private readonly IVisualElementScheduledItem tick;
        private bool faulted;
        public float CurrentAngle => stage.Image != null ? stage.Angle : draft.Settings.Angle;
        public FlashlightPreviewPane(FlashlightDebugWindow window, VisualElement root, FlashlightPreviewDraft draft, FlashlightDebugUndo undo)
        {
            this.window = window; this.draft = draft; this.undo = undo;
            image = root.Q<Image>("previewImage"); image.scaleMode = ScaleMode.ScaleToFit;
            status = root.Q<Label>("previewStatus");
            var toolbar = new VisualElement(); toolbar.AddToClassList("preview-toolbar"); root.Q("previewControls").Add(toolbar);
            map = new PopupField<string>(new List<string>(FlashlightPreviewSettings.MapNames), Array.IndexOf(FlashlightPreviewSettings.MapIds, draft.Settings.MapId));
            enabled = new Toggle("预览"); toolbar.Add(map); toolbar.Add(enabled);
            var pose = root.Q("previewPose"); pose.AddToClassList("preview-pose");
            var motion = new VisualElement(); motion.AddToClassList("preview-controls-row"); pose.Add(motion);
            rotate = new Toggle("手电 360° 自动旋转"); speed = new FloatField("速度 °/秒") { isDelayed = true };
            motion.Add(rotate); motion.Add(speed);
            direction = new Slider("方向（°）", 0, 359) { showInputField = true }; direction.AddToClassList("preview-angle");
            var angleInput = direction.Q<TextField>(); if (angleInput != null) angleInput.isDelayed = true; pose.Add(direction);
            direction.AddManipulator(new FlashlightDebugSliderGesture(() => undo.BeginGesture("previewAngle"), undo.EndGesture));
            rooms = new PopupField<string>("矿室", new List<string> { "准备地图后可选择" }, 0); pose.Add(rooms);
            roomStart = new Toggle("使用所选矿室起点"); pose.Add(roomStart);
            position = new Vector2Field("人物位置（格）"); pose.Add(position);
            position.Query<FloatField>().ForEach(field => field.isDelayed = true);
            zoom = new FloatField("镜头半高（格）") { isDelayed = true }; pose.Add(zoom);
            cameraOffset = new Vector2Field("镜头偏移（格）"); pose.Add(cameraOffset);
            cameraOffset.Query<FloatField>().ForEach(field => field.isDelayed = true);
            var records = root.Q("previewRecord"); records.AddToClassList("preview-record");
            recordName = new TextField("本次记录名称") { isDelayed = true }; records.Add(recordName);
            compare = new Toggle("对比当前已保存资产"); records.Add(compare);
            map.RegisterValueChangedCallback(evt => Write("previewMap", settings => { settings.MapId = FlashlightPreviewSettings.MapIds[map.choices.IndexOf(evt.newValue)]; settings.RoomIndex = 0; settings.UseRoomStart = true; settings.CameraOffset = Vector2.zero; }));
            enabled.RegisterValueChangedCallback(evt => Write("previewEnabled", settings => settings.Enabled = evt.newValue));
            rotate.RegisterValueChangedCallback(evt => Write("previewRotate", settings => { settings.AutoRotate = evt.newValue; if (!evt.newValue) settings.Angle = CurrentAngle; }));
            speed.RegisterValueChangedCallback(evt => Write("previewSpeed", settings => settings.DegreesPerSecond = evt.newValue));
            direction.RegisterValueChangedCallback(evt => Write("previewAngle", settings => settings.Angle = evt.newValue));
            rooms.RegisterValueChangedCallback(evt => Write("previewRoom", settings => { settings.RoomIndex = rooms.choices.IndexOf(evt.newValue); settings.UseRoomStart = true; settings.CameraOffset = Vector2.zero; }));
            roomStart.RegisterValueChangedCallback(evt => Write("previewRoomStart", settings => { if (!evt.newValue) settings.Position = stage.Position(settings); settings.UseRoomStart = evt.newValue; }));
            position.RegisterValueChangedCallback(evt => Write("previewPosition", settings => { settings.Position = evt.newValue; settings.UseRoomStart = false; settings.CameraOffset = Vector2.zero; }));
            zoom.RegisterValueChangedCallback(evt => Write("previewZoom", settings => settings.OrthographicSize = evt.newValue));
            cameraOffset.RegisterValueChangedCallback(evt => Write("previewPan", settings => settings.CameraOffset = evt.newValue));
            compare.RegisterValueChangedCallback(evt => Write("previewCompare", settings => settings.CompareSaved = evt.newValue));
            recordName.RegisterValueChangedCallback(evt => { draft.Rename(evt.newValue, undo); window.RefreshDrafts(); });
            image.AddManipulator(new FlashlightPreviewPick(point => Write("previewPosition", settings => { settings.Position = stage.Pick(point); settings.UseRoomStart = false; settings.CameraOffset = Vector2.zero; })));
            image.AddManipulator(new FlashlightPreviewNavigation(
                delta => Write("previewPan", settings => settings.CameraOffset = stage.ClampOffset(settings,
                    settings.CameraOffset - Vector2.Scale(delta, new Vector2(3.2f, 2) * settings.OrthographicSize))),
                (scroll, pivot) => Write("previewZoom", settings =>
                {
                    float next = Mathf.Clamp(settings.OrthographicSize * Mathf.Pow(1.1f, Mathf.Clamp(scroll, -12, 12)), 3, 24);
                    settings.CameraOffset = stage.ClampOffset(settings, settings.CameraOffset +
                        Vector2.Scale(pivot - new Vector2(.5f, .5f), new Vector2(3.2f, 2) * (settings.OrthographicSize - next)));
                    settings.OrthographicSize = next;
                }), undo.BeginGesture, undo.EndGesture));
            tick = root.schedule.Execute(Tick).Every(33); Refresh();
        }
        private void Write(string control, Action<FlashlightPreviewSettings> edit)
        {
            try { draft.Write(control, undo, edit); faulted = false; window.RefreshDrafts(); }
            catch (Exception error) { status.text = error.Message; Refresh(); }
        }
        public void Refresh()
        {
            var settings = draft.Settings;
            map.SetValueWithoutNotify(FlashlightPreviewSettings.MapNames[Array.IndexOf(FlashlightPreviewSettings.MapIds, settings.MapId)]);
            enabled.SetValueWithoutNotify(settings.Enabled); rotate.SetValueWithoutNotify(settings.AutoRotate);
            speed.SetValueWithoutNotify(settings.DegreesPerSecond); zoom.SetValueWithoutNotify(settings.OrthographicSize);
            cameraOffset.SetValueWithoutNotify(settings.CameraOffset);
            direction.SetEnabled(!settings.AutoRotate); if (!settings.AutoRotate) direction.SetValueWithoutNotify(settings.Angle);
            roomStart.SetValueWithoutNotify(settings.UseRoomStart); position.SetValueWithoutNotify(settings.UseRoomStart ? stage.Position(settings) : settings.Position);
            compare.SetValueWithoutNotify(settings.CompareSaved); recordName.SetValueWithoutNotify(draft.RecordName);
            var names = stage.Rooms.ToList();
            if (names.Count > 0)
            { rooms.choices = names; rooms.SetValueWithoutNotify(names[Mathf.Clamp(settings.RoomIndex, 0, names.Count - 1)]); }
        }
        private void Tick()
        {
            if (faulted || !draft.Settings.Enabled) return;
            if (window.Draft?.Definition == null || window.SceneDraft == null)
            { status.text = "在来源／场景页选择有效的手电 Definition 和场景配置。"; return; }
            try
            {
                stage.Tick(window.Draft, window.SceneSource, window.SceneDraft, draft.Settings);
                image.image = stage.Image; image.MarkDirtyRepaint(); status.text = stage.Status;
                if (draft.Settings.AutoRotate) direction.SetValueWithoutNotify(CurrentAngle);
            }
            catch (Exception error) { faulted = true; status.text = "预览已暂停：" + error.Message; stage.Dispose(); image.image = null; }
        }
        public void Dispose() { tick.Pause(); image.image = null; stage.Dispose(); }
    }
}
