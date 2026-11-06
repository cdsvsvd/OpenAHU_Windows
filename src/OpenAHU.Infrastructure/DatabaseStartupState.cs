namespace OpenAHU.Infrastructure;

/// <summary>Process-local startup readiness gate: failed migration must not appear healthy.</summary>
public sealed class DatabaseStartupState
{
    public bool IsReady { get; set; }
}
