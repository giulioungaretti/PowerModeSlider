using System.Runtime.InteropServices;

namespace PowerModeSlider;

[StructLayout(LayoutKind.Sequential)]
internal struct ScreenRect
{
    public int Left, Top, Right, Bottom;

    public readonly bool Contains(int x, int y) =>
        x >= Left && x < Right && y >= Top && y < Bottom;
}
