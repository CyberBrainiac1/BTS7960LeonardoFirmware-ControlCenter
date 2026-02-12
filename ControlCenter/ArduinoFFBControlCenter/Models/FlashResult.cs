namespace ArduinoFFBControlCenter.Models;

public enum FlashErrorType
{
    None,
    AvrDudeMissing,
    PortBusy,
    PortNotFound,
    BootloaderNotDetected,
    FlashFailed,
    VerifyFailed,
    SignatureMismatch,
    Unknown
}

public class FlashResult
{
    public bool Success { get; set; }
    public FlashErrorType ErrorType { get; set; } = FlashErrorType.None;
    public string UserMessage { get; set; } = string.Empty;
    public string SuggestedAction { get; set; } = string.Empty;
    public string RawOutput { get; set; } = string.Empty;
    public int ExitCode { get; set; }
    public string? BootloaderPort { get; set; }
    public int RetryCount { get; set; }
}
