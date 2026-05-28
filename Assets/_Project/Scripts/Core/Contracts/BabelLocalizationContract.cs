using System;

namespace Hecton8.Core.Contracts
{
    /// <summary>
    /// Allocation-free localization contract exposed through GlobalRegistry for Babel UI consumers.
    /// Keep this contract in the Core.Contracts assembly so Babel-only mocks do not depend on the
    /// concrete localization manager or any sibling runtime domain.
    /// </summary>
    public interface IBabelLocalization
    {
        /// <summary>Active language as a compact stable id.</summary>
        ushort ActiveLanguageId { get; }

        /// <summary>Resolve UTF-8 bytes for a localization key hash without creating a managed string.</summary>
        bool TryGetLocalizedSpan(uint hash, out ReadOnlySpan<byte> utf8Bytes);

        /// <summary>Resolve a staged char buffer for TMP SetCharArray without creating a managed string.</summary>
        bool TryGetLocalizedBuffer(uint hash, out char[] buffer, out int length);

        /// <summary>Decode localized UTF-8 into caller-owned storage without creating a managed string.</summary>
        bool TryWriteLocalized(uint hash, Span<char> destination, out int length, bool stripRichText = false);

        /// <summary>Inject one integer payload into a localized template using caller-owned storage.</summary>
        bool TryWriteLocalizedInt(uint templateHash, int value, Span<char> destination, out int length);

        /// <summary>Resolve singular/plural key choice through deterministic integer math.</summary>
        uint ResolvePluralHash(uint singularHash, uint pluralHash, int value);
    }
}
