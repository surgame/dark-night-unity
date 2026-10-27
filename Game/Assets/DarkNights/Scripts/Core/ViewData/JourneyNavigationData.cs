using System;
using DarkNights.Core.Config.Expedition;

namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 航程状态的深度冻结导航投影；目录不可变，位置和上一趟结算摘要均来自所属YYGC状态。
    /// 客户端可据此显示距离和ETA，不据倒计时推进阶段或结算收入。
    /// </summary>
    public sealed class JourneyNavigationData
    {
        public NavigationDefinition Definition { get; }
        public float X { get; }
        public float Y { get; }
        public string RunId { get; }
        public string SettledRunId { get; }
        public int LastIncome { get; }
        public NavigationSurveyData Survey { get; }
        public bool CoordinateFlight { get; }
        public float TargetX { get; }
        public float TargetY { get; }

        public JourneyNavigationData(NavigationDefinition definition, float x, float y,
            string runId = "", string settledRunId = "", int lastIncome = 0,
            NavigationSurveyData survey = null, bool coordinateFlight = false, float targetX = 0, float targetY = 0)
        {
            Definition = definition ?? throw new ArgumentNullException(nameof(definition));
            NavigationNodeDefinition.ValidateCoordinate(x); NavigationNodeDefinition.ValidateCoordinate(y);
            NavigationNodeDefinition.ValidateCoordinate(targetX); NavigationNodeDefinition.ValidateCoordinate(targetY);
            if (!coordinateFlight && (targetX != 0 || targetY != 0))
                throw new ArgumentException("非坐标航程仍携带坐标目标。");
            if (!Identity(runId) || !Identity(settledRunId) || lastIncome < 0 || lastIncome > 10000000 ||
                lastIncome != 0 && settledRunId.Length == 0)
                throw new ArgumentException("远征结算身份或收入无效。");
            X = x; Y = y; RunId = runId; SettledRunId = settledRunId; LastIncome = lastIncome;
            Survey = survey ?? NavigationSurveyData.Initial(definition, x, y);
            CoordinateFlight = coordinateFlight; TargetX = targetX; TargetY = targetY;
        }

        private static bool Identity(string id) => id != null && (id.Length == 0 ||
            Guid.TryParseExact(id, "N", out var parsed) && parsed != Guid.Empty);
    }
}
