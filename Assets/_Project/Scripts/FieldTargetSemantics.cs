using System;
using System.Globalization;
using UnityEngine;

namespace Hecton8.Gameplay
{
    public static class FieldTargetSemantics
    {
        private const string OneDecimalFormat = "0.0";
        private const string MassLabelPrefix = "Mass ";
        private const string KilogramsAtText = " kg at ";
        private const string RangeText = " Range ";
        private const string MetersText = " m";
        private const string MetersPeriodText = " m.";
        private const string PeriodText = ".";
        private const string SafeEnvelopeExceededText = " exceeds the safe envelope.";
        private const string SafeReelEdgeText = " is near the safe reel edge.";
        private const string ReelIntentExceededText = " exceeds reel intent.";

        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
        public readonly struct SemanticAssessment
        {
            public readonly string Headline;
            public readonly string Summary;
            public readonly string Recommendation;
            public readonly string Severity;
            public readonly string Category;

            public SemanticAssessment(string headline, string summary, string recommendation, string severity, string category)
            {
                Headline = headline;
                Summary = summary;
                Recommendation = recommendation;
                Severity = severity;
                Category = category;
            }
        }

        public static bool IsRouteRole(FieldTargetRole role)
        {
            return role == FieldTargetRole.RouteAnchor ||
                   role == FieldTargetRole.RouteRelay ||
                   role == FieldTargetRole.RouteFrontier;
        }

        public static bool IsCargoRole(FieldTargetRole role)
        {
            return role == FieldTargetRole.CargoLight ||
                   role == FieldTargetRole.CargoWork ||
                   role == FieldTargetRole.CargoHeavy ||
                   role == FieldTargetRole.CargoOverweight;
        }

        public static bool IsBioformRole(FieldTargetRole role)
        {
            return role == FieldTargetRole.BioformDormant ||
                   role == FieldTargetRole.BioformAggressive ||
                   role == FieldTargetRole.BioformFractured ||
                   role == FieldTargetRole.BioformDown;
        }

        public static bool IsConstructionRole(FieldTargetRole role)
        {
            return role == FieldTargetRole.ConstructionSocket ||
                   role == FieldTargetRole.ConstructionBlocked ||
                   role == FieldTargetRole.ConstructionClear;
        }

        public static bool IsPowerRole(FieldTargetRole role)
        {
            return role == FieldTargetRole.PowerGeneration ||
                   role == FieldTargetRole.PowerRelay ||
                   role == FieldTargetRole.PowerLoad;
        }

        public static bool TryFindNearestRouteMarkerSq(Vector3 position, float maxDistance, out FieldTargetDescriptor descriptor, out float distanceSq)
        {
            descriptor = null;
            distanceSq = 0f;

            float maxSqr = maxDistance * maxDistance;
            float bestSqr = maxSqr;

            for (int i = 0; i < FieldTargetDescriptor.ActiveCount; i++)
            {
                FieldTargetDescriptor candidate = FieldTargetDescriptor.GetActiveDescriptorAt(i);
                if (candidate == null || !IsRouteRole(candidate.Role))
                    continue;

                Vector3 markerVisualPosition = candidate.transform.position;
                Vector3 visualDeltaToMarker = markerVisualPosition - position;
                float sqr = visualDeltaToMarker.sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    descriptor = candidate;
                }
            }

            if (descriptor == null)
                return false;

            distanceSq = bestSqr;
            return true;
        }

        public static string BuildRouteRoleLabel(FieldTargetRole role)
        {
            return role switch
            {
                FieldTargetRole.RouteAnchor => "ANCHOR",
                FieldTargetRole.RouteRelay => "RELAY",
                FieldTargetRole.RouteFrontier => "FRONTIER",
                _ => "LOCAL MARK"
            };
        }

