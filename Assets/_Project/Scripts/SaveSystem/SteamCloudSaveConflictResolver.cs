using System;
using System.Text;
using Hecton8.Core;

namespace Hecton8.SaveSystem
{
    /// <summary>
    /// Non-blocking Steam Cloud conflict resolver.
    /// It compares MMF header metadata supplied by the save/cloud layer and routes the decision through diegetic UI.
    /// </summary>
    public static class SteamCloudSaveConflictResolver
    {
        private const string PromptTitle = "SYNC ANOMALY DETECTED";
        private static readonly Action _confirmSuggestedAction = ConfirmSuggested; // COLD ALLOC: Action[1] - modal confirm delegate cache - owner: SteamCloudSaveConflictResolver
        private static readonly Action _confirmOtherAction = ConfirmOther; // COLD ALLOC: Action[1] - modal alternate delegate cache - owner: SteamCloudSaveConflictResolver
        private static readonly StringBuilder _promptBuilder = new StringBuilder(192); // COLD ALLOC: StringBuilder[192] - reused cloud conflict prompt builder - owner: SteamCloudSaveConflictResolver

        private static Action<SteamCloudSaveChoice> _pendingResolver;
        private static SteamCloudSaveChoice _pendingSuggestedChoice;
        private static SteamCloudSaveChoice _pendingAlternateChoice;

        public static SteamCloudSaveResolution Resolve(
            in SteamCloudSaveCandidate local,
            in SteamCloudSaveCandidate cloud)
        {
            SteamCloudSaveChoice suggested = ResolveSuggestedChoice(in local, in cloud);
            SteamCloudSaveChoice alternate = suggested == SteamCloudSaveChoice.Cloud
                ? SteamCloudSaveChoice.Local
                : SteamCloudSaveChoice.Cloud;

            return new SteamCloudSaveResolution(suggested, alternate);
        }

        public static bool TryShowDiegeticPrompt(
            in SteamCloudSaveCandidate local,
            in SteamCloudSaveCandidate cloud,
            Action<SteamCloudSaveChoice> onResolved)
        {
            if (onResolved == null || !GlobalRegistry.TryGet(out IModalWindowService modalWindow))
                return false;

            SteamCloudSaveResolution resolution = Resolve(in local, in cloud);
            _pendingResolver = onResolved;
            _pendingSuggestedChoice = resolution.SuggestedChoice;
            _pendingAlternateChoice = resolution.AlternateChoice;

            modalWindow.ShowModal(
                PromptTitle,
                BuildPromptMessage(in local, in cloud, in resolution),
                _confirmSuggestedAction,
                _confirmOtherAction,
                FormatChoiceLabel(resolution.SuggestedChoice),
                FormatChoiceLabel(resolution.AlternateChoice));
            return true;
        }

        private static SteamCloudSaveChoice ResolveSuggestedChoice(
            in SteamCloudSaveCandidate local,
            in SteamCloudSaveCandidate cloud)
        {
            if (cloud.TimestampUnixMs > local.TimestampUnixMs)
                return SteamCloudSaveChoice.Cloud;

            if (local.TimestampUnixMs > cloud.TimestampUnixMs)
                return SteamCloudSaveChoice.Local;

            if (cloud.PlayTimeSeconds > local.PlayTimeSeconds)
                return SteamCloudSaveChoice.Cloud;

            return SteamCloudSaveChoice.Local;
        }

        private static string BuildPromptMessage(
            in SteamCloudSaveCandidate local,
            in SteamCloudSaveCandidate cloud,
            in SteamCloudSaveResolution resolution)
        {
            int localPlayTimeSeconds = local.PlayTimeSeconds > 0f ? (int)local.PlayTimeSeconds : 0;
            int cloudPlayTimeSeconds = cloud.PlayTimeSeconds > 0f ? (int)cloud.PlayTimeSeconds : 0;

            _promptBuilder.Length = 0;
            _promptBuilder
                .Append("Local MMF header: timestamp=")
                .Append(local.TimestampUnixMs)
                .Append(", playTime=")
                .Append(localPlayTimeSeconds)
                .Append("s\nCloud MMF header: timestamp=")
                .Append(cloud.TimestampUnixMs)
                .Append(", playTime=")
                .Append(cloudPlayTimeSeconds)
                .Append("s\nSuggested source: ")
                .Append(FormatChoiceLabel(resolution.SuggestedChoice));

            return _promptBuilder.ToString();
        }

        private static string FormatChoiceLabel(SteamCloudSaveChoice choice)
        {
            switch (choice)
            {
                case SteamCloudSaveChoice.Cloud:
                    return "USE CLOUD";
                case SteamCloudSaveChoice.Local:
                    return "USE LOCAL";
                default:
                    return "IGNORE";
            }
        }

        private static void ConfirmSuggested()
        {
            Complete(_pendingSuggestedChoice);
        }

        private static void ConfirmOther()
        {
            Complete(_pendingAlternateChoice);
        }

        private static void Complete(SteamCloudSaveChoice choice)
        {
            Action<SteamCloudSaveChoice> resolver = _pendingResolver;
            _pendingResolver = null;
            _pendingSuggestedChoice = SteamCloudSaveChoice.None;
            _pendingAlternateChoice = SteamCloudSaveChoice.None;
            resolver?.Invoke(choice);
        }
    }

    public enum SteamCloudSaveChoice : byte
    {
        None = 0,
        Local = 1,
        Cloud = 2
    }

    public readonly struct SteamCloudSaveCandidate
    {
        public SteamCloudSaveCandidate(ulong timestampUnixMs, float playTimeSeconds)
        {
            TimestampUnixMs = timestampUnixMs;
            PlayTimeSeconds = playTimeSeconds;
        }

        public ulong TimestampUnixMs { get; }
        public float PlayTimeSeconds { get; }
    }

    public readonly struct SteamCloudSaveResolution
    {
        public SteamCloudSaveResolution(SteamCloudSaveChoice suggestedChoice, SteamCloudSaveChoice alternateChoice)
        {
            SuggestedChoice = suggestedChoice;
            AlternateChoice = alternateChoice;
        }

        public SteamCloudSaveChoice SuggestedChoice { get; }
        public SteamCloudSaveChoice AlternateChoice { get; }
    }
}
