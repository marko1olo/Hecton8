using System;

namespace Hecton8.Tools.Scanner.Contracts
{
    /// <summary>
    /// Hash-keyed scanner lore title source for diegetic display writers.
    /// </summary>
    public interface IScannerLoreTitleReadModel
    {
        bool TryWriteLoreEntityTitle(uint hash, Span<char> destination, out int written);
    }
}