        public static string BuildRouteRecommendation(FieldTargetRole role)
        {
            return role switch
            {
                FieldTargetRole.RouteAnchor => "Use this point to stabilize your return route before extending deeper.",
                FieldTargetRole.RouteRelay => "Bridge the lane with a relay beacon and keep the return path readable.",
                FieldTargetRole.RouteFrontier => "Confirm supplies, then push this beacon outward as a deep frontier marker.",
                _ => "Use this route marker to keep navigation readable."
            };
        }

        public static string BuildDescriptorSummary(FieldTargetDescriptor descriptor, string fallback)
        {
            if (descriptor == null)
                return fallback;

            return string.IsNullOrWhiteSpace(descriptor.OperatorNote)
                ? fallback
                : descriptor.OperatorNote;
        }

        public static bool TryBuildFlashlightDirective(FieldTargetDescriptor descriptor, float distance, out string directive)
        {
            directive = null;
            if (descriptor == null)
                return false;

            switch (descriptor.Role)
            {
                case FieldTargetRole.RouteAnchor:
                    directive = "Use STANDARD to hold a calm, readable return origin around the anchor point.";
                    return true;
                case FieldTargetRole.RouteRelay:
                    directive = distance >= 10f
                        ? "Use FOCUS to keep the relay visible through the full lane."
                        : "Use STANDARD to keep the relay readable while you move through it.";
                    return true;
                case FieldTargetRole.RouteFrontier:
                    directive = distance >= 10f
                        ? "Use FOCUS before pushing into the frontier marker."
                        : "Use STANDARD only if the frontier route is already secure.";
                    return true;
                case FieldTargetRole.CargoLight:
                case FieldTargetRole.CargoWork:
                    directive = distance <= 6f
                        ? "Use FLOOD while handling cargo so nearby obstacles stay visible."
                        : "Use STANDARD until the cargo lane compresses.";
                    return true;
                case FieldTargetRole.CargoHeavy:
                case FieldTargetRole.CargoOverweight:
                    directive = distance >= 8f
                        ? "Use FOCUS to read the heavy obstacle before committing another tool."
                        : "Use STANDARD and keep space for rebounds or rerouting.";
                    return true;
                case FieldTargetRole.ServiceDamaged:
                    directive = distance >= 8f
                        ? "Use FOCUS to inspect the damaged module before committing repair work."
                        : "Use STANDARD to keep the damaged service face readable during repair.";
                    return true;
                case FieldTargetRole.ServiceFlooded:
                    directive = distance >= 8f
                        ? "Use FOCUS to inspect the flooded module and plan the service approach."
                        : "Use STANDARD to hold the flooded module in view for repair and drainage.";
                    return true;
                case FieldTargetRole.ServiceControl:
                    directive = "Use STANDARD to compare the control module against damaged service targets.";
                    return true;
                case FieldTargetRole.ConstructionSocket:
                    directive = "Use STANDARD to read the socket line before locking a snapped placement.";
                    return true;
                case FieldTargetRole.ConstructionBlocked:
                    directive = distance >= 8f
                        ? "Use FOCUS to inspect the obstruction before committing to a build route."
                        : "Use FLOOD or STANDARD to read the blocked build space and clear the obstruction.";
                    return true;
                case FieldTargetRole.ConstructionClear:
                    directive = "Use STANDARD while surveying the clear build lane and module footprint.";
                    return true;
                case FieldTargetRole.PowerGeneration:
                    directive = distance >= 8f
                        ? "Use FOCUS to read the exposed turbine face before committing service work."
                        : "Use STANDARD to keep the generator body readable while you stage support modules.";
                    return true;
                case FieldTargetRole.PowerRelay:
                    directive = "Use STANDARD to trace the relay line and keep the support route readable.";
                    return true;
                case FieldTargetRole.PowerLoad:
                    directive = distance >= 8f
                        ? "Use FOCUS to inspect the powered load before you commit repair or routing work."
                        : "Use STANDARD to keep the load face and service connections readable.";
                    return true;
            }

            return false;
        }

