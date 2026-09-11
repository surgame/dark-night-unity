using System;
using System.Globalization;
using DarkNights.Core.Logic;
using Newtonsoft.Json.Linq;

namespace DarkNights.Tests
{
    /// <summary>
    /// 对照独立 Godot 4.7.2 进程冻结的四种 seed、512 次交错整数和浮点采样及每一步状态。
    /// 验证整型全范围、负数区间与恢复重放，不能用本实现重新生成期望向量。
    /// </summary>
    public static class RandomCompatibilityScenarios
    {
        public static void Run(Action<bool, string> check)
        {
            foreach (JObject sequence in RuleScenario.Fixture("godot-rng-vectors.json")["sequences"])
            {
                var random = new SimulationRandom { Seed = Bits((string)sequence["seed"]) };
                check(random.State == Bits((string)sequence["seeded_state"]), "RNG seeded state: " + sequence["seed"]);
                int index = 0;
                foreach (JObject sample in sequence["samples"])
                {
                    ulong previous = random.State;
                    int from = index % 7 == 0 ? int.MinValue : -3;
                    int to = index % 7 == 0 ? int.MaxValue : 10;
                    int integer = random.RandiRange(from, to);
                    float floating = random.RandfRange(-5, 5);
                    check(integer == (int)sample["integer"] && floating == (float)sample["float"] &&
                        random.State == Bits((string)sample["state"]),
                        "Godot RNG exact integer/float/state: " + sequence["seed"] + "/" + index);
                    random.State = previous;
                    check(random.RandiRange(from, to) == integer && random.RandfRange(-5, 5) == floating,
                        "RNG restored state replay: " + sequence["seed"] + "/" + index);
                    index++;
                }
                ulong unchanged = random.State;
                check(random.RandiRange(5, 5) == 5 && random.State == unchanged, "Equal integer bounds consume no randomness");
            }
        }

        private static ulong Bits(string value) => unchecked((ulong)long.Parse(value, CultureInfo.InvariantCulture));
    }
}
