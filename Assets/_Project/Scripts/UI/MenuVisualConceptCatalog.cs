using System;
using Unity.Mathematics;
using UnityEngine;

namespace Hecton8.UI
{
    public enum MenuVisualConcept : byte
    {
        ModuleWindowOverlay = 0,
        CaptainPdaDock = 1,
        HelmetVisorRing = 2,
        BlackboxPlayback = 3,
        SonarPlotter = 4,
        EmergencyBulkheadPanel = 5,
        MaintenanceClipboard = 6,
        CargoManifestBoard = 7,
        DiveLogLedger = 8,
        ReactorConsole = 9,
        TrenchMapTable = 10,
        QuarantineEvidenceWall = 11
    }

    public readonly struct MenuVisualConceptState
    {
        public readonly Vector2 ShellOffset;
        public readonly Vector2 HeaderOffset;
        public readonly Vector2 ContentOffset;
        public readonly Vector2 PanelOffset;
        public readonly float ShellScale;
        public readonly float HeaderScale;
        public readonly float PanelScale;
        public readonly float ShellRotation;
        public readonly float HeaderRotation;
        public readonly float PanelRotation;
        public readonly float PanelSpread;
        public readonly float PanelStack;
        public readonly float MicroMotion;
        public readonly float WarningBias;

        public MenuVisualConceptState(
            Vector2 shellOffset,
            Vector2 headerOffset,
            Vector2 contentOffset,
            Vector2 panelOffset,
            float shellScale,
            float headerScale,
            float panelScale,
            float shellRotation,
            float headerRotation,
            float panelRotation,
            float panelSpread,
            float panelStack,
            float microMotion,
            float warningBias)
        {
            ShellOffset = shellOffset;
            HeaderOffset = headerOffset;
            ContentOffset = contentOffset;
            PanelOffset = panelOffset;
            ShellScale = shellScale;
            HeaderScale = headerScale;
            PanelScale = panelScale;
            ShellRotation = shellRotation;
            HeaderRotation = headerRotation;
            PanelRotation = panelRotation;
            PanelSpread = panelSpread;
            PanelStack = panelStack;
            MicroMotion = microMotion;
            WarningBias = warningBias;
        }
    }

    public static class MenuVisualConceptCatalog
    {
        public const int ConceptCount = 12;

        public static int ToIndex(MenuVisualConcept concept)
        {
            return ClampConceptIndex((int)concept);
        }

        public static MenuVisualConcept FromIndex(int index)
        {
            return (MenuVisualConcept)ClampConceptIndex(index);
        }

        public static int ClampConceptIndex(int index)
        {
            return math.clamp(index, 0, ConceptCount - 1);
        }

        public static bool IsValidConceptIndex(int index)
        {
            return index >= 0 && index < ConceptCount;
        }

        public static ReadOnlySpan<char> GetDisplayName(MenuVisualConcept concept)
        {
            switch (concept)
            {
                case MenuVisualConcept.CaptainPdaDock: return "CAPTAIN PDA DOCK".AsSpan();
                case MenuVisualConcept.HelmetVisorRing: return "HELMET VISOR RING".AsSpan();
                case MenuVisualConcept.BlackboxPlayback: return "BLACKBOX PLAYBACK".AsSpan();
                case MenuVisualConcept.SonarPlotter: return "SONAR PLOTTER".AsSpan();
                case MenuVisualConcept.EmergencyBulkheadPanel: return "EMERGENCY BULKHEAD PANEL".AsSpan();
                case MenuVisualConcept.MaintenanceClipboard: return "MAINTENANCE CLIPBOARD".AsSpan();
                case MenuVisualConcept.CargoManifestBoard: return "CARGO MANIFEST BOARD".AsSpan();
                case MenuVisualConcept.DiveLogLedger: return "DIVE LOG LEDGER".AsSpan();
                case MenuVisualConcept.ReactorConsole: return "REACTOR CONSOLE".AsSpan();
                case MenuVisualConcept.TrenchMapTable: return "TRENCH MAP TABLE".AsSpan();
                case MenuVisualConcept.QuarantineEvidenceWall: return "QUARANTINE EVIDENCE WALL".AsSpan();
                default: return "MODULE WINDOW OVERLAY".AsSpan();
            }
        }

