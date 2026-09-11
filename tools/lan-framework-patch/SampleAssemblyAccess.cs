using System.Runtime.CompilerServices;

// YYGC's generated update dispatcher accesses internal CoreBehaviour fields.
// Grant the isolated sample and the formal Dark Nights runtime the same access as YYGC.MinimalNetwork.
[assembly: InternalsVisibleTo("DarkNights.Samples.LanCoop.Runtime")]
[assembly: InternalsVisibleTo("DarkNights.Runtime")]
