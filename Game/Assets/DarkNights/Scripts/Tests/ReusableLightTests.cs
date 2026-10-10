using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using DarkNights.Runtime.Objects;
using DarkNights.Runtime.Session;
using DarkNights.View.Lighting;
using GameCore.Objects.Definition;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Rendering;
using static DarkNights.Tests.ShipScenario;

namespace DarkNights.Tests
{
    /// <summary>可复用照明及库存合同的真实回归；检查目标隔离、不同挂点、关闭清理和可信事务，不替代多人画面验收。</summary>
    public sealed class ReusableLightTests
    {
        [Test]
        public void ActualSpriteShaderLightsBoundTargetAndRestoresWhenOff()
        {
            var root = new GameObject("isolated local fill render");
            var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false, true);
            texture.SetPixels(Enumerable.Repeat(Color.white, 16).ToArray()); texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), new Vector2(.5f, .5f), 4);
            var material = new Material(AssetDatabase.LoadAssetAtPath<Shader>("Assets/DarkNights/Res/Shared/Lighting/CampSprite.shader"));
            var target = new RenderTexture(128, 64, 0, RenderTextureFormat.ARGBHalf, RenderTextureReadWrite.Linear);
            var pixels = new Texture2D(128, 64, TextureFormat.RGBA32, false, true);
            var composer = new LightFillComposer();
            float active = Shader.GetGlobalFloat("_DNLightingActive"), ambient = Shader.GetGlobalFloat("_DNLightingAmbient");
            Vector4 known = Shader.GetGlobalVector("_DNKnownBounds");
            RenderTexture previous = RenderTexture.active;
            try
            {
                var camera = root.AddComponent<Camera>(); camera.enabled = false;
                camera.orthographic = true; camera.orthographicSize = 1.5f; camera.aspect = 2;
                camera.transform.position = new Vector3(0, 0, -10); camera.cullingMask = 1 << 31;
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                SpriteRenderer Receiver(string name, float x)
                {
                    var child = new GameObject(name); child.layer = 31; child.transform.SetParent(root.transform, false);
                    child.transform.position = new Vector3(x, 0, 0);
                    var renderer = child.AddComponent<SpriteRenderer>(); renderer.sprite = sprite; renderer.sharedMaterial = material;
                    return renderer;
                }
                var lit = Receiver("bound", -.75f); Receiver("unbound", .75f);
                var effectRoot = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Editor.ReusableLightContentSetup.EffectPath), root.transform);
                var effect = effectRoot.GetComponent<LightEffect>();
                effect.Apply(AssetDatabase.LoadAssetAtPath<LightProfile>(Editor.Lighting.LightProfileMigration.FlashlightPath).Freeze());
                effect.Bind(camera, lit.transform, lit.transform, new[] { lit }); effect.SetOn(true);
                Shader.SetGlobalFloat("_DNLightingActive", 1); Shader.SetGlobalFloat("_DNLightingAmbient", .08f);
                Shader.SetGlobalVector("_DNKnownBounds", Vector4.zero);
                Color Read(int x)
                {
                    RenderPipeline.SubmitRenderRequest(camera, new RenderPipeline.StandardRequest { destination = target });
                    RenderTexture.active = target; pixels.ReadPixels(new Rect(0, 0, 128, 64), 0, 0); pixels.Apply();
                    return pixels.GetPixel(x, 32);
                }
                composer.Apply(new[] { effect }, 1);
                Color illuminated = Read(48), unbound = pixels.GetPixel(80, 32);
                Assert.That(illuminated.r, Is.GreaterThan(unbound.r + .15f));
                System.IO.File.WriteAllBytes("../artifacts/light-profile-refactor-20261009/local-fill-on.png", pixels.EncodeToPNG());
                effect.SetOn(false); composer.Apply(new[] { effect }, 1);
                Color off = Read(48);
                Assert.That(off.r, Is.EqualTo(unbound.r).Within(.02));
                System.IO.File.WriteAllBytes("../artifacts/light-profile-refactor-20261009/local-fill-off.png", pixels.EncodeToPNG());
            }
            finally
            {
                composer.Clear(); Shader.SetGlobalFloat("_DNLightingActive", active); Shader.SetGlobalFloat("_DNLightingAmbient", ambient);
                Shader.SetGlobalVector("_DNKnownBounds", known); RenderTexture.active = previous;
                UnityEngine.Object.DestroyImmediate(root); target.Release(); UnityEngine.Object.DestroyImmediate(target);
                UnityEngine.Object.DestroyImmediate(pixels); UnityEngine.Object.DestroyImmediate(sprite);
                UnityEngine.Object.DestroyImmediate(texture); UnityEngine.Object.DestroyImmediate(material);
            }
        }

        [Test]
        public void SharedEffectAndHeadVariantHaveCompleteBindings()
        {
            Editor.ReusableLightContentSetup.Validate();
            var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/DarkNights/Res/Shared/Lighting/CampSprite.shader");
            Assert.That(ShaderUtil.GetShaderMessages(shader).Where(message => message.severity.ToString() == "Error"), Is.Empty);
        }

        [Test]
        public void HeadMountMovesAndOnlyBoundSpritesReceiveLocalFill()
        {
            var head = new GameObject("head mount probe");
            var spare = new GameObject("unbound receiver");
            var cameraRoot = new GameObject("light context");
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Editor.ReusableLightContentSetup.HeadPath);
            var child = UnityEngine.Object.Instantiate(prefab, head.transform);
            var composer = new LightFillComposer();
            try
            {
                var receiver = head.AddComponent<SpriteRenderer>(); var other = spare.AddComponent<SpriteRenderer>();
                var camera = cameraRoot.AddComponent<Camera>(); var effect = child.GetComponent<LightEffect>();
                effect.Apply(AssetDatabase.LoadAssetAtPath<LightProfile>(Editor.Lighting.LightProfileMigration.FlashlightPath).Freeze());
                effect.Bind(camera, head.transform, head.transform, new[] { receiver }); effect.SetOn(true);
                head.transform.position = new Vector3(3, 4, 0); head.transform.rotation = Quaternion.Euler(0, 0, 90);
                var source = effect.Environment.Sample(null);
                Assert.That(source.Position, Is.EqualTo(effect.Environment.Emitter.position));
                Assert.That(source.Angle, Is.EqualTo(90).Within(.001));
                head.transform.localScale = new Vector3(-1, 1, 1);
                Assert.That(effect.Environment.Sample(null).Angle, Is.EqualTo(-90).Within(.001), "头部挂点翻转必须翻转灯光。");
                Assert.That(source.Rules.NearIntensity, Is.Zero, "角色补光不能重复写入环境光场。");
                var sources = new List<LightEffect>(); LightEffect.Collect(camera, camera, sources);
                Assert.That(sources, Does.Contain(effect));
                var authored = receiver.color;
                composer.Apply(sources, 1);
                var properties = new MaterialPropertyBlock(); receiver.GetPropertyBlock(properties);
                Assert.That(properties.GetFloat("_DNLocalFillCount"), Is.EqualTo(1));
                other.GetPropertyBlock(properties); Assert.That(properties.GetFloat("_DNLocalFillCount"), Is.Zero);
                Assert.That(receiver.color, Is.EqualTo(authored));
                composer.Apply(sources, 1, new Vector2(.08f, .12f)); receiver.GetPropertyBlock(properties);
                var radius = properties.GetVectorArray("_DNLocalFillOrigins")[0];
                Assert.That(radius.z, Is.EqualTo(.176f).Within(.001));
                Assert.That(radius.w, Is.EqualTo(.264f).Within(.001));
                effect.LocalFill.enabled = false; composer.Apply(sources, 1);
                receiver.GetPropertyBlock(properties); Assert.That(properties.GetFloat("_DNLocalFillCount"), Is.Zero);
                Assert.That(effect.Environment.Sample(null).Rules.Intensity, Is.GreaterThan(0));
                effect.LocalFill.enabled = true; effect.SetOn(false); composer.Apply(sources, 1);
                receiver.GetPropertyBlock(properties); Assert.That(properties.GetFloat("_DNLocalFillCount"), Is.Zero);
                child.SetActive(false); LightEffect.Collect(camera, camera, sources); Assert.That(sources, Has.No.Member(effect));
            }
            finally
            {
                composer.Clear(); UnityEngine.Object.DestroyImmediate(head);
                UnityEngine.Object.DestroyImmediate(spare); UnityEngine.Object.DestroyImmediate(cameraRoot);
            }
        }

        [UnityTest]
        public IEnumerator InventoryRemoveRestoreAndReclaimCannotRecreateFlashlight() => UniTask.ToCoroutine(async () =>
        {
            using var scope = await UnifiedSessionScope.Create();
            var world = await GroundSessionTests.Create(scope);
            using var authority = new SessionAuthority(world);
            var host = Connect(authority, 0); var hero = Hero(world, 0);
            var light = ObjectDefinitionDatabase.Instance.GetDefinitionByKey("item.flashlight");
            string guid = light.Guid.ToString();
            Assert.That(hero.CaptureState().Slot0, Is.EqualTo(guid));
            long sequence = 20000;
            SessionResultCode Send(SessionOperation operation, int value = 0, string kind = "", SessionConnection sender = null)
            {
                var state = hero.CaptureState();
                return JourneyScenario.Execute(authority, sender ?? host, new SessionRequest(operation, SessionAuthority.ProtocolVersion,
                    authority.Epoch, authority.PolicyRevision, ++sequence,
                    operation == SessionOperation.DebugSetEnabled ? Array.Empty<int>() : new[] { hero.Id }, kind: kind,
                    value: value, x: operation == SessionOperation.DebugGiveEquipment ? 1 : 0,
                    controlLease: operation == SessionOperation.SetHeroLight ? state.ControlLease : 0));
            }
            Assert.That(Send(SessionOperation.DebugSetEnabled, 1), Is.EqualTo(SessionResultCode.Applied));
            Assert.That(Send(SessionOperation.DebugGiveEquipment, hero.CaptureState().InventoryRevision, guid), Is.EqualTo(SessionResultCode.NoEffect));
            Assert.That(Send(SessionOperation.DebugRemoveEquipment, hero.CaptureState().InventoryRevision, guid), Is.EqualTo(SessionResultCode.Applied));
            Assert.That(hero.CaptureState().Slot0, Is.Empty); Assert.That(hero.CaptureState().LightDefinition, Is.Empty);
            Assert.That(Send(SessionOperation.SetHeroLight, 1), Is.EqualTo(SessionResultCode.NoEffect));
            string saved = world.SaveCodec.Serialize(world.CaptureWorld()); world.Restore(saved);
            Assert.That(authority.AcknowledgeReady(host, authority.Epoch, authority.Revision, true), Is.True);
            hero = Hero(world, 0); Assert.That(hero.CaptureState().LightDefinition, Is.Empty);
            Assert.That(Send(SessionOperation.DebugSetEnabled, 1), Is.EqualTo(SessionResultCode.Applied));
            var guest = Connect(authority, 1);
            Assert.That(Send(SessionOperation.DebugGiveEquipment, hero.CaptureState().InventoryRevision, guid, guest), Is.Not.EqualTo(SessionResultCode.Applied));
            Assert.That(Send(SessionOperation.DebugGiveEquipment, hero.CaptureState().InventoryRevision, guid), Is.EqualTo(SessionResultCode.Applied));
            var projected = authority.CaptureProjection(); var codec = new Runtime.Network.ProjectionCodec(world.Catalog, world.Layout);
            var actor = codec.Decode(codec.Encode(projected)).World.Actors.Single(value => value.Id == hero.Id);
            Assert.That(actor.Slot0Definition, Is.EqualTo(guid)); Assert.That(actor.LightEnabled, Is.True);
            string withLight = world.SaveCodec.Serialize(world.CaptureWorld());
            var invalid = JObject.Parse(withLight);
            invalid["world"]["actors"].OfType<JObject>().Single(value => (int)value["id"] == hero.Id)["slot_0"] = "";
            Assert.Throws<FormatException>(() => world.SaveCodec.Parse(invalid.ToString()));
        });
    }
}
