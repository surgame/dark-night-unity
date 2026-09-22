using System;
using System.IO;
using DarkNights.Core.Logic.Terrain;
using UnityEditor;
/// <summary>显式修订本任务固定样板的轮廓：按整格占比后统一坡形，避免逐格拟合半高面产生锯齿。</summary>
public static class RefineStrataSample
{
    public static string Run()
    {
        const string file = "Assets/DarkNights/Res/Terrain/StrataCave/ReferenceChamber.cells.bytes";
        var bytes = File.ReadAllBytes(file); var cells = new byte[320 * 192]; var protection = new bool[cells.Length];
        for (int i = 0; i < cells.Length; i++) cells[i] = bytes[i * 2];
        var source = File.ReadAllBytes("../tools/contour-reference/profile-raw.bin");
        for (int cy = 0; cy < 39; cy++) for (int cx = 0; cx < 63; cx++)
        {
            int coverage = 0;
            for (int y = 0; y < 8; y++) for (int x = 0; x < 8; x++) coverage += source[(cy * 8 + y) * 504 + cx * 8 + x];
            cells[(cy + 36) * 320 + cx + 40] = coverage >= 32 ? (byte)2 : (byte)0;
        }
        var shapes = TerrainShapeGeometry.Build(cells, protection, 320, 192);
        for (int i = 0; i < cells.Length; i++) { bytes[i * 2] = cells[i]; bytes[i * 2 + 1] = (byte)(shapes[i] << 1); }
        File.WriteAllBytes(file, bytes); AssetDatabase.ImportAsset(file); return "Updated owned reference cells with coherent slopes";
    }
}
