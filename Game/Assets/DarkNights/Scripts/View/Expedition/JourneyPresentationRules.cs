using System.Linq;
using DarkNights.Core.Logic.State;
using DarkNights.Core.Logic.Terrain;
using DarkNights.Core.ViewData;

namespace DarkNights.View.Expedition
{
    /// <summary>冻结航程的本地文字和交互提示规则；距离只控制按钮反馈，最终权限始终由服务端重新校验。</summary>
    public static class JourneyPresentationRules
    {
        public static string Stage(JourneyPhase phase)
        {
            switch (phase)
            {
                case JourneyPhase.Orbit: return "太空待命";
                case JourneyPhase.Preparing: return "准备目的地";
                case JourneyPhase.Transit: return "航行中";
                case JourneyPhase.ArrivalSync: return "到达同步";
                case JourneyPhase.Descent: return "手动降落";
                case JourneyPhase.Landed: return "已着陆";
                default: return "等待航程";
            }
        }

        public static bool AtCockpit(WorldViewData world, int slot)
        {
            var expedition = world?.Expedition;
            var actor = world?.Actors.FirstOrDefault(a => a.ControllerSlot == slot && a.Hp > 0);
            var crew = expedition?.Crew.FirstOrDefault(c => c.Id == actor?.Id);
            var ship = world?.Buildings.FirstOrDefault(b => b.Id == expedition?.Ship?.Id);
            var device = expedition?.Devices.FirstOrDefault(d => d.Id == ship?.Id);
            return actor != null && crew?.Boarded == true && ship != null && device != null &&
                ShipGeometry.AtPilot(actor.X - ship.X, actor.Height - device.Height);
        }

        public static string SelectionBlock(SessionViewData frame, int slot, bool ready)
        {
            var journey = frame?.World.Expedition?.Journey;
            if (journey?.Enabled != true) return "当前会话没有导航目录。";
            if (!ready) return "正在同步会话，请等待就绪。";
            if (frame.Paused) return "会话已暂停，恢复后可以确认目的地。";
            if (frame.HostOnly && slot != 0) return "当前由房主操作飞船。";
            if (journey.Phase != JourneyPhase.Orbit) return "当前航程阶段不能重新选择目的地。";
            if (!AtCockpit(frame.World, slot)) return "走到船内右侧驾驶台后选择目的地。";
            if (frame.World.Expedition.Ship.PilotId != 0) return "驾驶位已被占用。";
            return "";
        }

        public static bool InSpace(JourneyViewData journey) => journey?.Enabled == true &&
            journey.Phase is JourneyPhase.Orbit or JourneyPhase.Preparing or JourneyPhase.Transit;

        public static string Guidance(JourneyViewData journey, bool pilot)
        {
            switch (journey.Phase)
            {
                case JourneyPhase.Orbit: return "船内可自由走动；走到右侧驾驶台，点击选择目的地。";
                case JourneyPhase.Preparing: return "正在准备星球地图；驾驶者可取消，乘员可继续在船内走动。";
                case JourneyPhase.Transit: return "正在前往目的地；星点过场只在本地播放。";
                case JourneyPhase.ArrivalSync: return "已到达星球半空，等待地图和乘员同步；飞船保持悬停。";
                case JourneyPhase.Descent: return pilot ? "A/D 平移 · 空格上升 · S 下降；停稳后点击安全区着陆。" :
                    "乘员可在舱内走动；驾驶位空闲时可到驾驶台接管。";
                case JourneyPhase.Landed: return "坡道已展开，可步行下船；驾驶者先点击离座。";
                default: return "";
            }
        }
    }
}