        public static bool TryBuildAnalyzerAssessment(FieldTargetDescriptor descriptor, float distance, float? mass, out SemanticAssessment assessment)
        {
            assessment = default;
            if (descriptor == null)
                return false;

            string note = BuildDescriptorSummary(descriptor, "Authored field target detected.");

            switch (descriptor.Role)
            {
                case FieldTargetRole.RouteAnchor:
                case FieldTargetRole.RouteRelay:
                case FieldTargetRole.RouteFrontier:
                    assessment = new SemanticAssessment(
                        BuildRouteRoleLabel(descriptor.Role),
                        note,
                        BuildRouteRecommendation(descriptor.Role),
                        descriptor.Role == FieldTargetRole.RouteFrontier ? "WARN" : "INFO",
                        "Navigation");
                    return true;

                case FieldTargetRole.CargoLight:
                case FieldTargetRole.CargoWork:
                case FieldTargetRole.CargoHeavy:
                case FieldTargetRole.CargoOverweight:
                {
                    string cargoHeadline = descriptor.Role switch
                    {
                        FieldTargetRole.CargoLight => "PRECISION CARGO",
                        FieldTargetRole.CargoWork => "WORKLOAD CARGO",
                        FieldTargetRole.CargoHeavy => "HEAVY CARGO",
                        _ => "OVERWEIGHT CARGO"
                    };

                    string cargoSeverity = descriptor.Role == FieldTargetRole.CargoOverweight || descriptor.Role == FieldTargetRole.CargoHeavy
                        ? "WARN"
                        : "INFO";
                    string cargoRecommendation = descriptor.Role switch
                    {
                        FieldTargetRole.CargoLight => "Propulsion or harpoon handling is ideal here.",
                        FieldTargetRole.CargoWork => "Use tractor positioning or short pull cycles.",
                        FieldTargetRole.CargoHeavy => "Expect sluggish handling and protect the return lane.",
                        _ => "Avoid forced handling. Reroute or break the obstacle down."
                    };

                    assessment = new SemanticAssessment(
                        cargoHeadline,
                        note,
                        cargoRecommendation,
                        cargoSeverity,
                        "Logistics");
                    return true;
                }

                case FieldTargetRole.ServiceDamaged:
                case FieldTargetRole.ServiceFlooded:
                case FieldTargetRole.ServiceControl:
                {
                    string serviceHeadline = descriptor.Role switch
                    {
                        FieldTargetRole.ServiceDamaged => "DAMAGED SERVICE MODULE",
                        FieldTargetRole.ServiceFlooded => "FLOODED SERVICE MODULE",
                        _ => "CONTROL SERVICE MODULE"
                    };

                    string serviceSeverity = descriptor.Role == FieldTargetRole.ServiceFlooded
                        ? "WARN"
                        : "INFO";

                    string serviceRecommendation = descriptor.Role switch
                    {
                        FieldTargetRole.ServiceDamaged => "Repair tool and construction support are the best fit here.",
                        FieldTargetRole.ServiceFlooded => "Repair first, then stabilize power and drainage before reuse.",
                        _ => "Use this module as a clean baseline for service comparison and route planning."
                    };

                    assessment = new SemanticAssessment(
                        serviceHeadline,
                        note,
                        serviceRecommendation,
                        serviceSeverity,
                        "Structure");
                    return true;
                }

                case FieldTargetRole.BioformDormant:
                case FieldTargetRole.BioformAggressive:
                case FieldTargetRole.BioformFractured:
                case FieldTargetRole.BioformDown:
                {
                    string bioHeadline = descriptor.Role switch
                    {
                        FieldTargetRole.BioformDormant => "DORMANT BIOFORM",
                        FieldTargetRole.BioformAggressive => "AGGRESSIVE BIOFORM",
                        FieldTargetRole.BioformFractured => "FRACTURED BIOFORM",
                        _ => "DOWNED BIOFORM"
                    };

                    string bioSeverity = descriptor.Role switch
                    {
                        FieldTargetRole.BioformAggressive => "CRITICAL",
                        FieldTargetRole.BioformFractured => "WARN",
                        _ => "INFO"
                    };

                    string bioRecommendation = descriptor.Role switch
                    {
                        FieldTargetRole.BioformDormant => "Open with scanner, analyzer, or a quiet stun before wake-up.",
                        FieldTargetRole.BioformAggressive => "Control the contact first. Keep range or chain a stun window.",
                        FieldTargetRole.BioformFractured => "High-value finish window. Knife, harpoon, or stun follow-up is viable.",
                        _ => "Threat is neutralized. Recover samples or clear the lane."
                    };

                    assessment = new SemanticAssessment(
                        bioHeadline,
                        note,
                        bioRecommendation,
                        bioSeverity,
                        "Bioform");
                    return true;
                }

                case FieldTargetRole.ConstructionSocket:
                case FieldTargetRole.ConstructionBlocked:
                case FieldTargetRole.ConstructionClear:
                {
                    string buildHeadline = descriptor.Role switch
                    {
                        FieldTargetRole.ConstructionSocket => "SOCKET BUILD SITE",
                        FieldTargetRole.ConstructionBlocked => "BLOCKED BUILD SITE",
                        _ => "CLEAR BUILD SITE"
                    };

                    string buildSeverity = descriptor.Role == FieldTargetRole.ConstructionBlocked
                        ? "WARN"
                        : "INFO";

                    string buildRecommendation = descriptor.Role switch
                    {
                        FieldTargetRole.ConstructionSocket => "Builder snap placement is the value play here. Align to the socket before deploying.",
                        FieldTargetRole.ConstructionBlocked => "Construction route is obstructed. Clear the blocker or reposition before deployment.",
                        _ => "Space is clear for free placement. Builder and construction kit are recommended."
                    };

                    assessment = new SemanticAssessment(
                        buildHeadline,
                        note,
                        buildRecommendation,
                        buildSeverity,
                        "Construction");
                    return true;
                }

                case FieldTargetRole.PowerGeneration:
                case FieldTargetRole.PowerRelay:
                case FieldTargetRole.PowerLoad:
                {
                    string powerHeadline = descriptor.Role switch
                    {
                        FieldTargetRole.PowerGeneration => "POWER GENERATION NODE",
                        FieldTargetRole.PowerRelay => "POWER RELAY NODE",
                        _ => "POWER LOAD NODE"
                    };

                    string powerSeverity = descriptor.Role == FieldTargetRole.PowerLoad
                        ? "WARN"
                        : "INFO";

                    string powerRecommendation = descriptor.Role switch
                    {
                        FieldTargetRole.PowerGeneration => "Expose and protect this generator. Turbine support is the value play here.",
                        FieldTargetRole.PowerRelay => "Keep this relay path stable. Pylon routing and clean service access fit this lane well.",
                        _ => "This load will lean on power support. Pair pump or service work with a stable upstream supply."
                    };

                    assessment = new SemanticAssessment(
                        powerHeadline,
                        note,
                        powerRecommendation,
                        powerSeverity,
                        "Power");
                    return true;
                }
            }

            return false;
        }

