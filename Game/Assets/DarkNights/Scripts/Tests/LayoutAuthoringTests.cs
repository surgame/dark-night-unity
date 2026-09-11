using System.IO;
using System.Linq;
using DarkNights.Core.Logic;
using DarkNights.Editor;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace DarkNights.Tests
{
    /// <summary>
    /// 从已保存并重开的正式布局场景读取冻结配置，对照旧引擎证据及初始实体顺序；测试只读场景，不生成或覆盖人工资源。
    /// </summary>
    public sealed class LayoutAuthoringTests
    {
        [Test]
        public void PinewatchSceneMatchesFrozenLayout()
        {
            RuleScenario.RepositoryRoot = Path.GetFullPath("..");
            JObject expected = RuleScenario.Fixture("pinewatch-layout-v1.json");
            var layout = PinewatchLayoutSetup.Validate();
            Assert.That(layout.WorldWidth, Is.EqualTo((float)expected["world_width"]));
            Assert.That(layout.GroundY, Is.EqualTo((float)expected["ground_y"]));
            Assert.That(layout.BuildMinX, Is.EqualTo((float)expected["build_min_x"]));
            Assert.That(layout.BuildMaxX, Is.EqualTo((float)expected["build_max_x"]));
            Assert.That(layout.SpawnX, Is.EqualTo((float)expected["spawn_x"]));
            Assert.That(layout.CameraX, Is.EqualTo((float)expected["camera_x"]));
            Compare(expected, "buildings", layout.Buildings.Select(value => (value.Kind, value.X, value.Variant, value.Name)).ToArray());
            Compare(expected, "worksites", layout.Worksites.Select(value => (value.Kind, value.X, value.Variant, value.Name)).ToArray());
            Compare(expected, "actors", layout.Actors.Select(value => (value.Kind, value.X, value.Variant, value.Name)).ToArray());

            var session = new GameSession(RuleScenario.Catalog(), layout);
            Assert.That(session.World.Buildings.Select(value => value.Kind), Is.EqualTo(layout.Buildings.Select(value => value.Kind)));
            Assert.That(session.World.Actors.Select(value => value.Name), Is.EqualTo(layout.Actors.Select(value => value.Name)));
            Assert.That(session.World.NextId, Is.EqualTo(18), "The completed farm reserves its generated worksite identity.");
        }

        private static void Compare(JObject expected, string group, (string Kind, float X, int Variant, string Name)[] actual)
        {
            JArray values = (JArray)expected[group];
            Assert.That(actual.Length, Is.EqualTo(values.Count), group);
            for (int index = 0; index < values.Count; index++)
            {
                Assert.That(actual[index].Kind, Is.EqualTo((string)values[index]["kind"]), group + " kind " + index);
                Assert.That(actual[index].X, Is.EqualTo((float)values[index]["x"]), group + " x " + index);
                Assert.That(actual[index].Variant, Is.EqualTo((int?)values[index]["variant"] ?? 0), group + " variant " + index);
                Assert.That(actual[index].Name, Is.EqualTo((string)values[index]["name"] ?? ""), group + " name " + index);
            }
        }
    }
}
