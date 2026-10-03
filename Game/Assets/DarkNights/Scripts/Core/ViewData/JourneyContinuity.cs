namespace DarkNights.Core.ViewData
{
    /// <summary>冻结投影间的航程连续性判定；仅允许同一艘船的正常到达跨 epoch 接续，读档、换房及旧帧不沿用旧取景。</summary>
    public static class JourneyContinuity
    {
        public static bool IsArrival(SessionViewData previous, SessionViewData next)
        {
            var before = previous?.World.Expedition;
            var after = next?.World.Expedition;
            var a = before?.Journey; var b = after?.Journey;
            return a?.Enabled == true && b?.Enabled == true && before.Ship != null && after.Ship != null &&
                !previous.Loading && !next.Loading && next.Epoch == previous.Epoch + 1 &&
                before.Ship.Id == after.Ship.Id && a.JourneyId.Length != 0 && a.JourneyId == b.JourneyId &&
                a.PlanetId == b.PlanetId && a.MapId == b.MapId &&
                (a.Phase is JourneyPhase.Preparing or JourneyPhase.Transit) &&
                (b.Phase is JourneyPhase.ArrivalSync or JourneyPhase.Descent or JourneyPhase.Landed);
        }

        public static bool TryShipPosition(SessionViewData frame, out float x, out float height)
        {
            x = height = 0;
            var expedition = frame?.World.Expedition;
            if (expedition?.Ship == null) return false;
            bool found = false;
            foreach (var building in frame.World.Buildings)
                if (building.Id == expedition.Ship.Id) { x = building.X; found = true; break; }
            foreach (var device in expedition.Devices)
                if (device.Id == expedition.Ship.Id) { height = device.Height; return found; }
            return false;
        }
    }
}
