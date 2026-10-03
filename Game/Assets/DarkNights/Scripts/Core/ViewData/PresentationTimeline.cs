using System;
using System.Collections.Generic;

namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 客户端两帧表现插值，演员、船体及设备共用同一时间切点，避免乘船对象相对滑动。
    /// 不外推、生成动作或修改生命；暂停及换 epoch 立即使用新帧，缺帧时停在最后权威位置。
    /// </summary>
    public sealed class PresentationTimeline
    {
        private SessionViewData current;
        private readonly Dictionary<int, ActorViewData> previous = new Dictionary<int, ActorViewData>();
        private readonly Dictionary<int, BuildingViewData> previousBuildings = new Dictionary<int, BuildingViewData>();
        private readonly Dictionary<int, ExpeditionDeviceData> previousDevices = new Dictionary<int, ExpeditionDeviceData>();
        private double received, duration;

        public void Push(SessionViewData frame, double now)
        {
            if (frame == null) { Reset(); return; }
            if (current != null && frame.Publication <= current.Publication && frame.Epoch == current.Epoch) return;
            previous.Clear();
            previousBuildings.Clear(); previousDevices.Clear();
            duration = 0;
            if (current != null && current.Epoch == frame.Epoch && !frame.Paused && !frame.Loading &&
                frame.World.Camp.Mode == "Playing" && frame.ServerTick > current.ServerTick)
            {
                duration = Math.Min(0.2, (frame.ServerTick - current.ServerTick) / 60.0);
                foreach (ActorViewData actor in current.World.Actors) previous.Add(actor.Id, actor);
                foreach (BuildingViewData building in current.World.Buildings) previousBuildings.Add(building.Id, building);
                if (current.World.Expedition != null)
                    foreach (ExpeditionDeviceData device in current.World.Expedition.Devices) previousDevices.Add(device.Id, device);
            }
            current = frame; received = now;
        }

        public float X(ActorViewData actor, double now) => Previous(actor, out var old)
            ? old.X + (actor.X - old.X) * (float)Ratio(now) : actor.X;

        public float Height(ActorViewData actor, double now) => Previous(actor, out var old)
            ? old.Height + (actor.Height - old.Height) * (float)Ratio(now) : actor.Height;

        public float X(BuildingViewData building, double now) => previousBuildings.TryGetValue(building.Id, out var old) && old.Kind == building.Kind
            ? old.X + (building.X - old.X) * (float)Ratio(now) : building.X;

        public float Height(ExpeditionDeviceData device, double now) => device != null && previousDevices.TryGetValue(device.Id, out var old)
            ? old.Height + (device.Height - old.Height) * (float)Ratio(now) : device?.Height ?? 0;

        public double ActionTime(ActorViewData actor, double now)
        {
            if (!Previous(actor, out var old) || old.Activity != actor.Activity || actor.ActionTime < old.ActionTime)
                return actor.ActionTime;
            return old.ActionTime + (actor.ActionTime - old.ActionTime) * Ratio(now);
        }

        public void Reset() { current = null; previous.Clear(); previousBuildings.Clear(); previousDevices.Clear(); duration = 0; }
        private bool Previous(ActorViewData actor, out ActorViewData value) =>
            previous.TryGetValue(actor.Id, out value) && value.Kind == actor.Kind;
        private double Ratio(double now) => duration <= 0 ? 1 : Math.Max(0, Math.Min(1, (now - received) / duration));
    }
}
