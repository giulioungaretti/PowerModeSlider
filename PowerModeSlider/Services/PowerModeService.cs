using System;

namespace PowerModeSlider.Services;

public class PowerModeService : IPowerModeService
{
    public Guid BestPowerEfficiency => new Guid("00000000-0000-0000-0000-000000000001");

    public Guid Balanced => new Guid("00000000-0000-0000-0000-000000000001");

    public Guid BestPerformance => new Guid("00000000-0000-0000-0000-000000000001");

    public Guid GetPowerMode()
    {
        return new Guid("00000000-0000-0000-0000-000000000001");
    }

    public Guid GetPowerModeAC()
    {
        return new Guid("00000000-0000-0000-0000-000000000001");
    }

    public Guid GetPowerModeDC()
    {
        return new Guid("00000000-0000-0000-0000-000000000001");
    }

    public bool IsSupported()
    {
        return true;
    }

    public bool TrySetPowerMode(Guid modeId)
    {
        return true;
    }

    public bool TrySetPowerModeAC(Guid modeId)
    {
        return true;
    }

    public bool TrySetPowerModeDC(Guid modeId)
    {
        return true;
    }
}
