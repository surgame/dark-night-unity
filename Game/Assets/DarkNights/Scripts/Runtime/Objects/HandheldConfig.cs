using System;
using GameCore.Objects.Behaviours.Interfaces;

namespace DarkNights.Runtime.Objects
{
    /// <summary>
    /// 会话投射物能力的共享只读参数；像素、秒和伤害均由服务端读取，客户端不能提交数值。
    /// 挂在正式 CampSession 定义的 IConfigData 中；现有职业攻击和 balance 数值保持独立。
    /// </summary>
    [Serializable]
    public sealed class HandheldConfig : IConfigData
    {
        public string Name => "主角手持道具";
        public const int PoolCapacity = 128;
        public float BulletSpeed = 360;
        public float BulletRadius = 1;
        public float BulletLifetime = 1.5f;
        public int BulletDamage = 12;
        public float FireInterval = 0.22f;
        public float MuzzleDistance = 8;
        public float HandHeight = 9;
        public float PickaxeSeconds = 0.48f;
        public float BombMinimumSpeed = 65;
        public float BombMaximumSpeed = 180;
        public float BombLift = 65;
        public float BombGravity = 220;
        public float BombChargeSeconds = 1.2f;
        public float BombCooldown = 0.65f;
        public float BombFuseSeconds = 3;
        public float BombRadius = 2;
        public float ExplosionRadius = 36;
        public int BombDamage = 32;
        public float ExplosionSeconds = 0.3f;

        public string Fingerprint()
        {
            Validate();
            using var hash = System.Security.Cryptography.SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(System.Text.Encoding.UTF8.GetBytes(UnityEngine.JsonUtility.ToJson(this)))).Replace("-", "").ToLowerInvariant();
        }

        internal void Validate()
        {
            Positive(BulletSpeed, 1000); Positive(BulletRadius, 4); Positive(BulletLifetime, 5);
            Positive(FireInterval, 5); Positive(MuzzleDistance, 16); Positive(HandHeight, 20);
            Positive(PickaxeSeconds, 5); Positive(BombMinimumSpeed, 300); Positive(BombMaximumSpeed, 500);
            Positive(BombLift, 300); Positive(BombGravity, 500); Positive(BombChargeSeconds, 5);
            Positive(BombCooldown, 5); Positive(BombFuseSeconds, 8); Positive(BombRadius, 4);
            Positive(ExplosionRadius, 128); Positive(ExplosionSeconds, 1);
            if (BombMaximumSpeed < BombMinimumSpeed || BulletDamage < 1 || BulletDamage > 1000 || BombDamage < 1 || BombDamage > 1000)
                throw new InvalidOperationException("Invalid handheld damage or throw speed.");
        }

        private static void Positive(float value, float maximum)
        {
            if (float.IsNaN(value) || float.IsInfinity(value) || value <= 0 || value > maximum)
                throw new InvalidOperationException("Handheld configuration exceeds simulation bounds.");
        }
    }
}
