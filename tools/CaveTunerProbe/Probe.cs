using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using DarkNights.Core.Config.Terrain;
using DarkNights.Core.Logic.Terrain;

internal static class Probe
{
    static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
    static void Main(string[] args)
    {
        var data = File.ReadAllBytes(args[0]);
        var checks = new SortedDictionary<string,string>();
        var timings = new Dictionary<string,object>();
        var original = (byte[])data.Clone();
        bool Solid(int x, int y)
        {
            if (x < 0 || y < 0 || x >= 2560 || y >= 1536) return false;
            int i = (y / 8 * 320 + x / 8) * 2;
            return data[i] != 0 && TerrainShapeGeometry.Contains((TerrainCellShape)(data[i+1] >> 1),
                (x % 8 + .5f) / 8, 1 - (y % 8 + .5f) / 8);
        }
        foreach (CaveOutlineMode mode in Enum.GetValues(typeof(CaveOutlineMode)))
        foreach (int amplitude in new[]{0,3,8})
        foreach (int quantization in new[]{1,4})
        {
            var outline = new CaveOutlineSettings(mode,"outline-probe",amplitude,20,quantization);
            byte[] pixels = CaveOutlineBaker.Bake((x,y) => y < 56 + (x/13%3)*5 || y>140,
                256,192,17,21,170,140,outline);
            checks.Add($"outline/{mode}/{amplitude}/{quantization}",Hash(pixels));
        }
        var normal = new RoundedClusterModifier(algorithmVersion: RoundedClusterAlgorithmVersion.LocalV2);
        int radius = CaveRockBaker.DistanceCap + CaveOutlineSettings.Current.Reach + normal.DependencyRadiusPixels + 8;
        int[][] points = {new[]{69,51},new[]{126,80},new[]{128,96},new[]{199,139},new[]{1,190}};
        foreach (var p in points)
        foreach (bool grain in new[]{false,true})
        foreach (bool change in new[]{false,true})
        {
            Array.Copy(original,data,data.Length);
            if (change) {int i=(p[1]*320+p[0])*2; data[i]=(byte)(data[i]==0?1:0); data[i+1]=0;}
            var stack = new CaveModifierStack(new[]{new RoundedClusterModifier(grain:grain,
                algorithmVersion:RoundedClusterAlgorithmVersion.LocalV2)});
            byte[] output = BakePatches(p[0],p[1],1,stack);
            checks.Add($"rock/{p[0]},{p[1]}/grain={grain}/change={change}",Hash(output));
        }
        Array.Copy(original,data,data.Length);
        foreach(int size in new[]{1,8})
        {
            var stack = new CaveModifierStack(new[]{normal});
            for(int i=0;i<3;i++) BakePatches(128,96,size,stack);
            var samples = new List<double>();
            for(int i=0;i<11;i++) {var sw=Stopwatch.StartNew(); BakePatches(128,96,size,stack); samples.Add(sw.Elapsed.TotalMilliseconds);}
            var sorted=samples.OrderBy(x=>x).ToArray();
            timings.Add(size==1?"single":"8x8",new {samples,median=sorted[5],p95=sorted[10]});
        }
        File.WriteAllText(args[1],JsonSerializer.Serialize(new {kind="CPU_KERNEL_ONLY",runtime=Environment.Version.ToString(),radius,checks,timings},new JsonSerializerOptions{WriteIndented=true}));
        Console.WriteLine($"CHECKS={checks.Count}; RADIUS={radius}; OUTPUT={args[1]}");

        byte[] BakePatches(int cx,int cy,int cells,CaveModifierStack stack)
        {
            int left=Math.Max(0,cx*8-radius),top=Math.Max(0,cy*8-radius);
            int right=Math.Min(2560,(cx+cells)*8+radius),bottom=Math.Min(1536,(cy+cells)*8+radius);
            using var stream=new MemoryStream();
            for(int py=top/256;py<=(bottom-1)/256;py++)
            for(int px=left/256;px<=(right-1)/256;px++)
            {
                int x=Math.Max(left,px*256),y=Math.Max(top,py*256);
                int width=Math.Min(right,(px+1)*256)-x,height=Math.Min(bottom,(py+1)*256)-y;
                var region=CaveModifiedTerrain.BakeRockRegion(Solid,2560,1536,"DN-MATERIAL-0921",
                    CaveOutlineSettings.Current,stack,x,y,width,height,CaveRockBaker.DistanceCap,43*8);
                var pixels=CaveRockBaker.BakeRegion(region,x,y,width,height,"DN-MATERIAL-0921",4);
                stream.Write(pixels,0,pixels.Length);
            }
            return stream.ToArray();
        }
    }
}
