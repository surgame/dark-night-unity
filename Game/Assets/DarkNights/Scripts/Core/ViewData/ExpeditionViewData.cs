using System;
using System.Collections.Generic;

namespace DarkNights.Core.ViewData
{
    /// <summary>远征的冻结数据合同；仅供投影和保存，构造时复制集合，不拥有权威状态。</summary>
    public sealed class ExpeditionViewData
    {
        public ExpeditionShipData Ship { get; }
        public int Run { get; }
        public int Phase { get; }
        public double Risk { get; }
        public double Clock { get; }
        public bool Settled { get; }
        public int RobotModule { get; }
        public int CargoModule { get; }
        public int CrewModule { get; }
        public int LostCargo { get; }
        public int LostDevices { get; }
        public int ResupplyCost { get; }
        public IReadOnlyList<ExpeditionActorData> Crew { get; }
        public IReadOnlyList<ExpeditionDeviceData> Devices { get; }
        public ExpeditionViewData(int run, int phase, double risk, double clock, bool settled, int robotModule, int cargoModule, int crewModule, int lostCargo, int lostDevices, ExpeditionActorData[] crew, ExpeditionDeviceData[] devices, int resupplyCost = 0, ExpeditionShipData ship = null)
        {
            Ship = ship;
            Run = run;
            Phase = phase;
            Risk = risk;
            Clock = clock;
            Settled = settled;
            RobotModule = robotModule;
            CargoModule = cargoModule;
            CrewModule = crewModule;
            LostCargo = lostCargo;
            LostDevices = lostDevices;
            ResupplyCost = resupplyCost;
            Crew = Array.AsReadOnly((ExpeditionActorData[])(crew ?? Array.Empty<ExpeditionActorData>()).Clone());
            Devices = Array.AsReadOnly((ExpeditionDeviceData[])(devices ?? Array.Empty<ExpeditionDeviceData>()).Clone());
        }
    }
}
