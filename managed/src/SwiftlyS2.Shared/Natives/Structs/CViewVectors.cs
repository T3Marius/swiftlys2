using System.Runtime.InteropServices;

namespace SwiftlyS2.Shared.Natives;

[StructLayout(LayoutKind.Sequential)]
public struct CViewVectors {
    public Vector View;
    public Vector HullMin;
    public Vector HullMax;
    public Vector DuckHullMin;
    public Vector DuckHullMax;
    public Vector DuckView;
    public Vector ObsHullMin;
    public Vector ObsHullMax;
    public Vector DeadViewHeight;
}