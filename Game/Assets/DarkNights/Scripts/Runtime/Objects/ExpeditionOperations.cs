using System;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Logic.Terrain;

namespace DarkNights.Runtime.Objects
{
    /// <summary>远征阶段与成长协调；状态由 Camp、飞船、角色分别拥有，所有操作必须处于会话事务。</summary>
    public sealed class ExpeditionOperations
    {
        // 0 整备，1 探索，2 撤收，3 起飞倒计时，4 已结算。
        private readonly ObjectSession world;
        public Action<DarkNights.Core.Save.SessionSnapshot> CommitSave { get; set; }
        internal ExpeditionOperations(ObjectSession world) { this.world = world; }
        internal ExpeditionDefinition Rules => world.Catalog.Balance.Expedition;
        internal BuildingBehaviour Ship => world.Index.Buildings.FirstOrDefault(b => b.RuleKey == "ship");
        internal bool Active => world.Camp.Read().ExpeditionPhase is 1 or 2;
        internal bool AtShip(ActorBehaviour a) => Ship != null &&
            Math.Abs(a.X - Ship.X) < 100 && Math.Abs(a.Read().Height - Ship.Read().Height) < 24;

        internal void Advance(double seconds)
        {
            try
            {
                world.Mutations.Run(() =>
                {
                    double delta = world.Camp.BeginStep(seconds); Tick(delta);
                    if (Active || world.Camp.Read().ExpeditionPhase == 3)
                    {
                        foreach (var a in world.Index.Actors.Where(a => a.Read().ExpeditionRole == 0 && !a.Read().Boarded).ToArray()) a.Tick(delta);
                        world.Projectiles.Tick(delta);
                    }
                    return true;
                });
            }
            catch (System.IO.IOException) { SaveFailed(); }
            catch (UnauthorizedAccessException) { SaveFailed(); }
        }
        private void SaveFailed()
        {
            world.SetTime(true, world.Speed);
            world.Feedback.Notify("返航存档写入失败，结算未提交。修复存储后取消暂停重试。", true);
        }

        internal void Prepare()
        {
            world.Camp.Edit().ExpeditionRun = 1;
            world.Economy.SetStock(new ResourceAmounts());
            var ship = Ship.Edit(); ship.DeviceStage = 3; ship.Powered = true;
            world.Notify("远征整备：出发后采矿，返回船边卸货。首次收益可购买舱段。");
        }

        internal int Command(string operation, int target, ActorBehaviour hero)
        {
            var c = world.Camp.Edit();
            switch (operation)
            {
                case "depart":
                    if (c.ExpeditionPhase != 0 && c.ExpeditionPhase != 4) return 0;
                    if (c.ExpeditionSettled) c.ExpeditionRun++;
                    c.ExpeditionPhase = 1; c.ExpeditionSettled = false; c.ExpeditionClock = 0; c.ExpeditionRisk = 0;
                    c.LostCargo = c.LostDevices = 0;
                    foreach (var a in world.Index.Actors.Where(a => !a.Enemy))
                    {
                        var s = a.Edit(); s.Boarded = false; s.Oxygen = Rules.OxygenSeconds;
                        s.Hp = a.MaximumHp; s.X = Ship.X + 70; s.Height = Ship.Read().Height;
                        s.TaskTarget = s.TaskPhase = 0;
                    }
                    world.ExpeditionDevices.BeginDeployment();
                    world.Notify("已降落。矿物只在卸入船仓并撤离后结算。"); return 1;
                case "unload":
                    if (!Active || hero == null || !AtShip(hero)) return 0;
                    ExpeditionCargo.Transfer(hero, Ship, Rules.ShipCapacity * (1 + c.CargoModule)); return 1;
                case "board":
                    if ((!Active && c.ExpeditionPhase != 3) || hero == null || !AtShip(hero)) return 0;
                    ExpeditionCargo.Transfer(hero, Ship, Rules.ShipCapacity * (1 + c.CargoModule));
                    hero.Edit().Boarded = true; HeroControlBehaviour.ResetInput(hero.Edit()); return 1;
                case "recall":
                    if (c.ExpeditionPhase != 1) return 0;
                    c.ExpeditionPhase = 2;
                    foreach (var a in world.Index.Actors.Where(a => a.Read().ExpeditionRole == 2 && !a.Enemy))
                    { a.Edit().TaskTarget = Ship.Id; a.Edit().TaskPhase = 5; }
                    world.Notify("正在撤收。请登船；未回收设备与货物会列入损失。"); return 1;
                case "launch":
                    if (c.ExpeditionPhase != 2 || world.Index.Actors.Any(a => !a.Enemy && !a.Read().Boarded) ||
                        world.Index.Buildings.Any(b => b != Ship && b.Read().DeviceStage != 0 && b.Read().DeviceStage != 6)) return 0;
                    goto case "emergency";
                case "emergency":
                    if (!Active) return 0;
                    c.ExpeditionPhase = 3; c.ExpeditionClock = Rules.RecallSeconds;
                    world.Notify("起飞倒计时开始；截止时未登船的货物和设备将损失。", true); return 1;
                case "robot": case "cargo": case "crew":
                    if (c.ExpeditionPhase != 0 && c.ExpeditionPhase != 4) return 0;
                    if (operation == "robot" && c.RobotModule != 0 || operation == "cargo" && c.CargoModule != 0 ||
                        operation == "crew" && c.CrewModule != 0) return 0;
                    if (!world.Economy.Pay(new ResourceAmounts(iron: Rules.ModulePrice))) return 0;
                    if (operation == "robot") c.RobotModule = 1;
                    if (operation == "cargo") c.CargoModule = 1;
                    if (operation == "crew") c.CrewModule = 1;
                    return 1;
                case "resupply":
                    if (c.ExpeditionPhase != 0 && c.ExpeditionPhase != 4 || c.ResupplyCost == 0 ||
                        !world.Economy.Pay(new ResourceAmounts(iron: c.ResupplyCost))) return 0;
                    c.ResupplyCost = 0; return 1;
                case "relay":
                    return c.ExpeditionPhase == 1 && hero != null ? world.ExpeditionDevices.RequestRelay(hero) : 0;
                case "mine":
                    return c.ExpeditionPhase == 1 ? world.ExpeditionDevices.AssignMiner(target) : 0;
                default: return 0;
            }
        }

