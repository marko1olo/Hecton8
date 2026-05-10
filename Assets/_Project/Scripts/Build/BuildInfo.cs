using UnityEngine;

namespace Hecton8.Build
{
    [CreateAssetMenu(fileName = "BuildInfo", menuName = "Hecton8/Build Info")]
    public sealed class BuildInfo : ScriptableObject
    {
        private const int VersionPrefixLength = 3;
        private const uint UnknownHash = 0u;

        [SerializeField] private string gitCommitHash = "unknown";
        [SerializeField] private int gitCommitHash32;
        [SerializeField] private bool gitDirty;
        [SerializeField] private string gitBranch = "unknown";
        [SerializeField] private string buildUtc = "unknown";
        [SerializeField] private string unityVersion = "unknown";
        [SerializeField] private string buildTarget = "unknown";

        public string GitCommitHash => gitCommitHash;
        public int GitCommitHash32 => gitCommitHash32;
        public bool GitDirty => gitDirty;
        public string GitBranch => gitBranch;
        public string BuildUtc => buildUtc;
        public string UnityVersion => unityVersion;
        public string BuildTarget => buildTarget;

        public void Apply(
            string branch,
            string commitHash,
            int commitHash32,
            bool dirty,
            string utcTimestamp,
            string unity,
            string target)
        {
            gitBranch = string.IsNullOrWhiteSpace(branch) ? "unknown" : branch;
            gitCommitHash = string.IsNullOrWhiteSpace(commitHash) ? "unknown" : commitHash;
            gitCommitHash32 = commitHash32;
            gitDirty = dirty;
            buildUtc = string.IsNullOrWhiteSpace(utcTimestamp) ? "unknown" : utcTimestamp;
            unityVersion = string.IsNullOrWhiteSpace(unity) ? "unknown" : unity;
            buildTarget = string.IsNullOrWhiteSpace(target) ? "unknown" : target;
        }

        public void Apply(string branch, string commitHash, string utcTimestamp, string unity, string target)
        {
            Apply(branch, commitHash, ParseCommitHash32(commitHash), false, utcTimestamp, unity, target);
        }

        public string FormatWatermark()
        {
            return gitBranch + " " + gitCommitHash;
        }

        public int WriteVersionWatermark(char[] buffer)
        {
            if (buffer == null || buffer.Length < VersionPrefixLength + 8)
                return 0;

            buffer[0] = 'H';
            buffer[1] = '8';
            buffer[2] = '-';

            uint value = unchecked((uint)gitCommitHash32);
            if (value == UnknownHash)
                value = ParseCommitHash32(gitCommitHash);

            for (int i = 0; i < 8; i++)
            {
                int shift = 28 - (i * 4);
                int nibble = (int)((value >> shift) & 0xFu);
                buffer[VersionPrefixLength + i] = ToUpperHex(nibble);
            }

            int count = VersionPrefixLength + 8;
            if (gitDirty && buffer.Length > count)
                buffer[count++] = 'D';

            return count;
        }

        public static int ParseCommitHash32(string commitHash)
        {
            if (string.IsNullOrEmpty(commitHash))
                return 0;

            uint value = 0u;
            int parsed = 0;
            for (int i = 0; i < commitHash.Length && parsed < 8; i++)
            {
                int nibble = FromHex(commitHash[i]);
                if (nibble < 0)
                    break;

                value = (value << 4) | (uint)nibble;
                parsed++;
            }

            return parsed == 8 ? unchecked((int)value) : 0;
        }

        private static int FromHex(char value)
        {
            if (value >= '0' && value <= '9')
                return value - '0';
            if (value >= 'a' && value <= 'f')
                return value - 'a' + 10;
            if (value >= 'A' && value <= 'F')
                return value - 'A' + 10;
            return -1;
        }

        private static char ToUpperHex(int value)
        {
            return (char)(value < 10 ? '0' + value : 'A' + (value - 10));
        }
    }
}
