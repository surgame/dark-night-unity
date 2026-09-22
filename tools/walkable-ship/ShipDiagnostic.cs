using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DarkNights.Tests;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using Newtonsoft.Json.Linq;
/// <summary>读取旧远征种子中矿工的真实时序，定位新坡道增加的往返时间或实际通路阻塞。</summary>
public static class ShipDiagnostic
{
    public static async Task<string> Run()
    {
        using var scope = await UnifiedSessionScope.Create();
        var world = (ObjectSession)typeof(ExpeditionTests).GetMethod("Create", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { scope });
        using var authority = new SessionAuthority(world);
        var host = authority.Connect(0); authority.AcknowledgeReady(host, authority.Epoch, authority.Revision, true);
        var save = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()));
        save["world"]["expedition"]["CrewModule"] = 1; save["world"]["expedition"]["RobotModule"] = 1;
        world.Restore(save.ToString()); host = authority.Connect(0); authority.AcknowledgeReady(host, authority.Epoch, authority.Revision, true);
        void Send(long sequence, string kind, int[] actors = null, int target = 0, int lease = 0)
        {
            authority.Submit(host, new SessionRequest(SessionOperation.Expedition, SessionAuthority.ProtocolVersion, authority.Epoch,
                authority.PolicyRevision, sequence, actors, targetId: target, kind: kind, controlLease: lease)); authority.Tick();
        }
        Send(1, "depart"); var hero = world.Index.Actors.Single(a => a.CaptureState().OwnerSlot == 0);
        var ore = world.Index.MineralDeposits.First(); Send(2, "mine", new[] { hero.Id }, ore.Id, hero.CaptureState().ControlLease);
        var samples = new JArray();
        for (int tick = 0; tick < 12000; tick++)
        {
            authority.Tick();
            if (tick % 600 != 0) continue;
            var e = world.CaptureView().Expedition;
            samples.Add(new JObject { ["seconds"] = tick / 60, ["actors"] = new JArray(world.Index.Actors.Where(a => a.RuleKey is "miner" or "hauler").Select(a =>
            { var s = a.CaptureState(); return new JObject { ["kind"] = a.RuleKey, ["x"] = s.X, ["h"] = s.Height, ["hp"] = s.Hp, ["bag"] = s.CargoIron, ["task"] = s.TaskTarget, ["phase"] = s.TaskPhase, ["boarded"] = s.Boarded, ["oxygen"] = s.Oxygen }; })),
                ["devices"] = JArray.FromObject(e.Devices), ["lost"] = e.LostCargo });
            if (e.Devices.Sum(d => d.Iron) > 0) break;
        }
        string output = System.IO.Path.GetFullPath("../artifacts/walkable-ship/miner-diagnostic.json");
        System.IO.File.WriteAllText(output, samples.ToString()); return output;
    }
}
