using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace DarkNights.View.MapModule
{
    /// <summary>
    /// 作者Scene及Prefab有效输入的稳定摘要；派生网格、Bake引用和本地选择不进入来源身份。
    /// Editor与运行入口共用同一检查，拒绝源变化后启动旧版本，不反写作者对象。
    /// </summary>
    public static class MapModuleSource
    {
        public static string Hash(MapModuleStudyAuthoring study, MapModuleCaseAuthoring sample)
        {
            if (study == null || sample == null || study.RequiredCase(sample.CaseId) != sample ||
                sample.Spawn == null || sample.Modules == null || sample.Modules.Length == 0 ||
                sample.Modules.Any(m => m == null) || sample.Modules.Distinct().Count() != sample.Modules.Length)
                throw new InvalidOperationException("地图布局中的模块为空或重复，或缺少出生位置、地图配置引用。");
            ValidateId(study.StudyId); ValidateId(sample.CaseId);
            if (sample.PreviewMesh == null || sample.PreviewRenderer == null ||
                sample.PreviewMesh.transform.position != Vector3.zero || sample.PreviewMesh.transform.lossyScale != Vector3.one ||
                Quaternion.Angle(sample.PreviewMesh.transform.rotation, Quaternion.identity) > .001f)
                throw new InvalidOperationException("地图预览未设置网格组件，或位置、旋转、缩放有误。请将预览放在原点，旋转设为零，缩放设为一。");
            var text = new StringBuilder("mapmodule-source-v1|20cells/U|shape12|whitebox-v1|");
            text.Append(study.StudyId).Append('|').Append(sample.CaseId).Append('|');
            foreach (var module in sample.Modules)
            {
                module.Validate();
                text.Append(module.ModuleId).Append('|').Append(module.DisplayName).Append('|')
                    .Append(module.Footprint).Append('|').Append(module.CellOffset).Append('|');
                foreach (var solid in module.Solids)
                {
                    text.Append(solid.Id).Append(':').Append(solid.Material).Append(':');
                    foreach (var vertex in solid.Vertices) text.Append(vertex.x).Append(',').Append(vertex.y).Append(';');
                    text.Append('|');
                }
                foreach (var port in module.Ports)
                    text.Append(port.Id).Append(':').Append(port.From).Append(':').Append(port.To).Append(':').Append(port.Normal).Append('|');
            }
            foreach (var link in sample.Connections ?? throw new InvalidOperationException("未设置模块连接列表。"))
            {
                if (link == null || !sample.Modules.Contains(link.From) || !sample.Modules.Contains(link.To) || link.From == link.To)
                    throw new InvalidOperationException("每条连接需要选择当前地图中的两个不同模块。");
                var from = link.From.Ports.SingleOrDefault(p => p.Id == link.FromPort);
                var to = link.To.Ports.SingleOrDefault(p => p.Id == link.ToPort);
                if (from == null || to == null || from.Normal != -to.Normal ||
                    from.From + link.From.CellOffset != to.From + link.To.CellOffset ||
                    from.To + link.From.CellOffset != to.To + link.To.CellOffset)
                    throw new InvalidOperationException("模块连接口的位置、大小或朝向不匹配，请调整后重新检查。");
                text.Append(Array.IndexOf(sample.Modules, link.From)).Append(':').Append(link.FromPort).Append('>')
                    .Append(Array.IndexOf(sample.Modules, link.To)).Append(':').Append(link.ToPort).Append('|');
            }
            var spawn = sample.Spawn.position;
            if (!Finite(spawn.x) || !Finite(spawn.y) || Math.Abs(spawn.z) > .0001f)
                throw new InvalidOperationException("出生位置的坐标无效。");
            text.Append(spawn.x.ToString("R", CultureInfo.InvariantCulture)).Append('|')
                .Append(spawn.y.ToString("R", CultureInfo.InvariantCulture));
            using var hash = SHA256.Create();
            return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(text.ToString()))).Replace("-", "").ToLowerInvariant();
        }
        private static void ValidateId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length > 40 || value.Any(c =>
                !(c >= 'a' && c <= 'z' || c >= '0' && c <= '9' || c == '-' || c == '_')))
                throw new InvalidOperationException("场景和布局的标识只能使用小写英文字母、数字、短横线或下划线，长度为1至40个字符。");
        }
        private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
