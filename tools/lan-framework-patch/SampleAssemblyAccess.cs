using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("DarkNights.Tests")]

// YYGC's generated update dispatcher accesses internal CoreBehaviour fields.
// Grant isolated Sample, formal Runtime and generated UGUI View dispatchers the same access as YYGC.MinimalNetwork.
[assembly: InternalsVisibleTo("DarkNights.Samples.LanCoop.Runtime")]
[assembly: InternalsVisibleTo("DarkNights.Runtime")]
[assembly: InternalsVisibleTo("DarkNights.View")]