        public static void Resolve(MenuVisualConcept concept, float globalQualityWeight01, out MenuVisualConceptState state)
        {
            float quality = MenuVisualStyleCatalog.Sanitize01(globalQualityWeight01, 1f);
            float eased = quality * quality * (3f - 2f * quality);

            switch (concept)
            {
                case MenuVisualConcept.CaptainPdaDock:
                    state = BuildState(eased, new Vector2(190f, -18f), new Vector2(-18f, 3f), new Vector2(-26f, 0f), new Vector2(-46f, -4f), 0.90f, 0.96f, -0.7f, -0.2f, 0.0f, 18f, 8f, 0.10f, 0.05f);
                    return;
                case MenuVisualConcept.HelmetVisorRing:
                    state = BuildState(eased, Vector2.zero, new Vector2(0f, -5f), Vector2.zero, Vector2.zero, 1.035f, 1.02f, 0.0f, 0.0f, 0.0f, 8f, 4f, 0.34f, 0.08f);
                    return;
                case MenuVisualConcept.BlackboxPlayback:
                    state = BuildState(eased, new Vector2(-145f, -26f), new Vector2(9f, -2f), new Vector2(22f, 0f), new Vector2(34f, -8f), 0.94f, 0.98f, -1.15f, 0.3f, -0.45f, 26f, 14f, 0.22f, 0.16f);
                    return;
                case MenuVisualConcept.SonarPlotter:
                    state = BuildState(eased, new Vector2(0f, 14f), new Vector2(0f, 4f), new Vector2(0f, -4f), new Vector2(0f, 10f), 0.98f, 1.03f, 0.0f, 0.0f, 0.0f, 38f, 20f, 0.28f, 0.04f);
                    return;
                case MenuVisualConcept.EmergencyBulkheadPanel:
                    state = BuildState(eased, new Vector2(0f, -34f), new Vector2(0f, 8f), Vector2.zero, new Vector2(0f, -8f), 1.00f, 1.05f, 0.0f, 0.0f, 0.0f, 10f, 6f, 0.18f, 0.34f);
                    return;
                case MenuVisualConcept.MaintenanceClipboard:
                    state = BuildState(eased, new Vector2(-205f, 18f), new Vector2(12f, -6f), new Vector2(18f, 0f), new Vector2(52f, 0f), 0.88f, 0.97f, -1.8f, -0.3f, -0.6f, 20f, 12f, 0.08f, 0.02f);
                    return;
                case MenuVisualConcept.CargoManifestBoard:
                    state = BuildState(eased, new Vector2(132f, 10f), new Vector2(-12f, 0f), new Vector2(-10f, 0f), new Vector2(-28f, -2f), 0.93f, 0.99f, 0.8f, 0.2f, 0.35f, 30f, 6f, 0.06f, 0.03f);
                    return;
                case MenuVisualConcept.DiveLogLedger:
                    state = BuildState(eased, new Vector2(-86f, 0f), new Vector2(20f, -4f), new Vector2(16f, 0f), new Vector2(38f, -4f), 0.95f, 0.95f, 0.55f, 0.25f, 0.2f, 12f, 18f, 0.05f, 0.02f);
                    return;
                case MenuVisualConcept.ReactorConsole:
                    state = BuildState(eased, new Vector2(0f, -44f), new Vector2(0f, 3f), new Vector2(0f, 8f), new Vector2(0f, -12f), 1.02f, 1.02f, 0.0f, 0.0f, 0.0f, 18f, 12f, 0.30f, 0.18f);
                    return;
                case MenuVisualConcept.TrenchMapTable:
                    state = BuildState(eased, new Vector2(0f, 58f), new Vector2(0f, -8f), new Vector2(0f, -18f), new Vector2(0f, 18f), 0.91f, 0.94f, -0.45f, 0.0f, -0.25f, 44f, 24f, 0.14f, 0.04f);
                    return;
                case MenuVisualConcept.QuarantineEvidenceWall:
                    state = BuildState(eased, new Vector2(72f, 4f), new Vector2(-10f, 5f), new Vector2(-22f, 0f), new Vector2(-24f, -6f), 0.96f, 1.00f, 1.25f, -0.4f, 0.7f, 58f, 28f, 0.22f, 0.26f);
                    return;
                default:
                    state = BuildState(eased, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero, 1.00f, 1.00f, 0.0f, 0.0f, 0.0f, 0f, 0f, 0.04f, 0.00f);
                    return;
            }
        }

        private static MenuVisualConceptState BuildState(
            float quality,
            Vector2 shellOffsetHigh,
            Vector2 headerOffsetHigh,
            Vector2 contentOffsetHigh,
            Vector2 panelOffsetHigh,
            float shellScaleHigh,
            float headerScaleHigh,
            float shellRotationHigh,
            float headerRotationHigh,
            float panelRotationHigh,
            float panelSpreadHigh,
            float panelStackHigh,
            float microMotionHigh,
            float warningBiasHigh)
        {
            return new MenuVisualConceptState(
                shellOffsetHigh * quality,
                headerOffsetHigh * quality,
                contentOffsetHigh * quality,
                panelOffsetHigh * quality,
                math.lerp(1f, shellScaleHigh, quality),
                math.lerp(1f, headerScaleHigh, quality),
                math.lerp(1f, 0.98f, quality * 0.5f),
                shellRotationHigh * quality,
                headerRotationHigh * quality,
                panelRotationHigh * quality,
                panelSpreadHigh * quality,
                panelStackHigh * quality,
                microMotionHigh * quality,
                warningBiasHigh * quality);
        }
    }
}
