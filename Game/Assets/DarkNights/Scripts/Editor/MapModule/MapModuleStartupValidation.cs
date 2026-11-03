using System;
using System.Linq;
using DarkNights.Entry;
using DarkNights.Entry.MapModule;
using DarkNights.View;
using DarkNights.View.MapModule;
using FishNet.Managing;
using FishNet.Object;
using GameCore.Objects.Definition;
using GameCore.Objects.Runner;
using Runtime.AppStartup;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DarkNights.Editor.MapModule
{
    /// <summary>
    /// 工坊启动前只读核对正式Bootstrap和研究Rig的完整装配；不创建运行服务或重写作者资源。
    /// 使用隔离预览读取Bootstrap，研究Scene只拥有地图、角色和输入，服务由同一正式入口提供。
    /// </summary>
    internal static class MapModuleStartupValidation
    {
        internal static void Check(MapModuleStudyAuthoring study)
        {
            EnvironmentValidation.Validate();
            if (study == null) throw new InvalidOperationException("缺少MapModule研究Scene。");
            var scene = study.gameObject.scene;
            if (Components<AppStartup>(scene).Length != 0 || Components<NetworkManager>(scene).Length != 0)
                throw new InvalidOperationException("研究Scene不能另装AppStartup或NetworkManager；统一由Bootstrap提供。");
            var entry = Components<MapModuleEntry>(scene).Single();
            if (entry.Study != study || Components<PinewatchStage>(scene).Single() != entry.Stage ||
                Components<ScenePlacement>(scene).Single() != entry.HeroPlacement)
                throw new InvalidOperationException("研究Scene需显式绑定唯一Study、Stage与主角Placement。");
            CheckStage(entry, scene);
            CheckHero(entry.HeroPlacement, scene);
            var bootstrap = EditorSceneManager.OpenPreviewScene(EnvironmentValidation.ScenePath);
            try
            {
                var startup = Components<AppStartup>(bootstrap).Single();
                var settings = new SerializedObject(startup).FindProperty("_settings").objectReferenceValue as AppStartupSettings;
                if (settings != AssetDatabase.LoadAssetAtPath<AppStartupSettings>(EnvironmentValidation.StartupPath) ||
                    startup.OverrideSettingsModules)
                    throw new InvalidOperationException("Bootstrap需使用正式AppStartupSettings且不覆盖模块。");
                if (!settings.Modules.Any(m => m.Enabled && m.Required && m.Module is GameSessionStartupModule))
                    throw new InvalidOperationException("正式会话装配模块必须启用且为必需。");
                foreach (var module in settings.Modules.Where(m => m.Enabled && m.Module != null))
                    foreach (var dependency in module.Module.Dependencies ?? Array.Empty<Type>())
                        if (!settings.Modules.Any(m => m.Enabled && m.Module?.GetType() == dependency))
                            throw new InvalidOperationException(module.Module.Name + " 缺少启动依赖：" + dependency.Name);
                if (Components<AudioListener>(bootstrap).Count(l => l.enabled && l.gameObject.activeInHierarchy) != 1)
                    throw new InvalidOperationException("Bootstrap必须提供唯一启用的AudioListener。");
                CheckNetwork(Components<NetworkManager>(bootstrap).Single());
            }
            finally { EditorSceneManager.ClosePreviewScene(bootstrap); }
        }

        private static T[] Components<T>(Scene scene) where T : Component =>
            scene.GetRootGameObjects().SelectMany(root => root.GetComponentsInChildren<T>(true)).ToArray();

        private static void CheckStage(MapModuleEntry entry, Scene scene)
        {
            var stage = entry.Stage;
            if (stage == null || stage.gameObject.scene != scene || stage.SceneCamera == null ||
                stage.SceneCamera.gameObject.scene != scene || stage.Entities == null || stage.Entities.gameObject.scene != scene ||
                Components<UnityEngine.InputSystem.PlayerInput>(scene).Length != 1 ||
                Components<Camera>(scene).Single() != stage.SceneCamera)
                throw new InvalidOperationException("研究Stage缺少本Scene的唯一镜头、输入或运行时对象容器。");
            var serialized = new SerializedObject(stage);
            var sky = serialized.FindProperty("sky").objectReferenceValue as SpriteRenderer;
            if (sky == null || sky.gameObject.scene != scene)
                throw new InvalidOperationException("研究Stage缺少显式天空绑定。");
            var backgrounds = serialized.FindProperty("backgrounds");
            for (int i = 0; i < backgrounds.arraySize; i++)
                if (backgrounds.GetArrayElementAtIndex(i).objectReferenceValue == null)
                    throw new InvalidOperationException("研究Stage远景列表含空引用。");
        }

        private static void CheckHero(ScenePlacement hero, Scene scene)
        {
            if (hero == null || hero.Loader == null || hero.View == null || hero.gameObject.scene != scene ||
                hero.Loader.gameObject != hero.gameObject || hero.View.gameObject != hero.gameObject ||
                string.IsNullOrEmpty(hero.PlacementKey) || hero.Loader.ObjectInstance == null ||
                hero.Loader.ObjectInstance != hero.View.Owner)
                throw new InvalidOperationException("研究主角的Placement／Loader／ObjectInstance／View／Initializer绑定不完整。");
            var initializer = new SerializedObject(hero.Loader).FindProperty("_initializerComponent").objectReferenceValue;
            if (initializer == null || !ReferenceEquals(initializer, hero.View.Initializer))
                throw new InvalidOperationException("研究主角Loader与View必须显式指向同一初始化器。");
            var definition = hero.Loader.ResolveDefinition();
            if (definition == null || Runtime.Objects.ObjectSessionResources.Rule(definition) != "worker")
                throw new InvalidOperationException("研究主角必须绑定正式worker定义。");
            ObjectAssemblyValidation.Validate(definition, hero.View);
        }

        private static void CheckNetwork(NetworkManager manager)
        {
            if (!manager.gameObject.activeInHierarchy || !manager.enabled || manager.SpawnablePrefabs == null)
                throw new InvalidOperationException("Bootstrap缺少启用的NetworkManager或生成集合。");
            var database = AssetDatabase.LoadAssetAtPath<ObjectDefinitionDatabase>(
                "Assets/Addressables/Datas/GlobalSO/ObjectDefinitionDatabase.asset");
            foreach (string key in new[] { "session.pinewatch", "connection.pinewatch" })
            {
                var definition = database.Definitions.SingleOrDefault(d => d != null && d.Key == key);
                if (definition == null || !definition.isNetwork || definition.PrefabRef == null)
                    throw new InvalidOperationException("缺少正式网络对象定义：" + key);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(definition.PrefabRef.AssetGUID));
                var instance = prefab != null ? prefab.GetComponent<ObjectInstance>() : null;
                var view = instance != null ? instance.View as YY.Features.Players.View.ObjectView : null;
                if (view == null || view.Owner != instance || view.Initializer == null ||
                    !(view.Initializer is IObjectSessionInitializer initializer) || initializer.ObjectInstance != instance)
                    throw new InvalidOperationException("网络Prefab缺少原生实例／视图／会话初始化器：" + key);
                var networkObject = prefab.GetComponent<NetworkObject>();
                int matches = 0;
                for (int i = 0; i < manager.SpawnablePrefabs.GetObjectCount(); i++)
                    if (manager.SpawnablePrefabs.GetObject(true, i) == networkObject && networkObject != null) matches++;
                if (matches != 1) throw new InvalidOperationException("网络Prefab必须唯一登记于Bootstrap生成集合：" + key);
                ObjectAssemblyValidation.Validate(definition, view);
            }
        }
    }
}
