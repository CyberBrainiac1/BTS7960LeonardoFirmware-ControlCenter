using ArduinoFFBControlCenter.Models;

namespace ArduinoFFBControlCenter.Services;

public interface ISimTelemetryProvider
{
    string Name { get; }
    bool IsActive { get; }
    SimTelemetrySample? GetLatest();
}

public class NullSimTelemetryProvider : ISimTelemetryProvider
{
    public string Name => "None";
    public bool IsActive => false;
    public SimTelemetrySample? GetLatest() => null;
}

public class SimTelemetryService
{
    private ISimTelemetryProvider _provider = new NullSimTelemetryProvider();

    public ISimTelemetryProvider Provider => _provider;

    public void SetProvider(ISimTelemetryProvider provider)
    {
        _provider = provider;
    }
}
