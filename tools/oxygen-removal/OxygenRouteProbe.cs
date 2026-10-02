using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using DarkNights.Core.Config.Terrain;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.Runtime.Terrain;
using DarkNights.Tests;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>沿原失败用例的可信输入诊断阻塞地形，并只读调用原寻路器；不传送、不挖路、不替换生成器或断言。</summary>
public static class OxygenRouteProbe
{
    public static async Task<string> Run()
    {
        string output = Path.GetFullPath("../artifacts/oxygen-removal-20261003/route-r1.json");
        if (File.Exists(output)) throw new IOException("Diagnostic identity exists.");
        using var scope = await UnifiedSessionScope.Create();
        var factory = typeof(ExpeditionTests).GetMethod("Create", BindingFlags.NonPublic | BindingFlags.Static);
        var world = (ObjectSession)factory.Invoke(null, new object[] { scope });
        using var authority = new SessionAuthority(world);
        var host = authority.Connect(0);
        authority.AcknowledgeReady(host, authority.Epoch, authority.Revision, true);
        var hero = world.Index.Actors.Single(a => a.CaptureState().OwnerSlot == 0);
        var seeded = JObject.Parse(world.SaveCodec.Serialize(world.CaptureWorld()));
        var actorSave = seeded["world"]["actors"].OfType<JObject>().Single(v => (int)v["id"] == hero.Id);
        actorSave["slot_1"] = GameCore.Objects.Definition.ObjectDefinitionDatabase.Instance.GetDefinitionByKey("item.pickaxe").Guid.ToString();
        actorSave["inventory_revision"] = 1;
        actorSave["jetpack_owned"] = actorSave["jetpack_equipped"] = true;
        actorSave["jetpack_fuel"] = world.Catalog.Balance.HeroControl.FuelSeconds;
        world.Restore(seeded.ToString());
        authority.AcknowledgeReady(host, authority.Epoch, authority.Revision, true);
        hero = world.Index.Actors.Single(a => a.CaptureState().OwnerSlot == 0);
        var request = new SessionRequest(SessionOperation.Expedition, SessionAuthority.ProtocolVersion,
            authority.Epoch, authority.PolicyRevision, 1, kind: "depart");
        authority.Submit(host, request); authority.Tick();
        var deposit = world.Index.MineralDeposits.Cast<MineralDepositBehaviour>().First();
        var samples = new JArray();
        for (int tick = 0; tick < 1300 && hero.X < deposit.X - 5; tick++)
        {
            var state = hero.CaptureState();
            authority.SubmitInput(host, new HeroInputRequest(SessionAuthority.ProtocolVersion, authority.Epoch,
                authority.PolicyRevision, hero.Id, state.ControlLease, tick + 1, authority.ServerTick,
                1, false, false, false, hero.X < 416));
            authority.Tick();
            if (tick % 60 == 0 || tick == 1299)
                samples.Add(new JObject { ["tick"] = tick, ["x"] = hero.X,
                    ["height"] = hero.CaptureState().Height, ["fuel"] = hero.CaptureState().JetpackFuel });
            if (tick % 300 == 0) await Task.Yield();
        }
        var current = hero.CaptureState();
        var hits = new JArray();
        for (float x = hero.X - 8; x <= hero.X + 32; x += 2)
            for (float h = current.Height; h <= current.Height + 32; h += 2)
                if (TerrainHeroMotion.Solid(world.Terrain.Map, x, h))
                    hits.Add(new JObject { ["x"] = x, ["height"] = h });
        var navigationType = typeof(ObjectSession).Assembly.GetType("DarkNights.Runtime.Objects.ExpeditionNavigation");
        var navigation = Activator.CreateInstance(navigationType, BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { world }, null);
        var find = navigationType.GetMethod("Find", BindingFlags.Instance | BindingFlags.NonPublic);
        var floor = navigationType.GetMethod("Floor", BindingFlags.Instance | BindingFlags.NonPublic);
        var ship = world.Index.Buildings.Single(b => b.RuleKey == "ship");
        var start = new Vector2(ship.X + DarkNights.Core.Logic.Terrain.ShipGeometry.RampToe,
            world.CaptureView().Expedition.Devices.Single(d => d.Id == ship.Id).Height);
        var trace = new JArray(); var cursor = start;
        for (int i = 0; i < 220 && cursor.x < deposit.X; i++)
        {
            object[] arguments = { cursor.x + 8, cursor.y, 0f };
            bool exists = (bool)floor.Invoke(navigation, arguments);
            trace.Add(new JObject { ["x"] = cursor.x + 8, ["previousHeight"] = cursor.y,
                ["floorFound"] = exists, ["height"] = (float)arguments[2] });
            if (!exists) break;
            cursor = new Vector2(cursor.x + 8, (float)arguments[2]);
        }
        var goals = new JArray();
        foreach (var mineral in world.Index.MineralDeposits.Cast<MineralDepositBehaviour>())
        {
            float h = PlayableTerrain.OriginY - (mineral.Y + .5f) * 16;
            var path = (Queue<Vector2>)find.Invoke(navigation, new object[] { start, new Vector2(mineral.X, h), false });
            goals.Add(new JObject { ["id"] = mineral.Id, ["x"] = mineral.X, ["height"] = h,
                ["goalSolid"] = TerrainHeroMotion.Solid(world.Terrain.Map, mineral.X, h), ["pathPoints"] = path.Count });
        }
        File.WriteAllText(output, new JObject { ["samples"] = samples, ["solidNearStop"] = hits,
            ["floorTrace"] = trace, ["goals"] = goals, ["start"] = new JObject { ["x"] = start.x, ["height"] = start.y },
            ["expectedMinimumX"] = deposit.X - 10, ["actualState"] = JToken.FromObject(current),
            ["notVisualAcceptance"] = true }.ToString());
        return output;
    }
}