        public static bool TryBuildPropulsionAssessment(FieldTargetDescriptor descriptor, float distance, float mass, bool tractorIntent, out SemanticAssessment assessment)
        {
            assessment = default;
            if (descriptor == null || !IsCargoRole(descriptor.Role))
                return false;

            string note = BuildDescriptorSummary(descriptor, "Authored cargo trial target.");
            switch (descriptor.Role)
            {
                case FieldTargetRole.CargoLight:
                    assessment = new SemanticAssessment(
                        tractorIntent ? "TRACTOR - PRECISION CARGO" : "PROPULSION - PRECISION CARGO",
                        BuildMassDistanceSentence(note, includeMassLabel: true, mass, distance, PeriodText),
                        tractorIntent ? "Lock it and walk it through hazards or narrow gaps." : "Use short pulses to clear the lane without overshooting.",
                        "INFO",
                        "Logistics");
                    return true;
                case FieldTargetRole.CargoWork:
                    assessment = new SemanticAssessment(
                        tractorIntent ? "TRACTOR - WORK CRATE" : "PROPULSION - WORK CRATE",
                        BuildMassDistanceSentence(note, includeMassLabel: true, mass, distance, PeriodText),
                        tractorIntent ? "Stable lock is expected. Reposition it with deliberate holds." : "Push in measured bursts and keep the return path open.",
                        "INFO",
                        "Logistics");
                    return true;
                case FieldTargetRole.CargoHeavy:
                    assessment = new SemanticAssessment(
                        tractorIntent ? "TRACTOR - HEAVY SALVAGE" : "PROPULSION - HEAVY SALVAGE",
                        BuildMassDistanceSentence(note, includeMassLabel: true, mass, distance, PeriodText),
                        tractorIntent ? "Keep the hold distance steady and expect sluggish correction." : "Use controlled pulses only. Avoid rebounds in tight spaces.",
                        "WARN",
                        "Logistics");
                    return true;
                case FieldTargetRole.CargoOverweight:
                    assessment = new SemanticAssessment(
                        tractorIntent ? "TRACTOR - OVERWEIGHT LOAD" : "PROPULSION - OVERWEIGHT LOAD",
                        BuildMassDistanceSentence(note, includeMassLabel: true, mass, distance, SafeEnvelopeExceededText),
                        "Do not force this lane. Reroute, deconstruct, or break the problem into lighter pieces.",
                        "WARN",
                        "Logistics");
                    return true;
            }

            return false;
        }