        internal void Tick(double delta)
        {
            var c = world.Camp.Edit();
            if (c.ExpeditionPhase == 3)
            {
                world.ExpeditionDevices.Tick(delta);
                c.ExpeditionClock = Math.Max(0, c.ExpeditionClock - delta);
                if (c.ExpeditionClock == 0) Settle();
                return;
            }
            if (!Active) return;
            c.ExpeditionClock += delta;
            c.ExpeditionRisk += delta * (1 + world.Index.Buildings.Count(b => b != Ship && b.Read().Powered) * .15);
            world.ExpeditionDevices.Tick(delta);
            foreach (var a in world.Index.Actors.Where(a => !a.Enemy).ToArray())
            {
                var s = a.Edit();
                if (s.Boarded || s.ExpeditionRole == 1) continue;
                bool supplied = AtShip(a) || world.ExpeditionDevices.OxygenAt(a.X, s.Height);
                s.Oxygen = Math.Clamp(s.Oxygen + delta * (supplied ? 12 : -1), 0, Rules.OxygenSeconds);
                if (s.Oxygen <= 0)
                {
                    s.Hp = Math.Max(0, s.Hp - delta);
                    if (s.Hp <= 0) { s.Boarded = true; c.LostCargo += s.CargoIron + s.CargoGold; s.CargoIron = s.CargoGold = 0; }
                }
            }
            world.ExpeditionThreat.Tick(delta);
            if (world.Index.Actors.Any(a => a.Read().OwnerSlot >= 0) &&
                world.Index.Actors.Where(a => a.Read().OwnerSlot >= 0).All(a => a.Hp <= 0)) Settle();
        }

        private void Settle()
        {
            var c = world.Camp.Edit();
            if (c.ExpeditionSettled) return;
            foreach (var a in world.Index.Actors.Where(a => !a.Enemy).ToArray())
            {
                var s = a.Edit();
                if (!s.Boarded || s.Hp <= 0)
                {
                    c.LostCargo += s.CargoIron + s.CargoGold; s.CargoIron = s.CargoGold = 0;
                    if (s.ExpeditionRole > 0) { c.ResupplyCost += Rules.ModulePrice / 2; world.Lifecycle.Retire(a); continue; }
                }
                s.Boarded = true;
                // 船仓满载时留在个人包中的部分不属于成功入船货物。
                c.LostCargo += s.CargoIron + s.CargoGold; s.CargoIron = s.CargoGold = 0;
                s.TaskTarget = s.TaskPhase = 0; s.TaskClock = 0;
            }
            foreach (var b in world.Index.Buildings.Where(b => b != Ship).ToArray())
                if (b.Read().DeviceStage != 0 && b.Read().DeviceStage != 6)
                { c.LostCargo += b.Read().CargoIron + b.Read().CargoGold; c.LostDevices++; c.ResupplyCost += (int)Math.Ceiling(b.Definition.Cost.Iron); world.Lifecycle.Retire(b); }
            var ship = Ship.Edit();
            world.Economy.AddResource("iron", ship.CargoIron); world.Economy.AddResource("gold", ship.CargoGold);
            ship.CargoIron = ship.CargoGold = 0;
            c.ExpeditionSettled = true; c.ExpeditionPhase = 4;
            foreach (var enemy in world.Index.Actors.Where(a => a.Enemy).ToArray()) world.Lifecycle.Retire(enemy);
            CommitSave?.Invoke(world.CaptureWorld());
            world.Notify("已返航：船仓收益已结算。可购买舱段后再次出发。");
        }
    }
}
