namespace DarkNights.View.Lighting
{
    /// <summary>本地环境照明的渲染后端选择；不进入权威对象、协议或存档，URP 后端沿用当前 2D Renderer。</summary>
    public enum LightingBackendKind
    {
        PrivateField,
        Urp2D
    }
}
