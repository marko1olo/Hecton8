// ============================================================================
// HECTON-8 — BuildPlaytestEntry.cs
// Struktura dannyh dlya zapisi rezultatov kazhdoy sborki.
//
// NAZNAChENIE:
//   Kazhdyy build dolzhen zapolnit etot kontrakt pered otpravkoy na playtest:
//   - Versiya (data + hesh + nomer sborki)
//   - FPS feel (mean / worst / hitch 60→30 moment)
//   - Glavnaya razdrazhavshaya problema
//   - Glavnyy vizualnyy defekt
//   - Glavnyy UX defekt
//   - Glavnyy kontent gap
//   - Blocker: da/net
//
// ISPOLZOVANIE:
//   var entry = new BuildPlaytestEntry(
//     version: "2026-04-07-main-#847",
//     fpsMean: 58f,
//     fpsWorst: 32f,
//     mainIrritant: "Surface transition hitch when rotating camera",
//     mainVisualFlaw: "Gas giant reads as flat overlay",
//     mainUXFlaw: "Pause menu needs better button labeling",
//     mainContentGap: "Underwater flora density too low",
//     isBlocker: true
//   );
//   BuildPlaytestLog.RecordEntry(entry);
//
// ZOLOTOY STANDART:
//   • Kazhdyy build = odna zapis
//   • Zapisyvaetsya PERED otpravkoy na playtest
//   • Vhodit v BUILD_PLAYTEST_ISSUES.md kak history
//   • Ispolzuetsya dlya otslezhivaniya progress mezhdu relizami
// ============================================================================

using System;
using System.Globalization;
using UnityEngine;

namespace Hecton8.BuildTools
{
    /// <summary>
    /// Kontrakt dlya zapisi rezultatov odnogo build playtest.
    /// </summary>
    [Serializable]
    public struct BuildPlaytestEntry
    {
        /// <summary>Versiya sborki (data-vetka-hesh).</summary>
        [Tooltip("Versiya sborki: YYYY-MM-DD-branch-commit")]
        public string Version;

        /// <summary>Sredniy FPS za 10 minut testirovaniya.</summary>
        [Tooltip("Sredniy FPS za testovyy progulku")]
        public float FpsMean;

        /// <summary>Hudshiy (minimalnyy) FPS v moment pika nagruzki.</summary>
        [Tooltip("Hudshiy odnokratnyy frame za test")]
        public float FpsWorst;

        /// <summary>Glavnaya razdrazhayuschaya problema, kotoraya isportila opyt.</summary>
        [Tooltip("Chto bolshe vsego razdrazhalo pri igre")]
        public string MainIrritant;

        /// <summary>Glavnyy vizualnyy defekt (sheyder, LOD, artefakt).</summary>
        [Tooltip("Glavnyy vizualnyy bag: gazovyy gigant, razmytie, LOD pop, i t.d.")]
        public string MainVisualFlaw;

        /// <summary>Glavnyy UX defekt (nadpis, knopka, menyu).</summary>
        [Tooltip("Glavnyy UX bag: neyasnaya knopka, nepravilnyy tekst, propuschennoe menyu")]
        public string MainUXFlaw;

        /// <summary>Glavnyy kontent gap (otsutstvuet flora, vragi, sistema).</summary>
        [Tooltip("Chto otsutstvuet kontentom: voda slishkom pusta, net vragov, i t.d.")]
        public string MainContentGap;

        /// <summary>Yavlyaetsya li eta problema blokerom dlya sleduyuschego reliza.</summary>
        [Tooltip("Blokiruet li eto priemku bilda")]
        public bool IsBlocker;

        /// <summary>Dopolnitelnye primechaniya (optsionalno).</summary>
        [Tooltip("Lyubye dopolnitelnye zametki")]
        public string Notes;

        /// <summary>Timestamp kogda entry byla sozdana.</summary>
        public long CreatedTimestamp;

        /// <summary>Pokazyvaet, byl li entry sozdan cherez fabriku i poluchil timestamp.</summary>
        public readonly bool HasRecordedTimestamp => CreatedTimestamp > 0;

