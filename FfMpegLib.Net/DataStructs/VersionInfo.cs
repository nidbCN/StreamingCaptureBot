using System.Runtime.InteropServices;

namespace FfMpeg.AutoGen.Wrapper.DataStructs;

[StructLayout(LayoutKind.Explicit)]
public struct VersionInfo(uint version)
{
    [FieldOffset(0)] public uint Version = version;
    [FieldOffset(2)] public ushort Major;
    [FieldOffset(1)] public byte Minor;
    [FieldOffset(0)] public byte Patch;

    public override string ToString()
        => $"{Major}.{Minor}.{Patch}";
}
