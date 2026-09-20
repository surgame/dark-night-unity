using System.Linq;
using DarkNights.Core.ViewData;
using MemoryPack;

namespace DarkNights.Runtime.Network
{
    /// <summary>远征冻结合同的有界网络字段；接收后立即复制，不能参与玩法计算。</summary>
    [MemoryPackable]
    public partial class ExpeditionViewWire
    {
        public int Run { get; set; }
        public int Phase { get; set; }
        public double Risk { get; set; }
        public double Clock { get; set; }
        public bool Settled { get; set; }
        public int RobotModule { get; set; }
        public int CargoModule { get; set; }
        public int CrewModule { get; set; }
        public int LostCargo { get; set; }
        public int LostDevices { get; set; }
        public int ResupplyCost { get; set; }
        public ExpeditionActorWire[] Crew { get; set; }
        public ExpeditionDeviceWire[] Devices { get; set; }
        public static ExpeditionViewWire From(ExpeditionViewData v) => v == null ? null : new ExpeditionViewWire
        {
            Run = v.Run,
            Phase = v.Phase,
            Risk = v.Risk,
            Clock = v.Clock,
            Settled = v.Settled,
            RobotModule = v.RobotModule,
            CargoModule = v.CargoModule,
            CrewModule = v.CrewModule,
            LostCargo = v.LostCargo,
            LostDevices = v.LostDevices,
            ResupplyCost = v.ResupplyCost,
            Crew = v.Crew.Select(ExpeditionActorWire.From).ToArray(),
            Devices = v.Devices.Select(ExpeditionDeviceWire.From).ToArray(),
        };
        public ExpeditionViewData Freeze() => new ExpeditionViewData(Run, Phase, Risk, Clock, Settled, RobotModule, CargoModule, CrewModule, LostCargo, LostDevices, Crew?.Select(v => v.Freeze()).ToArray(), Devices?.Select(v => v.Freeze()).ToArray(), ResupplyCost);
    }
}
