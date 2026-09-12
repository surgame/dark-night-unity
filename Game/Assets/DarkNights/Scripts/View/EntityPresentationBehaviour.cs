using System;
using GameCore.Objects.Behaviours;
using GameCore.Objects.Views;
using UnityEngine;

namespace DarkNights.View
{
    /// <summary>
    /// 本地个体表现的有限生命周期：由框架绑定原生外观，由 Entry 显式绑定当前世界身份和输入回调。
    /// 未绑定的预览与残骸保持被动；解绑、离场和复用均清除副本、身份和回调，不拥有权威状态。
    /// </summary>
    public abstract partial class EntityPresentationBehaviour : PooledBehaviour
    {
        [ViewComponent("visual")] private NativeVisual visual;
        private Action<InputIntent> submit;
        public NativeVisual Visual => visual;
        public int Id { get; private set; }
        public int Epoch { get; private set; }
        public string Kind { get; private set; } = "";
        public bool IsBound => Id > 0 && Owner != null && visual != null;
        public abstract bool IsAvailable { get; }

        protected void BindEntity(int id, int epoch, string kind, Action<InputIntent> callback)
        {
            if (Owner == null || visual == null) throw new InvalidOperationException("Entity visual binding is not assembled.");
            if (id <= 0 || epoch <= 0 || string.IsNullOrWhiteSpace(kind)) throw new ArgumentException("Invalid entity identity.");
            Unbind();
            Id = id;
            Epoch = epoch;
            Kind = kind;
            submit = callback;
        }

        public void Unbind()
        {
            submit = null;
            Id = 0;
            Epoch = 0;
            Kind = "";
            ClearState();
        }

        protected bool Accept(int id, int epoch, string kind) =>
            IsBound && id == Id && epoch == Epoch && kind == Kind;

        protected bool Submit(InputIntent intent)
        {
            if (!IsAvailable || submit == null) return false;
            submit(intent);
            return true;
        }

        protected void Position(float x, Color ambient)
        {
            visual.transform.position = new Vector3(x / NativeVisual.PixelsPerUnit, 0, 0);
            visual.Ambient = ambient;
        }

        protected abstract void ClearState();
        protected override void OnSpawn()
        {
            Unbind();
            if (visual == null) throw new InvalidOperationException("Missing generated visual binding: " + GetType().Name);
            base.OnSpawn();
        }

        public override void OnDespawn()
        {
            Unbind();
            visual = null;
            base.OnDespawn();
        }
    }
}
