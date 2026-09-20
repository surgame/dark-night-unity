using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace DarkNights.Core.Config
{
    /// <summary>
    /// 按内容 ID 索引的只读数值目录。构造时复制各类字典，所有定义和嵌套集合不可变，防止界面或模拟改写全局规则。
    /// </summary>
    public sealed class BalanceDefinition
    {
        public ExpeditionDefinition Expedition { get; }
        public int SchemaVersion { get; }
        public EconomyDefinition Economy { get; }
        public HeroControlDefinition HeroControl { get; }
        public IReadOnlyDictionary<string, UnitDefinition> Units { get; }
        public IReadOnlyDictionary<string, BuildingDefinition> Buildings { get; }
        public IReadOnlyDictionary<string, WorksiteDefinition> Worksites { get; }

        public BalanceDefinition(
            int schemaVersion,
            EconomyDefinition economy,
            IReadOnlyDictionary<string, UnitDefinition> units,
            IReadOnlyDictionary<string, BuildingDefinition> buildings,
            IReadOnlyDictionary<string, WorksiteDefinition> worksites, HeroControlDefinition heroControl = null, ExpeditionDefinition expedition = null)
        {
            Expedition = expedition ?? new ExpeditionDefinition();
            SchemaVersion = schemaVersion;
            HeroControl = heroControl;
            Economy = economy ?? throw new ArgumentNullException(nameof(economy));
            Units = new ReadOnlyDictionary<string, UnitDefinition>(new Dictionary<string, UnitDefinition>(units ?? throw new ArgumentNullException(nameof(units)), StringComparer.Ordinal));
            Buildings = new ReadOnlyDictionary<string, BuildingDefinition>(new Dictionary<string, BuildingDefinition>(buildings ?? throw new ArgumentNullException(nameof(buildings)), StringComparer.Ordinal));
            Worksites = new ReadOnlyDictionary<string, WorksiteDefinition>(new Dictionary<string, WorksiteDefinition>(worksites ?? throw new ArgumentNullException(nameof(worksites)), StringComparer.Ordinal));
        }
    }
}
