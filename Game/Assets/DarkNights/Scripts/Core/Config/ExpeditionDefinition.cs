using System;
namespace DarkNights.Core.Config
{
    /// <summary>远征只读数值；唯一作者来源为 balance JSON 的 expedition 段，既有灰松谷规则保持。</summary>
    public sealed class ExpeditionDefinition
    {
        public double OxygenSeconds { get; }
        public int BagCapacity { get; }
        public int ShipCapacity { get; }
        public int StorageCapacity { get; }
        public double OxygenRadius { get; }
        public double RelayRange { get; }
        public int PowerSupply { get; }
        public double DeploySeconds { get; }
        public double ExtractSeconds { get; }
        public double RecallSeconds { get; }
        public double ThreatSeconds { get; }
        public int ModulePrice { get; }
        public ExpeditionDefinition(double oxygenSeconds = 120, int bagCapacity = 24, int shipCapacity = 160, int storageCapacity = 80, double oxygenRadius = 120, double relayRange = 360, int powerSupply = 12, double deploySeconds = 3, double extractSeconds = 2, double recallSeconds = 8, double threatSeconds = 90, int modulePrice = 10)
        {
            if (oxygenSeconds <= 0 || oxygenSeconds > 10000 || double.IsNaN(oxygenSeconds)) throw new ArgumentOutOfRangeException(nameof(oxygenSeconds));
            OxygenSeconds = oxygenSeconds;
            if (bagCapacity <= 0 || bagCapacity > 10000) throw new ArgumentOutOfRangeException(nameof(bagCapacity));
            BagCapacity = bagCapacity;
            if (shipCapacity <= 0 || shipCapacity > 10000) throw new ArgumentOutOfRangeException(nameof(shipCapacity));
            ShipCapacity = shipCapacity;
            if (storageCapacity <= 0 || storageCapacity > 10000) throw new ArgumentOutOfRangeException(nameof(storageCapacity));
            StorageCapacity = storageCapacity;
            if (oxygenRadius <= 0 || oxygenRadius > 10000 || double.IsNaN(oxygenRadius)) throw new ArgumentOutOfRangeException(nameof(oxygenRadius));
            OxygenRadius = oxygenRadius;
            if (relayRange <= 0 || relayRange > 10000 || double.IsNaN(relayRange)) throw new ArgumentOutOfRangeException(nameof(relayRange));
            RelayRange = relayRange;
            if (powerSupply <= 0 || powerSupply > 10000) throw new ArgumentOutOfRangeException(nameof(powerSupply));
            PowerSupply = powerSupply;
            if (deploySeconds <= 0 || deploySeconds > 10000 || double.IsNaN(deploySeconds)) throw new ArgumentOutOfRangeException(nameof(deploySeconds));
            DeploySeconds = deploySeconds;
            if (extractSeconds <= 0 || extractSeconds > 10000 || double.IsNaN(extractSeconds)) throw new ArgumentOutOfRangeException(nameof(extractSeconds));
            ExtractSeconds = extractSeconds;
            if (recallSeconds <= 0 || recallSeconds > 10000 || double.IsNaN(recallSeconds)) throw new ArgumentOutOfRangeException(nameof(recallSeconds));
            RecallSeconds = recallSeconds;
            if (threatSeconds <= 0 || threatSeconds > 10000 || double.IsNaN(threatSeconds)) throw new ArgumentOutOfRangeException(nameof(threatSeconds));
            ThreatSeconds = threatSeconds;
            if (modulePrice <= 0 || modulePrice > 10000) throw new ArgumentOutOfRangeException(nameof(modulePrice));
            ModulePrice = modulePrice;
        }
    }
}
