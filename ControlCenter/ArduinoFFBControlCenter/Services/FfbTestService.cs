namespace ArduinoFFBControlCenter.Services;

public class FfbTestService
{
    private readonly LoggerService _logger;
    private Guid? _attachedGuid;

    public bool IsAttached => _attachedGuid.HasValue;

    public FfbTestService(LoggerService logger)
    {
        _logger = logger;
    }

    public bool Attach(Guid instanceGuid)
    {
        _attachedGuid = instanceGuid;
        _logger.Info("FFB test service attached (safe stub mode).");
        return true;
    }

    public void StopAll()
    {
        _logger.Info("FFB test service stop requested.");
    }

    public void PlayConstant(int magnitude)
    {
        _logger.Warn("Built-in FFB effect playback is disabled in this build.");
    }

    public void PlaySine(int magnitude, int periodMs)
    {
        _logger.Warn("Built-in FFB effect playback is disabled in this build.");
    }

    public void PlaySpring(int coefficient)
    {
        _logger.Warn("Built-in FFB effect playback is disabled in this build.");
    }

    public void PlayDamper(int coefficient)
    {
        _logger.Warn("Built-in FFB effect playback is disabled in this build.");
    }
}