        public static bool TryBuildHarpoonAssessment(FieldTargetDescriptor descriptor, float distance, float mass, bool tetherReady, out SemanticAssessment assessment)
        {
            assessment = default;
            if (descriptor == null)
                return false;

            string note = BuildDescriptorSummary(descriptor, "Authored harpoon lane target.");
            switch (descriptor.Role)
            {
                case FieldTargetRole.CargoLight:
                    assessment = new SemanticAssessment(
                        tetherReady ? "HARPOON - LIGHT CARGO TETHERED" : "HARPOON - LIGHT CARGO",
                        BuildMassDistanceSentence(note, includeMassLabel: false, mass, distance, PeriodText),
                        tetherReady ? "Reel it quickly to recover or clear the lane." : "One clean shot is enough if you need a fast tether.",
                        "INFO",
                        "Logistics");
                    return true;
                case FieldTargetRole.CargoWork:
                    assessment = new SemanticAssessment(
                        tetherReady ? "HARPOON - WORKLOAD TETHERED" : "HARPOON - WORKLOAD CONTACT",
                        BuildMassDistanceSentence(note, includeMassLabel: false, mass, distance, PeriodText),
                        tetherReady ? "Use steady reels to reposition the crate without overcommitting." : "Confirm the tether before moving through the lane.",
                        "INFO",
                        "Logistics");
                    return true;
                case FieldTargetRole.CargoHeavy:
                    assessment = new SemanticAssessment(
                        tetherReady ? "HARPOON - HEAVY CARGO TETHERED" : "HARPOON - HEAVY CARGO",
                        BuildMassDistanceSentence(note, includeMassLabel: false, mass, distance, SafeReelEdgeText),
                        tetherReady ? "Reel only to stabilize. Switch to propulsion for major repositioning." : "Use the shot to tag and control, not to drag the load far.",
                        "WARN",
                        "Logistics");
                    return true;
                case FieldTargetRole.CargoOverweight:
                    assessment = new SemanticAssessment(
                        "HARPOON - OVERWEIGHT LOAD",
                        BuildMassDistanceSentence(note, includeMassLabel: false, mass, distance, ReelIntentExceededText),
                        "Do not waste line tension here. Use propulsion planning or route around it.",
                        "WARN",
                        "Logistics");
                    return true;
                case FieldTargetRole.BioformDormant:
                    assessment = new SemanticAssessment(
                        tetherReady ? "HARPOON - DORMANT TARGET TETHERED" : "HARPOON - DORMANT CONTACT",
                        BuildRangeSentence(note, distance),
                        tetherReady ? "Control the contact before wake-up." : "Clean opener is available if you need a controlled pull.",
                        "INFO",
                        "Bioform");
                    return true;
                case FieldTargetRole.BioformAggressive:
                    assessment = new SemanticAssessment(
                        tetherReady ? "HARPOON - HOSTILE TETHERED" : "HARPOON - HOSTILE CONTACT",
                        BuildRangeSentence(note, distance),
                        tetherReady ? "Use the line to manage spacing and stop the rush." : "Control first, then decide whether to reel or disengage.",
                        "CRITICAL",
                        "Bioform");
                    return true;
                case FieldTargetRole.BioformFractured:
                    assessment = new SemanticAssessment(
                        tetherReady ? "HARPOON - FRACTURED TARGET TETHERED" : "HARPOON - FRACTURED TARGET",
                        BuildRangeSentence(note, distance),
                        tetherReady ? "Short controlled reels are safe while the target is weak." : "Finish window is open. Reel or strike before the lane changes.",
                        "WARN",
                        "Bioform");
                    return true;
                case FieldTargetRole.BioformDown:
                    assessment = new SemanticAssessment(
                        "HARPOON - TARGET DOWN",
                        BuildRangeSentence(note, distance),
                        "Threat is neutralized. Use the line only if recovery or repositioning matters.",
                        "INFO",
                        "Bioform");
                    return true;
            }

            return false;
        }

