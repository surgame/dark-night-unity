using System;
using DarkNights.View.Terrain;
using UnityEditor;
using UnityEngine;

namespace DarkNights.Editor
{
    /// <summary>接入现有背景的独立绘制材质；只更新当前样式的 Shader 引用，不创建或重绘背景 PNG，不修改点缀参数。</summary>
    public static class CaveEntranceArtSetup
    {
        public const string Root = "Assets/DarkNights/Res/Art/Custom/CaveEntranceLayers/";
        [MenuItem("Dark Nights/Terrain/接入现有背景分层绘制")]
        public static void Install()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || BuildPipeline.isBuildingPlayer)
                throw new InvalidOperationException("Editor 有活动任务。");
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(Root + "CaveBackgroundLayer.shader");
            if (shader == null) throw new InvalidOperationException("缺少分层绘制材质 Shader。");
            foreach (string path in new[] { "Assets/DarkNights/Res/Terrain/StrataCave/Background.asset",
                "Assets/DarkNights/Res/Terrain/CaveContourStatic/Background.asset" })
            {
                var background = AssetDatabase.LoadAssetAtPath<CaveBackgroundStyle>(path);
                if (background == null) throw new InvalidOperationException("缺少当前背景样式：" + path);
                if (background.LayerShader != null && background.LayerShader != shader)
                    throw new InvalidOperationException("当前样式已有其他人工材质，不能覆盖：" + path);
                Undo.RecordObject(background, "接入现有背景独立绘制");
                background.LayerShader = shader; EditorUtility.SetDirty(background);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("CAVE_EXISTING_LAYERS_INSTALLED 原三层素材及生成参数保持");
        }
    }
}
