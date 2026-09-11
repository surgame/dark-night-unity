using System;
using System.Collections.Generic;
using System.Linq;
using DarkNights.Core.Config;
using DarkNights.Core.Save;

namespace DarkNights.Core.Save
{
    /// <summary>
    /// 一次存档验证的局部索引，收集已确认唯一的实体与训练归属。
    /// 不修改快照或当前游戏，用于跨对象关系检查，验证结束即可释放。
    /// </summary>
    internal sealed class ValidationContext
    {
        public SessionSnapshot Saved { get; }
        public GameCatalog Catalog { get; }
        public Dictionary<int, ActorSnapshot> Actors { get; }
        public Dictionary<int, BuildingSnapshot> Buildings { get; }
        public Dictionary<int, WorksiteSnapshot> Sites { get; }
        public Dictionary<int, int> TrainingOwners { get; } = new Dictionary<int, int>();

        public LevelLayout Layout { get; }

        public ValidationContext(SessionSnapshot saved, GameCatalog catalog, LevelLayout layout)
        {
            Saved = saved;
            Catalog = catalog;
            Layout = layout;
            Actors = saved.Actors.ToDictionary(a => a.Id);
            Buildings = saved.Buildings.ToDictionary(a => a.Id);
            Sites = saved.Worksites.ToDictionary(a => a.Id);
        }

        public bool Has(int id) => Actors.ContainsKey(id) || Buildings.ContainsKey(id) || Sites.ContainsKey(id);

        public static bool Number(double value, double minimum, double maximum) => !double.IsNaN(value) && !double.IsInfinity(value) && value >= minimum && value <= maximum;

        public static bool Id(int id, int minimum = 0) => id >= minimum && id <= 1000000;
    }
}
