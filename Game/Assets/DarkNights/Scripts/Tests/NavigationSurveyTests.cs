using System;
using System.Linq;
using DarkNights.Core.Config.Expedition;
using DarkNights.Core.ViewData;
using DarkNights.Runtime.Network;
using DarkNights.Runtime.Session;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>
    /// 勘测位图、深度冻结和坐标请求身份的定向合同检查；不启动Play，不代替真实Host／Client验收。
    /// 覆盖未知节点不提前出现、长航迹保留、规范编码拒绝及Y坐标参与去重。
    /// </summary>
    public sealed class NavigationSurveyTests
    {
        private static NavigationDefinition Catalog() => new NavigationDefinition(8, 3,
            new[] { new NavigationNodeDefinition("home", "起始星", 0, 0, 1, "示意", false),
                new NavigationNodeDefinition("hidden", "隐藏星", 90, 10, 2, "示意", true) });

        [Test]
        public void OnlyInitialPlanetIsKnownAndRevealDoesNotMutateOldSnapshot()
        {
            var definition = Catalog(); var initial = NavigationSurveyData.Initial(definition, 0, 0);
            Assert.That(definition.Nodes.Count(n => initial.IsRevealed(n.X, n.Y)), Is.EqualTo(1));
            var visited = initial.Reveal(0, 0, 100, 10);
            Assert.That(visited.IsRevealed(90, 10), Is.True);
            Assert.That(initial.IsRevealed(90, 10), Is.False);
            Assert.That(visited.IsRevealed(45, 5), Is.True);
            var returned = visited.Reveal(100, 10, 0, 0);
            Assert.That(returned.IsRevealed(90, 10), Is.True);
        }

        [Test]
        public void WireFreezesSurveyAndCoordinateGoalTogether()
        {
            var definition = Catalog(); var survey = NavigationSurveyData.Initial(definition, 0, 0);
            var value = new JourneyNavigationData(definition, 0, 0, survey: survey,
                coordinateFlight: true, targetX: 90, targetY: 10);
            var wire = NavigationWire.From(value); var frozen = wire.Freeze();
            wire.SurveyBits = new string('x', NavigationSurveyData.EncodedLength);
            Assert.That(frozen.Survey.Encoded, Is.EqualTo(survey.Encoded));
            Assert.That(frozen.CoordinateFlight, Is.True);
            Assert.That(frozen.TargetX, Is.EqualTo(90)); Assert.That(frozen.TargetY, Is.EqualTo(10));
        }

        [Test]
        public void InvalidSurveyEncodingAndInactiveGoalAreRejected()
        {
            Assert.Throws<ArgumentException>(() => new NavigationSurveyData(""));
            Assert.Throws<ArgumentException>(() => new NavigationSurveyData(new string('!', NavigationSurveyData.EncodedLength)));
            Assert.Throws<ArgumentException>(() => new JourneyNavigationData(Catalog(), 0, 0, targetY: 1));
            var survey = NavigationSurveyData.Initial(Catalog(), 0, 0);
            Assert.That(survey.IsRevealed(float.NaN, 0), Is.False);
            Assert.That(survey.IsRevealed(5000, 0), Is.False);
        }

        [Test]
        public void GoalYSurvivesFreezeAndPoolReset()
        {
            var command = new SessionCommand { Operation = SessionOperation.SelectNavigationPoint,
                RequestSequence = 1, Kind = "", ActorIds = new[] { 1 }, X = 70, Y = -15, Value = 2, ControlLease = 1 };
            var frozen = command.Freeze(); command.OnReturnToPool();
            Assert.That(frozen.Y, Is.EqualTo(-15)); Assert.That(command.Y, Is.EqualTo(0));
            var other = new SessionRequest(frozen.Operation, frozen.Protocol, frozen.Epoch, frozen.PolicyRevision,
                frozen.Sequence, frozen.ActorIds, frozen.TargetId, frozen.X, frozen.Kind, frozen.Value, frozen.ControlLease, 15);
            var compare = typeof(SessionRequest).GetMethod("SameIntent", System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic);
            Assert.That((bool)compare.Invoke(frozen, new object[] { other }), Is.False);
        }
    }
}
