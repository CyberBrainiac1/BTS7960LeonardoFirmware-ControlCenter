namespace ArduinoFFBControlCenter.Models;

public class OllamaChatEntry
{
    public string Role { get; set; } = "assistant";
    public string Content { get; set; } = string.Empty;
    public DateTime TimestampLocal { get; set; } = DateTime.Now;

    public bool IsUser => string.Equals(Role, "user", StringComparison.OrdinalIgnoreCase);
    public bool IsAssistant => string.Equals(Role, "assistant", StringComparison.OrdinalIgnoreCase);
}
