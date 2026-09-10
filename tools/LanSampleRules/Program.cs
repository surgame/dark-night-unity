using System;
using DarkNights.Samples.LanCoop.Core;

namespace DarkNights.Samples.LanCoop.Tests
{
    /// <summary>直接链接真实纯规则源码的冻结断言；补足坏序号和 Ready 边界，不替代 Unity 多进程验证。</summary>
    internal static class Program
    {
        private static int checks;
        private static void Equal<T>(T expected, T actual)
        {
            if (!Equals(expected, actual)) throw new Exception($"Expected {expected}, got {actual}");
            checks++;
        }
        private static void Main()
        {
            var world = new CampSession();
            Equal("NotReady", world.Apply(1, true, 1, 1, 1, SampleOperation.Buy, 1));
            Equal("BadSequence", world.Apply(1, true, 0, 1, 1, SampleOperation.Ready, 1));
            Equal("ProtocolMismatch", world.Apply(1, true, 2, 9, 1, SampleOperation.Ready, 1));
            Equal("Ready", world.Apply(1, true, 2, 1, 1, SampleOperation.Ready, 1));
            Equal("Ready", world.Apply(2, false, 1, 1, 1, SampleOperation.Ready, 1));
            Equal("InvalidEntity", world.Apply(1, true, 3, 1, 1, SampleOperation.Buy, 99));
            var frozen = world.Snapshot();
            Equal("Accepted", world.Apply(1, true, 4, 1, 1, SampleOperation.Buy, 1));
            Equal("DuplicateOrExpired", world.Apply(1, true, 4, 1, 1, SampleOperation.Buy, 1));
            Equal("InsufficientFunds", world.Apply(2, false, 2, 1, 1, SampleOperation.Buy, 1));
            Equal(10, frozen.Coins);
            Equal(0, world.Snapshot().Coins);
            Equal(1, world.Snapshot().Purchases);
            Equal("Accepted", world.Apply(2, false, 3, 1, 1, SampleOperation.Claim, 1));
            Equal("Occupied", world.Apply(1, true, 5, 1, 1, SampleOperation.Claim, 1));
            Equal("NotOccupant", world.Apply(1, true, 6, 1, 1, SampleOperation.Release, 1));
            world.RemovePlayer(2);
            Equal(-1, world.Snapshot().Occupant);
            world.Step();
            Equal(0, world.Snapshot().SimulationTicks);
            Equal("HostOnly", world.Apply(1, false, 7, 1, 1, SampleOperation.Pause, 1));
            Equal("Accepted", world.Apply(1, true, 8, 1, 1, SampleOperation.Pause, 1));
            world.Step();
            Equal(1, world.Snapshot().SimulationTicks);
            Equal("Accepted", world.Apply(1, true, 9, 1, 1, SampleOperation.Reset, 1));
            Equal("StaleEpoch", world.Apply(1, true, 10, 1, 1, SampleOperation.Buy, 1));
            Equal("NotReady", world.Apply(1, true, 10, 1, 2, SampleOperation.Buy, 1));
            Equal(2, world.Snapshot().Epoch);
            Equal(10, world.Snapshot().Coins);
            Console.WriteLine($"PASS: {checks} assertions against actual CampSession source (C# 9)");
        }
    }
}
