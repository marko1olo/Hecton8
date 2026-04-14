// ============================================================================
// HECTON-8 — BuildPlaytestEntry.cs
// Структура данных для записи результатов каждой сборки.
//
// НАЗНАЧЕНИЕ:
//   Каждый build должен заполнить этот контракт перед отправкой на playtest:
//   - Версия (дата + хеш + номер сборки)
//   - FPS feel (mean / worst / hitch 60→30 момент)
//   - Главная раздражавшая проблема
//   - Главный визуальный дефект
//   - Главный UX дефект
//   - Главный контент гап
//   - Blocker: да/нет
//
// ИСПОЛЬЗОВАНИЕ:
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
//   BuildPlaytestLog.Instance.RecordEntry(entry);
//
// ЗОЛОТОЙ СТАНДАРТ:
//   • Каждый build = одна запись
//   • Записывается ПЕРЕД отправкой на playtest
//   • Входит в BUILD_PLAYTEST_ISSUES.md как history
//   • Используется для отслеживания progress между релизами
// ============================================================================

using System;
using UnityEngine;

namespace Hecton8.BuildTools
{
    /// <summary>
    /// Контракт для записи результатов одного build playtest.
    /// </summary>
    [Serializable]
    public struct BuildPlaytestEntry
    {
        /// <summary>Версия сборки (дата-ветка-хеш).</summary>
        [Tooltip("Версия сборки: YYYY-MM-DD-branch-commit")]
        public string Version;

        /// <summary>Средний FPS за 10 минут тестирования.</summary>
        [Tooltip("Средний FPS за тестовый прогулку")]
        public float FpsMean;

        /// <summary>Худший (минимальный) FPS в момент пика нагрузки.</summary>
        [Tooltip("Худший однократный frame за тест")]
        public float FpsWorst;

        /// <summary>Главная раздражающая проблема, которая испортила опыт.</summary>
        [Tooltip("Что больше всего раздражало при игре")]
        public string MainIrritant;

        /// <summary>Главный визуальный дефект (шейдер, LOD, артефакт).</summary>
        [Tooltip("Главный визуальный баг: газовый гигант, размытие, LOD pop, и т.д.")]
        public string MainVisualFlaw;

        /// <summary>Главный UX дефект (надпись, кнопка, меню).</summary>
        [Tooltip("Главный UX баг: неясная кнопка, неправильный текст, пропущенное меню")]
        public string MainUXFlaw;

        /// <summary>Главный контент gap (отсутствует флора, враги, система).</summary>
        [Tooltip("Что отсутствует контентом: вода слишком пуста, нет врагов, и т.д.")]
        public string MainContentGap;

        /// <summary>Является ли эта проблема блокером для следующего релиза.</summary>
        [Tooltip("Блокирует ли это приемку билда")]
        public bool IsBlocker;

        /// <summary>Дополнительные примечания (опционально).</summary>
        [Tooltip("Любые дополнительные заметки")]
        public string Notes;

        /// <summary>Timestamp когда entry была создана.</summary>
        public long CreatedTimestamp { get; private set; }

        /// <summary>Показывает, был ли entry создан через фабрику и получил timestamp.</summary>
        public readonly bool HasRecordedTimestamp => CreatedTimestamp > 0;

        /// <summary>
        /// Создает новую запись с текущим временем.
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
        /// Форматирует entry для логирования в markdown-like формат.
        /// </summary>
        public readonly string ToMarkdownEntry()
        {
            string blocker = IsBlocker ? "🔴 BLOCKER" : "✓ OK";
            string timestamp = FormatTimestamp(CreatedTimestamp);

            string markdown = $"## {Version} — {timestamp}" + "\n";
            markdown += $"- **Status:** {blocker}\n";
            markdown += $"- **FPS:** Mean={FpsMean:F1}, Worst={FpsWorst:F1}\n";
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
            return $"[{Version}] FPS={FpsMean:F1}/{FpsWorst:F1} | Irritant: {MainIrritant} | Blocker={IsBlocker}";
        }

        private static string NormalizeText(string value, string fallback = "")
        {
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static string FormatTimestamp(long timestamp)
        {
            if (timestamp <= 0)
                return "uninitialized";

            return DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.ToString("yyyy-MM-dd HH:mm");
        }
    }

    /// <summary>
    /// Глобальный реестр всех build playtest entries.
    /// </summary>
    public static class BuildPlaytestLog
    {
        private static readonly System.Collections.Generic.List<BuildPlaytestEntry> _entries =
            new System.Collections.Generic.List<BuildPlaytestEntry>();

        /// <summary>
        /// Добавляет запись в лог.
        /// </summary>
        public static void RecordEntry(BuildPlaytestEntry entry)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _entries.Add(entry);
            Debug.Log($"[BuildPlaytestLog] Recorded: {entry}");
#endif
        }

        /// <summary>
        /// Возвращает все записи (для экспорта в файл).
        /// </summary>
        public static System.Collections.Generic.IReadOnlyList<BuildPlaytestEntry> GetAllEntries()
        {
            return _entries.AsReadOnly();
        }

        /// <summary>
        /// Экспортирует все записи в markdown формат.
        /// </summary>
        public static string ExportToMarkdown()
        {
            if (_entries.Count == 0)
                return "# Build Playtest Log\n\nNo entries yet.\n";

            string markdown = "# Build Playtest Log\n\n";
            markdown += $"Generated: {System.DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC\n\n";

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                markdown += _entries[i].ToMarkdownEntry() + "\n";
            }

            return markdown;
        }

        /// <summary>
        /// Клирит все записи.
        /// </summary>
        public static void Clear()
        {
            _entries.Clear();
        }
    }
}
