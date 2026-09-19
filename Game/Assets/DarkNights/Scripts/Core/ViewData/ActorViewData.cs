namespace DarkNights.Core.ViewData
{
    /// <summary>
    /// 单位的一次冻结展示值，保留外观、生命、任务与动画采样；不携带 AI 决策计时、随机数或可写实体引用。
    /// </summary>
    public sealed class ActorViewData
    {
        public int Id { get; }
        public string Kind { get; }
        public string Name { get; }
        public bool Enemy { get; }
        public float X { get; }
        public double Hp { get; }
        public string Activity { get; }
        public int TargetId { get; }
        public float Face { get; }
        public bool Walking { get; }
        public double ActionTime { get; }
        public double Windup { get; }
        public double HitFlash { get; }
        public float Height { get; }
        public float VerticalSpeed { get; }
        public int SupportPlatform { get; }
        public bool ManualControl { get; }
        public int SelectedItem { get; }
        public int SelectionRevision { get; }
        public bool JetpackEquipped { get; }
        public double JetpackFuel { get; }
        public int ExplosiveCharges { get; }
        public int ControllerSlot { get; }
        public int ControlLease { get; }

        public ActorViewData(
            int id,
            string kind,
            string name,
            bool enemy,
            float x,
            double hp,
            string activity,
            int targetId,
            float face,
            bool walking,
            double actionTime,
            double windup,
            double hitFlash,
            float height = 0,
            float verticalSpeed = 0,
            int supportPlatform = 0,
            bool manualControl = false,
            int selectedItem = 0,
            int selectionRevision = 0,
            bool jetpackEquipped = false,
            double jetpackFuel = 0,
            int controllerSlot = -1,
            int controlLease = 0,
            int explosiveCharges = 0)
        {
            Id = id;
            Kind = kind;
            Name = name;
            Enemy = enemy;
            X = x;
            Hp = hp;
            Activity = activity;
            TargetId = targetId;
            Face = face;
            Walking = walking;
            ActionTime = actionTime;
            Windup = windup;
            HitFlash = hitFlash;
            Height = height;
            VerticalSpeed = verticalSpeed;
            SupportPlatform = supportPlatform;
            ManualControl = manualControl;
            SelectedItem = selectedItem;
            SelectionRevision = selectionRevision;
            JetpackEquipped = jetpackEquipped;
            JetpackFuel = jetpackFuel;
            ControllerSlot = controllerSlot;
            ControlLease = controlLease;
            ExplosiveCharges = explosiveCharges;
        }
    }
}