        public static bool TryBuildStunAssessment(FieldTargetDescriptor descriptor, float distance, out SemanticAssessment assessment)
        {
            assessment = default;
            if (descriptor == null || !IsBioformRole(descriptor.Role))
                return false;

            string note = BuildDescriptorSummary(descriptor, "Authored stun trial target.");
            switch (descriptor.Role)
            {
                case FieldTargetRole.BioformDormant:
                    assessment = new SemanticAssessment(
                        "STUN PISTOL - DORMANT CONTACT",
                        BuildRangeSentence(note, distance),
                        "Take the shot now or pass quietly before wake-up.",
                        "INFO",
                        "Bioform");
                    return true;
                case FieldTargetRole.BioformAggressive:
                    assessment = new SemanticAssessment(
                        "STUN PISTOL - AGGRESSIVE THREAT",
                        BuildRangeSentence(note, distance),
                        "Disrupt immediately, then create distance.",
                        "CRITICAL",
                        "Bioform");
                    return true;
                case FieldTargetRole.BioformFractured:
                    assessment = new SemanticAssessment(
                        "STUN PISTOL - FRACTURED TARGET",
                        BuildRangeSentence(note, distance),
                        "Stun now if you want a clean finish or safe bypass.",
                        "WARN",
                        "Bioform");
                    return true;
                case FieldTargetRole.BioformDown:
                    assessment = new SemanticAssessment(
                        "STUN PISTOL - TARGET DOWN",
                        BuildRangeSentence(note, distance),
                        "No disruption needed. Recover, scan, or move on.",
                        "INFO",
                        "Bioform");
                    return true;
            }

            return false;
        }

