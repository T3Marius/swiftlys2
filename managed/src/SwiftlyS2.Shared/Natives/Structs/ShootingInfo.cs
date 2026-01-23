using System.Runtime.InteropServices;

namespace SwiftlyS2.Shared.Natives;

[StructLayout(LayoutKind.Sequential)]
public struct ShootingInfo
{
    public int AttackTick;
    public float AttackTickFraction;
    public float AttackTime;
    public int RenderTick;
    public float RenderTickFraction;
    public Vector AttackPos;
    public Vector AttackAngle;
    public Vector PunchAngle;
    public byte FirstShot;
    public int State;
    public int ServerTickCount;
    public float Inaccuracy;
    public float GroundInaccuracy;
    public float AirInaccuracy;
    public float RecoilIdx;
    private float Unknown;
}