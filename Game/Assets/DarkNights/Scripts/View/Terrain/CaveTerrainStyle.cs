using UnityEngine;
namespace DarkNights.View.Terrain
{
    /// <summary>洞穴岩层的人工可编辑表现配置；不保存玩法状态，材质按每个预览独立实例化并释放。</summary>
    [CreateAssetMenu(menuName = "Dark Nights/Cave terrain style")]
    public sealed class CaveTerrainStyle : ScriptableObject
    {
        public Shader Shader;
        public Texture2D Rock;
        [Range(0, 255)] public int TextureSeed = 17;
        [Range(0, .6f)] public float TextureDetail = .22f;
        public Color DarkColor = new Color32(17, 14, 15, 255);
        public Color BaseColor = new Color32(52, 41, 38, 255);
        public Color LightColor = new Color32(138, 107, 77, 255);
        public Color EdgeColor = new Color32(168, 130, 96, 255);
        [Range(0, .6f)] public float EdgeStrength = .045f;
        [Range(1, 5)] public float EdgeStartPixels = 2;
        [Range(4, 16)] public float EdgeDecayPixels = 10;
        [Range(16, 48)] public float CoreAfterPixels = 36;
        [Range(.5f, 2.5f)] public float EdgeSoftness = 1.55f;
        [Range(.5f, 2.5f)] public float LightSoftness = 1.55f;
        [Range(6, 24)] public float LightFalloff = 12;
        [Range(48, 160)] public float RockLightLoss = 96;

        /// <summary>把人工风格参数写入每张地图的独立材质；不修改共享材质或权威地图。</summary>
        public void ApplyTo(Material material)
        {
            if (material == null) return;
            material.SetFloat("_TextureSeed", TextureSeed);
            material.SetFloat("_TextureDetail", Mathf.Clamp(TextureDetail, 0, .6f));
            material.SetColor("_DarkColor", DarkColor); material.SetColor("_BaseColor", BaseColor);
            material.SetColor("_LightColor", LightColor); material.SetColor("_EdgeColor", EdgeColor);
            material.SetFloat("_EdgeStrength", Mathf.Clamp(EdgeStrength, 0, .6f));
            float start = Mathf.Clamp(EdgeStartPixels, 1, 5);
            float decay = Mathf.Clamp(EdgeDecayPixels, start + 1, 16);
            float core = Mathf.Clamp(CoreAfterPixels, decay + 2, 48);
            material.SetFloat("_EdgeStartPixels", start); material.SetFloat("_EdgeDecayPixels", decay);
            material.SetFloat("_CoreAfterPixels", core);
            material.SetFloat("_EdgeSoftness", Mathf.Clamp(EdgeSoftness, .5f, 2.5f));
            material.SetFloat("_LightSoftness", Mathf.Clamp(LightSoftness, .5f, 2.5f));
        }

        /// <summary>复制另一风格的可调表面参数；保留当前 Shader 与纹理引用。</summary>
        public void CopyParametersFrom(CaveTerrainStyle source)
        {
            if (source == null) return;
            TextureSeed = source.TextureSeed; TextureDetail = source.TextureDetail;
            DarkColor = source.DarkColor; BaseColor = source.BaseColor;
            LightColor = source.LightColor; EdgeColor = source.EdgeColor;
            EdgeStrength = source.EdgeStrength; EdgeStartPixels = source.EdgeStartPixels;
            EdgeDecayPixels = source.EdgeDecayPixels; CoreAfterPixels = source.CoreAfterPixels;
            EdgeSoftness = source.EdgeSoftness; LightSoftness = source.LightSoftness;
            LightFalloff = source.LightFalloff; RockLightLoss = source.RockLightLoss;
        }
    }
}