        public static bool TryBuildKnifeAssessment(FieldTargetDescriptor descriptor, float distance, out SemanticAssessment assessment)
        {
            assessment = default;
            if (descriptor == null || !IsBioformRole(descriptor.Role))
                return false;

            string note = BuildDescriptorSummary(descriptor, "Authored close-quarters contact.");
            switch (descriptor.Role)
            {
                case FieldTargetRole.BioformDormant:
                    assessment = new SemanticAssessment(
                        "BLADE READ - DORMANT BIOFORM",
                        BuildRangeSentence(note, distance),
                        "Quiet opener is possible, but do not wake it without a plan.",
                        "INFO",
                        "Bioform");
                    return true;
                case FieldTargetRole.BioformAggressive:
                    assessment = new SemanticAssessment(
                        "BLADE READ - HOSTILE",
                        BuildRangeSentence(note, distance),
                        "Do not stay in close range unless you are finishing or forced to commit.",
                        "WARN",
                        "Bioform");
                    return true;
                case FieldTargetRole.BioformFractured:
                    assessment = new SemanticAssessment(
                        "BLADE READ - FRACTURED TARGET",
                        BuildRangeSentence(note, distance),
                        "Precision strike window is open if you need a quick finish.",
                        "INFO",
                        "Bioform");
                    return true;
                case FieldTargetRole.BioformDown:
                    assessment = new SemanticAssessment(
                        "BLADE READ - TARGET DOWN",
                        BuildRangeSentence(note, distance),
                        "Switch tools. The blade is no longer the value play here.",
                        "INFO",
                        "Bioform");
                    return true;
            }

            return false;
        }

        private static string BuildMassDistanceSentence(
            string note,
            bool includeMassLabel,
            float mass,
            float distance,
            string suffix)
        {
            note ??= string.Empty;
            suffix ??= string.Empty;
            int massLength = GetSingleDecimalLength(mass);
            int distanceLength = GetSingleDecimalLength(distance);
            int length = note.Length +
                         1 +
                         (includeMassLabel ? MassLabelPrefix.Length : 0) +
                         massLength +
                         KilogramsAtText.Length +
                         distanceLength +
                         MetersText.Length +
                         suffix.Length;

            return string.Create(
                length,
                (note, includeMassLabel, mass, distance, suffix),
                static (span, state) =>
                {
                    int cursor = 0;
                    cursor += CopyString(state.note, span, cursor);
                    span[cursor++] = ' ';
                    if (state.includeMassLabel)
                        cursor += CopyString(MassLabelPrefix, span, cursor);

                    cursor += WriteSingleDecimal(state.mass, span, cursor);
                    cursor += CopyString(KilogramsAtText, span, cursor);
                    cursor += WriteSingleDecimal(state.distance, span, cursor);
                    cursor += CopyString(MetersText, span, cursor);
                    CopyString(state.suffix, span, cursor);
                });
        }

        private static string BuildRangeSentence(string note, float distance)
        {
            note ??= string.Empty;
            int distanceLength = GetSingleDecimalLength(distance);
            int length = note.Length + RangeText.Length + distanceLength + MetersPeriodText.Length;

            return string.Create(
                length,
                (note, distance),
                static (span, state) =>
                {
                    int cursor = 0;
                    cursor += CopyString(state.note, span, cursor);
                    cursor += CopyString(RangeText, span, cursor);
                    cursor += WriteSingleDecimal(state.distance, span, cursor);
                    CopyString(MetersPeriodText, span, cursor);
                });
        }

        private static int GetSingleDecimalLength(float value)
        {
            Span<char> buffer = stackalloc char[32];
            return value.TryFormat(buffer, out int written, OneDecimalFormat.AsSpan(), CultureInfo.CurrentCulture)
                ? written
                : 0;
        }

        private static int WriteSingleDecimal(float value, Span<char> destination, int offset)
        {
            return value.TryFormat(destination.Slice(offset), out int written, OneDecimalFormat.AsSpan(), CultureInfo.CurrentCulture)
                ? written
                : 0;
        }

        private static int CopyString(string source, Span<char> destination, int offset)
        {
            source.AsSpan().CopyTo(destination.Slice(offset));
            return source.Length;
        }
    }
}
