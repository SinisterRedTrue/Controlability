namespace Controlability.Core.Profiles;

/// <summary>A profile file is unreadable, invalid, or from a newer app version.</summary>
public sealed class ProfileFormatException : Exception
{
    public ProfileFormatException(string message)
        : base(message)
    {
    }

    public ProfileFormatException(string message, Exception inner)
        : base(message, inner)
    {
    }
}
