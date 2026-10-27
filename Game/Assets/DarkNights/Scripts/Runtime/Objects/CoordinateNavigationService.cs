using System;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.ViewData;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 原航程能力的坐标航行事务适配；唯一状态仍归ExpeditionJourneyState。
    /// 坐标抵达只回太空待命，不创建地图、结算货物或授予示意星球降落资格。
    /// </summary>
    internal static class CoordinateNavigationService
    {
        internal static int Select(ObjectSession world, ActorBehaviour hero, float x, float y, int revision)
        {
            var flow = world.Flow;
            NavigationNodeDefinition.ValidateCoordinate(x); NavigationNodeDefinition.ValidateCoordinate(y);
            if (!flow.Enabled || flow.Phase is not (JourneyPhase.Orbit or JourneyPhase.Transit) ||
                !flow.AtNavigation(hero) || world.Journey.Read().Revision != revision) return 0;
            var j = world.Journey.Edit();
            if (Math.Abs(j.StarX - x) + Math.Abs(j.StarY - y) < .01f) return 0;
            j.JourneyId = Guid.NewGuid().ToString("N"); j.PlanetId = j.MapId = j.Seed = "";
            j.CoordinateFlight = true; j.NavigationTargetX = x; j.NavigationTargetY = y;
            world.Journey.SetPhase(JourneyPhase.Transit);
            world.Mutations.AfterCommit(flow.ResetPending);
            world.Notify($"坐标航路已确认：X {x:F0}，Y {y:F0}。");
            return 1;
        }

        internal static void Tick(ObjectSession world, double delta)
        {
            var j = world.Journey.Edit();
            double dx = j.NavigationTargetX - j.StarX, dy = j.NavigationTargetY - j.StarY;
            double distance = Math.Sqrt(dx * dx + dy * dy);
            double amount = Math.Min(distance, world.Flow.Navigation.Speed * delta);
            if (distance > 0)
            {
                j.StarX += (float)(dx * amount / distance);
                j.StarY += (float)(dy * amount / distance);
            }
            if (amount < distance) return;
            world.Flow.ClearTarget(); world.Journey.SetPhase(JourneyPhase.Orbit);
            world.Notify("坐标航行完毕，飞船在当前位置待命。勘测资料已记入星图。");
        }
    }
}
