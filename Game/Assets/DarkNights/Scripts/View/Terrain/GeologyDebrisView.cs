using System;
using UnityEngine;

namespace DarkNights.View.Terrain
{
    /// <summary>原生尘雾和石粒的有界被动表现；只用冻结模拟时间手动推进，不消费业务随机数或参与碰撞。实例停用时清空，暂停和恢复不会补发撞击。</summary>
    public sealed class GeologyDebrisView : MonoBehaviour
    {
        public ParticleSystem Dust, Chips;
        public float GroundY { get; set; }
        private readonly ParticleSystem.Particle[] chipBuffer = new ParticleSystem.Particle[256];
        private double previousTime = -1;
        public int ImpactCount { get; private set; }

        public void ResetEffects()
        {
            if (Dust == null || Chips == null) throw new InvalidOperationException("地质粒子缺少原生绑定。");
            Dust.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            Chips.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            previousTime = -1; ImpactCount = 0;
        }

        public void Advance(double simulationTime)
        {
            if (previousTime >= 0)
            {
                float delta = (float)Math.Max(0, Math.Min(.25, simulationTime - previousTime));
                if (delta > 0)
                {
                    Dust.Simulate(delta, false, false, false); Chips.Simulate(delta, false, false, false);
                    int count = Chips.GetParticles(chipBuffer);
                    for (int i = 0; i < count; i++)
                    {
                        if (chipBuffer[i].position.y >= GroundY) continue;
                        var position = chipBuffer[i].position; position.y = GroundY; chipBuffer[i].position = position;
                        var velocity = chipBuffer[i].velocity;
                        chipBuffer[i].velocity = new Vector3(velocity.x * .55f, Math.Abs(velocity.y) * .23f, 0);
                        chipBuffer[i].remainingLifetime = Math.Min(chipBuffer[i].remainingLifetime, .3f);
                    }
                    Chips.SetParticles(chipBuffer, count);
                }
            }
            previousTime = simulationTime;
        }

        public void Shed(Vector3 position)
        {
            Emit(Dust, position, 3, .055f, .35f, .07f);
            Emit(Chips, position, 2, .025f, .15f, -.08f);
        }

        public void Impact(Vector3 position, float size = 16)
        {
            ImpactCount++;
            bool heavy = size >= 18;
            Emit(Dust, position, heavy ? 8 : 3, heavy ? .15f : .09f, .65f, .18f);
            Emit(Chips, position, heavy ? 6 : 2, .025f, .5f, .6f);
        }

        private static void Emit(ParticleSystem system, Vector3 position, int count, float size, float speed, float lift)
        {
            for (int i = 0; i < count; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                float fraction = (i * 7 % 11 + 2) / 12f;
                var particle = new ParticleSystem.EmitParams
                {
                    position = position + Vector3.right * side * .025f,
                    velocity = new Vector3(side * speed * fraction, lift * (.45f + fraction), 0),
                    startSize = size * (.65f + fraction * .5f), startLifetime = .55f + fraction * .9f,
                    rotation = i * 37
                };
                system.Emit(particle, 1);
            }
        }
    }
}
