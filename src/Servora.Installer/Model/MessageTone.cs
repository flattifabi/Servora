namespace Servora.Installer.Model;

/// <summary>Colour/semantic tone of a console value.</summary>
internal enum MessageTone
{
    /// <summary>Default text colour.</summary>
    Normal,

    /// <summary>Positive state (green).</summary>
    Good,

    /// <summary>Attention required (yellow).</summary>
    Warning,

    /// <summary>Failure state (red).</summary>
    Bad,

    /// <summary>Secondary information (grey).</summary>
    Muted,

    /// <summary>Highlighted value (cyan).</summary>
    Accent,
}