        /// <summary>
        /// Sozdaet novuyu zapis s tekuschim vremenem.
        /// </summary>
        public static BuildPlaytestEntry Create(
            string version,
            float fpsMean,
            float fpsWorst,
            string mainIrritant,
            string mainVisualFlaw,
            string mainUXFlaw,
            string mainContentGap,
            bool isBlocker,
            string notes = "")
        {
            return new BuildPlaytestEntry
            {
                Version = NormalizeText(version, "unknown"),
                FpsMean = fpsMean,
                FpsWorst = fpsWorst,
                MainIrritant = NormalizeText(mainIrritant),
                MainVisualFlaw = NormalizeText(mainVisualFlaw),
                MainUXFlaw = NormalizeText(mainUXFlaw),
                MainContentGap = NormalizeText(mainContentGap),
                IsBlocker = isBlocker,
                Notes = NormalizeText(notes),
                CreatedTimestamp = System.DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            };
        }

        /// <summary>
        /// Formatiruet entry dlya logirovaniya v markdown-like format.
        /// </summary>
        public readonly string ToMarkdownEntry()
        {
            string blocker = IsBlocker ? "🔴 BLOCKER" : "✓ OK";
            string timestamp = FormatTimestamp(CreatedTimestamp);

            string markdown = $"## {Version} — {timestamp}" + "\n";
            markdown += $"- **Status:** {blocker}\n";
            markdown += "- **FPS:** Mean=" +
                        FpsMean.ToString("F1", CultureInfo.InvariantCulture) +
                        ", Worst=" +
                        FpsWorst.ToString("F1", CultureInfo.InvariantCulture) +
                        "\n";
            markdown += $"- **Main Irritant:** {MainIrritant}\n";
            markdown += $"- **Visual Flaw:** {MainVisualFlaw}\n";
            markdown += $"- **UX Flaw:** {MainUXFlaw}\n";
            markdown += $"- **Content Gap:** {MainContentGap}\n";

            if (!string.IsNullOrEmpty(Notes))
                markdown += $"- **Notes:** {Notes}\n";

            return markdown;
        }

        public override string ToString()
        {
            return "[" + Version + "] FPS=" +
                   FpsMean.ToString("F1", CultureInfo.InvariantCulture) +
                   "/" +
                   FpsWorst.ToString("F1", CultureInfo.InvariantCulture) +
                   " | Irritant: " + MainIrritant +
                   " | Blocker=" + IsBlocker;
        }

        private static string NormalizeText(string value, string fallback = "")
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string FormatTimestamp(long timestamp)
        {
            if (timestamp <= 0)
                return "uninitialized";

            return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);
        }
    }

    /// <summary>
    /// Globalnyy reestr vseh build playtest entries.
    /// </summary>
    public static class BuildPlaytestLog
    {
        private static readonly System.Collections.Generic.List<BuildPlaytestEntry> _entries =
            new System.Collections.Generic.List<BuildPlaytestEntry>();

        /// <summary>
        /// Dobavlyaet zapis v log.
        /// </summary>
        public static void RecordEntry(BuildPlaytestEntry entry)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _entries.Add(entry);
            Hecton8.Core.H8Debug.Log($"[BuildPlaytestLog] Recorded: {entry}");
#endif
        }

        /// <summary>
        /// Vozvraschaet vse zapisi (dlya eksporta v fayl).
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<BuildPlaytestEntry> GetAllEntries()
        {
            return _entries.AsReadOnly();
        }

        /// <summary>
        /// Eksportiruet vse zapisi v markdown format.
        /// </summary>
        public static string ExportToMarkdown()
        {
            if (_entries.Count == 0)
                return "# Build Playtest Log\n\nNo entries yet.\n";

            string markdown = "# Build Playtest Log\n\n";
            markdown += "Generated: " + System.DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture) + " UTC\n\n";

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                markdown += _entries[i].ToMarkdownEntry() + "\n";
            }

            return markdown;
        }

        /// <summary>
        /// Klirit vse zapisi.
        /// </summary>
        public static void Clear()
        {
            _entries.Clear();
        }
    }
}
