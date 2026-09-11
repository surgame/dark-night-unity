using System;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Core.Config;
using DarkNights.Runtime.Network;
using DarkNights.View;
using UnityEngine;
using YY.Features.Players.View;

namespace DarkNights.Entry
{
    /// <summary>
    /// 经同一定义工厂创建客户端建造幽灵，用公共占地计算和冻结库存显示有效颜色。
    /// 异步创建受本地模式、连接代次和 epoch 约束；取消、换世界或退出时释放，点击支付仍走权威命令。
    /// </summary>
    public sealed class SessionPlacementView : MonoBehaviour
    {
        private SessionClient client;
        private SessionEntityViews entities;
        private CampInput input;
        private PinewatchStage stage;
        private GameCatalog catalog;
        private LevelLayout layout;
        private ObjectView owner;
        private NativeVisual visual;
        private string kind = "";
        private int generation, epoch;
        private long connection;
        public bool Valid { get; private set; }

        public void Initialize(SessionClient value, SessionEntityViews views, CampInput controls,
            PinewatchStage scene, GameCatalog rules, LevelLayout level)
        {
            client = value; entities = views; input = controls; stage = scene; catalog = rules; layout = level;
        }

        private void Update()
        {
            if (client == null) return;
            var frame = client.Replica.Current;
            string requested = client.Ready && frame != null ? input.BuildKind : "";
            if (requested != kind || connection != client.ConnectionGeneration || epoch != (frame?.Epoch ?? 0))
            {
                Clear(); kind = requested; connection = client.ConnectionGeneration; epoch = frame?.Epoch ?? 0;
                if (kind.Length > 0) Create(kind, generation).Forget();
            }
            if (kind.Length == 0 || frame == null) return;
            float x = PlacementGeometry.Snap(stage.SceneCamera.ScreenToWorldPoint(input.Pointer).x * 100);
            BuildingDefinition definition = catalog.Balance.Buildings[kind];
            var world = frame.World;
            Valid = (!frame.HostOnly || client.PlayerSlot == 0) && world.Camp.Mode == "Playing" &&
                PlacementGeometry.Within(x, definition.Width, layout.BuildMinX, layout.BuildMaxX) &&
                !world.Buildings.Any(b => PlacementGeometry.BuildingOverlap(x, definition.Width, b.X, catalog.Balance.Buildings[b.Kind].Width)) &&
                !world.Worksites.Any(w => w.Amount != 0 && w.FarmId == 0 &&
                    PlacementGeometry.WorksiteOverlap(x, definition.Width, w.X, catalog.Balance.Worksites[w.Kind].Width)) &&
                GameText.ResourceIds.All(id => world.Camp.Stock.Get(id) >= definition.Cost.Get(id));
            input.PresentPlacement(x, Valid);
            if (visual != null)
            {
                visual.transform.position = new Vector3(x / 100, 0, 0);
                visual.Ambient = stage.Ambient;
                visual.PreviewTint(Valid ? new Color(.69f, .91f, .65f, .68f) : new Color(1, .35f, .28f, .65f));
            }
        }

        private async UniTask Create(string requested, int captured)
        {
            ObjectView created = null;
            try
            {
                created = await entities.CreateVisual(requested);
                if (this == null || captured != generation || kind != input.BuildKind ||
                    client.ConnectionGeneration != connection || client.Replica.Current?.Epoch != epoch)
                {
                    if (created != null) Destroy(created.gameObject);
                    return;
                }
                if (created == null) throw new InvalidOperationException("Cannot create placement visual: " + requested);
                owner = created;
                visual = owner.Get<NativeVisual>("visual");
                if (visual == null) throw new InvalidOperationException("Missing placement visual binding: " + requested);
            }
            catch (Exception error)
            {
                if (created != null) Destroy(created.gameObject);
                Debug.LogException(error);
            }
        }

        private void Clear()
        {
            generation++;
            if (owner != null) Destroy(owner.gameObject);
            owner = null; visual = null; Valid = false;
        }
        private void OnDestroy() { Clear(); }
    }
}
