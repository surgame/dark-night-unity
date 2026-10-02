using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.Tests;
using Newtonsoft.Json.Linq;

/// <summary>旧远征用例的实际矿工派工诊断；复用原地图和真实会话，逐矿床发送可信命令，不传送人物、不修改地形或矿床位置。</summary>
public static class OxygenMinerProbe
{
    public static async Task<string> Run()
    {
        string output = Path.GetFullPath("../artifacts/oxygen-removal-20261003/miner-reachability-r1.json");
        if (File.Exists(output)) throw new IOException("Diagnostic identity exists.");
        using var scope = await UnifiedSessionScope.Create();
        var factory = typeof(ExpeditionTests).GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static);
        var world = (ObjectSession)factory.Invoke(null, new object[] { scope });
        using var authority = new SessionAuthority(world);
        var host = authority.Connect(0);
        authority.AcknowledgeReady(host, authority.Epoch, authority.Revision, true);
        var save = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()));
        save["world"]["expedition"]["CrewModule"] = 1;
        save["world"]["expedition"]["RobotModule"] = 1;
        world.Restore(save.ToString());
        host = authority.Connect(0); authority.AcknowledgeReady(host, authority.Epoch, authority.Revision, true);
        long sequence = 1;
        SessionResultCode Command(string kind, int target = 0)
        {
            var hero = world.Index.Actors.Single(a => a.CaptureState().OwnerSlot == 0);
            var request = new SessionRequest(SessionOperation.Expedition, SessionAuthority.ProtocolVersion,
                authority.Epoch, authority.PolicyRevision, sequence++, new[] { hero.Id }, kind: kind,
                targetId: target, controlLease: hero.CaptureState().ControlLease);
            var receipt = authority.Submit(host, request);
            if (receipt.Code != SessionResultCode.Pending) return receipt.Code;
            return authority.Tick().Single(r => r.Sequence == request.Sequence).Code;
        }
        if (Command("depart") != SessionResultCode.Applied) throw new InvalidOperationException("Departure failed.");
        var results = new JArray(); int selected = 0;
        foreach (var deposit in world.Index.MineralDeposits.Cast<MineralDepositBehaviour>())
        {
            var code = Command("mine", deposit.Id);
            results.Add(new JObject { ["id"] = deposit.Id, ["x"] = deposit.X, ["y"] = deposit.Y,
                ["remaining"] = deposit.Remaining, ["result"] = code.ToString() });
            if (code == SessionResultCode.Applied) selected = deposit.Id;
        }
        if (selected != 0) Command("mine", selected);
        for (int tick = 0; selected != 0 && tick < 9000; tick++)
        {
            authority.Tick();
            if (tick % 300 == 0) await Task.Yield();
            if (world.CaptureView().Expedition.Devices.Sum(d => d.Iron + d.Gold) > 0) break;
        }
        File.WriteAllText(output, new JObject { ["deposits"] = results, ["selected"] = selected,
            ["view"] = JToken.FromObject(world.CaptureView().Expedition),
            ["notVisualAcceptance"] = true }.ToString());
        return output;
    }
}
