namespace Controlability.Core.Abstractions;

public interface IClock
{
    long Ticks { get; }

    double ToMs(long ticks);
}
