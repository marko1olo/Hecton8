using System;
using Hecton.Localization;
using Hecton8.Core;
using Hecton8.Core.Contracts.Signals;
using Hecton8.Core.Generated;
using Hecton8.World;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;

namespace Hecton8.Data
{
    /// <summary>
    /// Zero-allocation runtime facade for applied lore packets baked into static_data.h8bin.
    /// </summary>
    public static class H8AppliedLoreRuntime
    {
        public const uint DefaultLocaleHash = H8AppliedLoreHashes.Locale_en_US;
        public const uint UnlockSourceId = 0x41504C52u; // APLR
        public const byte WorldImpactFlagBiome = 1 << 0;
        public const byte WorldImpactFlagAcoustic = 1 << 1;
        private const uint AcousticGhostSourceHash = 0x414C4748u; // ALGH
        private const float MaxLoreDepthMeters = 5000f;
        private const double DefaultSeaLevelY = 14.02d;
        private static int s_appliedLoreSignalPushDropCount;

        public static uint ResolveLocaleHash(GameLanguage language)
        {
            switch (language)
            {
                case GameLanguage.Russian:
                    return H8AppliedLoreHashes.Locale_ru_RU;
                case GameLanguage.German:
                    return H8AppliedLoreHashes.Locale_de_DE;
                case GameLanguage.French:
                    return H8AppliedLoreHashes.Locale_fr_FR;
                case GameLanguage.Spanish:
                    return H8AppliedLoreHashes.Locale_es_ES;
                case GameLanguage.PortugueseBrazilian:
                    return H8AppliedLoreHashes.Locale_pt_BR;
                case GameLanguage.Polish:
                    return H8AppliedLoreHashes.Locale_pl_PL;
                case GameLanguage.Ukrainian:
                    return H8AppliedLoreHashes.Locale_uk_UA;
                case GameLanguage.ChineseSimplified:
                case GameLanguage.ChineseTraditional:
                    return H8AppliedLoreHashes.Locale_zh_CN;
                case GameLanguage.Japanese:
                    return H8AppliedLoreHashes.Locale_ja_JP;
                case GameLanguage.Korean:
                    return H8AppliedLoreHashes.Locale_ko_KR;
                case GameLanguage.Indonesian:
                    return H8AppliedLoreHashes.Locale_id_ID;
                case GameLanguage.Arabic:
                    return H8AppliedLoreHashes.Locale_ar_SA;
                case GameLanguage.Hebrew:
                    return H8AppliedLoreHashes.Locale_he_IL;
                case GameLanguage.Dutch:
                    return H8AppliedLoreHashes.Locale_nl_NL;
                default:
                    return DefaultLocaleHash;
            }
        }

        public static bool ValidateRuntimeLayout()
        {
            int packetBytes = UnsafeUtility.SizeOf<H8AppliedLorePacketRecord>();
            int routeBytes = UnsafeUtility.SizeOf<H8AppliedLoreRouteRecord>();
            int worldImpactBytes = UnsafeUtility.SizeOf<H8AppliedLoreWorldImpactRecord>();
            int loreFragmentSignalBytes = UnsafeUtility.SizeOf<LoreFragmentScannedSignal>();
            int terminalPreviewSignalBytes = UnsafeUtility.SizeOf<AppliedLoreTerminalPreviewSignal>();
            return packetBytes == H8DataLayoutConstants.AppliedLorePacketRecordSize &&
                   (packetBytes & 7) == 0 &&
                   routeBytes == H8DataLayoutConstants.AppliedLoreRouteRecordSize &&
                   (routeBytes & 7) == 0 &&
                   worldImpactBytes == H8AppliedLoreWorldImpactRecord.SizeBytes &&
                   (worldImpactBytes & 7) == 0 &&
                   loreFragmentSignalBytes == LoreFragmentScannedSignal.SizeBytes &&
                   (loreFragmentSignalBytes & 7) == 0 &&
                   terminalPreviewSignalBytes == AppliedLoreTerminalPreviewSignal.SizeBytes &&
                   (terminalPreviewSignalBytes & 7) == 0 &&
                   AppliedLoreTerminalPreviewSignal.LowTierFrameSignals > 0 &&
                   AppliedLoreTerminalPreviewSignal.LowTierFrameSignals <= AppliedLoreTerminalPreviewSignal.MaxFrameSignals &&
                   AppliedLoreTerminalPreviewSignal.MaxFrameSignals <= AppliedLoreTerminalPreviewSignal.ExpectedCapacity &&
                   AppliedLoreTerminalPreviewSignal.LaneHash == 0x41545056u;
        }

        public static bool TryGetUtf8(
            uint packetHash,
            uint localeHash,
            H8AppliedLoreSurface surface,
            out ReadOnlySpan<byte> utf8Bytes)
        {
            utf8Bytes = ReadOnlySpan<byte>.Empty;
            return TryFindPacket(packetHash, localeHash, out H8AppliedLorePacketRecord record) &&
                   H8StaticDataArena.TryGetAppliedLoreUtf8(record, surface, out utf8Bytes);
        }

        /// <remarks>
        /// The record reference never reaches the returned span: the record is a pure offset/length
        /// table and is handed to the arena by value, and the arena builds the span over its own
        /// long-lived static buffer with <c>MemoryMarshal.CreateReadOnlySpan</c>. The compiler
        /// cannot see that, so it conservatively ties the out span's escape scope to this
        /// <c>in</c> reference and every caller holding the record in a local reports CS9091
        /// ("returns local by reference") even though nothing can dangle.
        ///
        /// The `scoped` modifier is the right way to say so, but it is a C# 11 feature and Unity
        /// 6000.5 compiles this project at C# 9 - it was a hard CS8773 build error, not a warning,
        /// and it broke every assembly in the project. So CS9091 stands as a warning here until the
        /// project moves to C# 11, at which point `scoped` should come back. It is deliberately NOT
        /// suppressed in Assets/csc.rsp: that file is gitignored, so a suppression there would only
        /// silence the warning on one machine and quietly make that build differ from everyone
        /// else's.
        /// </remarks>
        public static bool TryGetUtf8(
            in H8AppliedLorePacketRecord record,
            H8AppliedLoreSurface surface,
            out ReadOnlySpan<byte> utf8Bytes)
        {
            return H8StaticDataArena.TryGetAppliedLoreUtf8(record, surface, out utf8Bytes);
        }

        public static bool TryFindPacket(
            uint packetHash,
            uint localeHash,
            out H8AppliedLorePacketRecord record)
        {
            record = default;
            if (packetHash == 0u)
                return false;

            uint resolvedLocale = localeHash != 0u ? localeHash : DefaultLocaleHash;
            if (H8StaticDataArena.TryFindAppliedLorePacket(packetHash, resolvedLocale, out record))
                return true;

            return resolvedLocale != DefaultLocaleHash &&
                   H8StaticDataArena.TryFindAppliedLorePacket(packetHash, DefaultLocaleHash, out record);
        }

        public static ReadOnlySpan<H8AppliedLorePacketRecord> GetPacketRecords()
        {
            return H8StaticDataArena.GetSectionSpan<H8AppliedLorePacketRecord>(H8DataSectionId.AppliedLorePackets);
        }

        public static bool TryWriteTitleUtf16(
            uint packetHash,
            uint localeHash,
            Span<char> destination,
            out int written)
        {
            written = 0;
            return destination.Length > 0 &&
                   TryGetUtf8(packetHash, localeHash, H8AppliedLoreSurface.Title, out ReadOnlySpan<byte> utf8Bytes) &&
                   TryDecodeUtf8ToChars(utf8Bytes, destination, out written);
        }

        public static bool TryWriteTitleUtf16(
            in H8AppliedLorePacketRecord record,
            Span<char> destination,
            out int written)
        {
            return TryWriteSurfaceUtf16(in record, H8AppliedLoreSurface.Title, destination, out written);
        }

        public static bool TryWriteSurfaceUtf16(
            uint packetHash,
            uint localeHash,
            H8AppliedLoreSurface surface,
            Span<char> destination,
            out int written)
        {
            written = 0;
            return destination.Length > 0 &&
                   TryGetUtf8(packetHash, localeHash, surface, out ReadOnlySpan<byte> utf8Bytes) &&
                   TryDecodeUtf8ToChars(utf8Bytes, destination, out written);
        }

        public static bool TryWriteSurfaceUtf16(
            in H8AppliedLorePacketRecord record,
            H8AppliedLoreSurface surface,
            Span<char> destination,
            out int written)
        {
            written = 0;
            return destination.Length > 0 &&
                   TryGetUtf8(in record, surface, out ReadOnlySpan<byte> utf8Bytes) &&
                   TryDecodeUtf8ToChars(utf8Bytes, destination, out written);
        }

        public static int GetRouteCount()
        {
            return H8StaticDataArena.GetAppliedLoreRouteCount();
        }

        public static bool TryFindRoute(uint routeCardHash, out H8AppliedLoreRouteRecord record)
        {
            return H8StaticDataArena.TryFindAppliedLoreRoute(routeCardHash, out record);
        }

        public static bool TryGetRouteAt(int index, out H8AppliedLoreRouteRecord record)
        {
            return H8StaticDataArena.TryGetAppliedLoreRouteAt(index, out record);
        }

        public static bool TryFindRouteForPacket(uint packetHash, out H8AppliedLoreRouteRecord record)
        {
            record = default;
            return H8StaticDataArena.TryFindAppliedLoreRouteForPacket(packetHash, out record);
        }

        public static bool TryResolveRoutePacketOrdinal(
            in H8AppliedLoreRouteRecord record,
            uint packetHash,
            out uint ordinal)
        {
            ordinal = 0u;
            if (packetHash == 0u || record.PacketCount == 0u)
                return false;

            uint count = Math.Min(record.PacketCount, (uint)H8DataLayoutConstants.AppliedLoreRoutePacketCapacity);
            if (record.PacketHash0 == packetHash)
            {
                ordinal = 1u;
                return true;
            }
            if (count <= 1u)
                return false;
            if (record.PacketHash1 == packetHash)
            {
                ordinal = 2u;
                return true;
            }
            if (count <= 2u)
                return false;
            if (record.PacketHash2 == packetHash)
            {
                ordinal = 3u;
                return true;
            }
            if (count <= 3u)
                return false;
            if (record.PacketHash3 == packetHash)
            {
                ordinal = 4u;
                return true;
            }
            if (count <= 4u)
                return false;
            if (record.PacketHash4 == packetHash)
            {
                ordinal = 5u;
                return true;
            }
            if (count <= 5u)
                return false;
            if (record.PacketHash5 == packetHash)
            {
                ordinal = 6u;
                return true;
            }
            if (count <= 6u)
                return false;
            if (record.PacketHash6 == packetHash)
            {
                ordinal = 7u;
                return true;
            }
            if (count <= 7u || record.PacketHash7 != packetHash)
                return false;

            ordinal = 8u;
            return true;
        }

        public static uint GetRouteRequiredPacketHash(in H8AppliedLoreRouteRecord record, uint index)
        {
            return H8StaticDataArena.GetAppliedLoreRouteRequiredPacketHash(in record, index);
        }

        public static bool TryRaisePacketUnlocked(
            uint packetHash,
            uint sourceId = UnlockSourceId,
            byte flags = 0)
        {
            AbsoluteUniversePosition positionAup = default;
            byte loreFlags = (byte)(flags & ~(LoreFragmentScannedSignal.FlagHasAup | LoreFragmentScannedSignal.FlagPairedScanComplete));
            return TryRaisePacketUnlockedCore(packetHash, in positionAup, sourceId, loreFlags);
        }

        private static bool TryRaisePacketUnlockedCore(
            uint packetHash,
            in AbsoluteUniversePosition positionAup,
            uint sourceId,
            byte flags)
        {
            if (packetHash == 0u)
                return false;

            LoreFragmentScannedSignal signal = new LoreFragmentScannedSignal
            {
                PositionAup = positionAup,
                Hash = packetHash,
                Frame = SystemDispatcher.CurrentFrameId,
                SourceId = sourceId != 0u ? sourceId : UnlockSourceId,
                Flags = flags
            };

            return SignalBus<LoreFragmentScannedSignal>.TryPushTracked(
                in signal,
                ref s_appliedLoreSignalPushDropCount);
        }

        public static bool TryRaisePacketUnlockedAt(
            uint packetHash,
            in AbsoluteUniversePosition positionAup,
            uint sourceId = UnlockSourceId,
            byte flags = 0,
            byte reconKind = 0)
        {
            if (packetHash == 0u)
                return false;

            uint resolvedSourceId = sourceId != 0u ? sourceId : UnlockSourceId;
            bool hasFiniteAup = AbsoluteUniversePosition.IsFinite(in positionAup);
            byte loreFlags = hasFiniteAup
                ? (byte)(flags | LoreFragmentScannedSignal.FlagHasAup | LoreFragmentScannedSignal.FlagPairedScanComplete)
                : (byte)(flags & ~(LoreFragmentScannedSignal.FlagHasAup | LoreFragmentScannedSignal.FlagPairedScanComplete));
            bool raised = TryRaisePacketUnlockedCore(packetHash, in positionAup, resolvedSourceId, loreFlags);
            if (!hasFiniteAup)
                return raised;

            ScanCompleteSignal scanSignal = new ScanCompleteSignal
            {
                PositionAup = positionAup,
                EntryHash = packetHash,
                ScanId = packetHash,
                SourceId = resolvedSourceId,
                ReconKind = reconKind,
                Flags = flags
            };

            return SignalBus<ScanCompleteSignal>.TryPushTracked(
                in scanSignal,
                ref s_appliedLoreSignalPushDropCount) || raised;
        }

        public static bool TryRaiseScanCompleteWorldImpact(
            in ScanCompleteSignal signal,
            uint previousBiomeHash,
            ref int signalPushDropCount,
            out uint currentBiomeHash,
            out float acousticInterference01)
        {
            currentBiomeHash = previousBiomeHash;
            acousticInterference01 = 0f;
            if (signal.EntryHash == 0u ||
                !AbsoluteUniversePosition.IsFinite(in signal.PositionAup) ||
                !TryResolveWorldImpact(signal.EntryHash, out H8AppliedLoreWorldImpactRecord impact))
            {
                return false;
            }

            bool raised = false;
            uint frame = SystemDispatcher.CurrentFrameId;
            if ((impact.Flags & WorldImpactFlagBiome) != 0 &&
                impact.BiomeHash != 0u &&
                impact.BiomeHash != previousBiomeHash)
            {
                BiomeChangedSignal biomeSignal = new BiomeChangedSignal
                {
                    PositionAup = signal.PositionAup,
                    PreviousBiomeHash = previousBiomeHash,
                    CurrentBiomeHash = impact.BiomeHash,
                    PoiHash = signal.EntryHash,
                    Frame = frame
                };

                if (SignalBus<BiomeChangedSignal>.TryPushTracked(in biomeSignal, ref signalPushDropCount))
                {
                    currentBiomeHash = impact.BiomeHash;
                    raised = true;
                }
            }

            if ((impact.Flags & WorldImpactFlagAcoustic) != 0)
            {
                float depth01 = ResolveDepth01(in signal.PositionAup);
                acousticInterference01 = math.max(Sanitize01(impact.AcousticIntensity01), depth01);
                ToolAcousticSignal acousticSignal = new ToolAcousticSignal
                {
                    ToolHash = AcousticGhostSourceHash,
                    TargetHash = signal.EntryHash,
                    Progress01 = depth01,
                    PitchScale = SanitizePositive01(impact.AcousticPitchScale, 1f),
                    Intensity01 = acousticInterference01,
                    Frame = frame,
                    State = ToolAcousticSignal.StateDataGhost,
                    Flags = ToolAcousticSignal.FlagNarrativeGhost | ToolAcousticSignal.FlagCorrupted
                };

                raised |= SignalBus<ToolAcousticSignal>.TryPushTracked(in acousticSignal, ref signalPushDropCount);
            }

            return raised;
        }

        public static bool TryResolveWorldImpact(uint packetHash, out H8AppliedLoreWorldImpactRecord impact)
        {
            impact = default;
            if (packetHash == 0u)
                return false;

            impact.PacketHash = packetHash;
            impact.AcousticPitchScale = 1f;

            switch (packetHash)
            {
                case H8AppliedLoreHashes.P004_BLUE_DEBT:
                case H8AppliedLoreHashes.P019_HECTON8_RESOURCE_STACK:
                    impact.BiomeHash = H8Hashes.Biomes.BiomePlayCrystalGrowthHash;
                    impact.AcousticIntensity01 = 0.42f;
                    impact.Flags = WorldImpactFlagBiome | WorldImpactFlagAcoustic;
                    return true;

                case H8AppliedLoreHashes.P020_HECTON8_ECOLOGY_REGISTRY:
                case H8AppliedLoreHashes.P031_PHOTIC_SHELF_LIFE:
                    impact.BiomeHash = H8Hashes.Biomes.BiomePlayFossilReefHash;
                    impact.AcousticIntensity01 = 0.24f;
                    impact.Flags = WorldImpactFlagBiome;
                    return true;

                case H8AppliedLoreHashes.P033_CABLE_REEF_SYMBIOSIS:
                case H8AppliedLoreHashes.P048_CABLE_SPLICE_SCAR:
                    impact.BiomeHash = H8Hashes.Biomes.BiomePlayRiftSpineHash;
                    impact.AcousticIntensity01 = 0.5f;
                    impact.AcousticPitchScale = 0.92f;
                    impact.Flags = WorldImpactFlagBiome | WorldImpactFlagAcoustic;
                    return true;

                case H8AppliedLoreHashes.P034_ABYSSAL_REPAIR_FAUNA:
                case H8AppliedLoreHashes.P035_FACTORY_TEMPLE_THRESHOLD:
                    impact.BiomeHash = H8Hashes.Biomes.BiomePlayMetallicHadalHash;
                    impact.AcousticIntensity01 = 0.68f;
                    impact.AcousticPitchScale = 0.86f;
                    impact.Flags = WorldImpactFlagBiome | WorldImpactFlagAcoustic;
                    return true;

                case H8AppliedLoreHashes.P039_DEEP_REACH_CLEANSE_ORDER:
                case H8AppliedLoreHashes.P040_ATLAS_FINAL_ARGUMENT:
                    impact.BiomeHash = H8Hashes.Biomes.BiomePlayRiftVoidHash;
                    impact.AcousticIntensity01 = 0.82f;
                    impact.AcousticPitchScale = 0.78f;
                    impact.Flags = WorldImpactFlagBiome | WorldImpactFlagAcoustic;
                    return true;

                case H8AppliedLoreHashes.P045_BLACK_BOX_NAME_STACK:
                case H8AppliedLoreHashes.P049_SONAR_RETURN_ROUTE:
                    impact.AcousticIntensity01 = 0.72f;
                    impact.AcousticPitchScale = 0.84f;
                    impact.Flags = WorldImpactFlagAcoustic;
                    return true;

                case H8AppliedLoreHashes.P246_BLACK_KEEL_APPROACH_AUDIO_PACKET:
                    impact.AcousticIntensity01 = 0.36f;
                    impact.AcousticPitchScale = 0.96f;
                    impact.Flags = WorldImpactFlagAcoustic;
                    return true;

                case H8AppliedLoreHashes.P247_DROP_CAPSULE_DIAGNOSTIC_READOUT:
                    impact.AcousticIntensity01 = 0.44f;
                    impact.AcousticPitchScale = 0.9f;
                    impact.Flags = WorldImpactFlagAcoustic;
                    return true;

                case H8AppliedLoreHashes.P248_P63_PUMP_ROOM_FIRST_REPAIR_TASK:
                    impact.BiomeHash = H8Hashes.Biomes.BiomePlayFossilReefHash;
                    impact.AcousticIntensity01 = 0.32f;
                    impact.Flags = WorldImpactFlagBiome | WorldImpactFlagAcoustic;
                    return true;

                case H8AppliedLoreHashes.P249_SANITIZED_ACCIDENT_PACKET_BODY:
                    impact.BiomeHash = H8Hashes.Biomes.BiomePlayFossilReefHash;
                    impact.AcousticIntensity01 = 0.48f;
                    impact.AcousticPitchScale = 0.88f;
                    impact.Flags = WorldImpactFlagBiome | WorldImpactFlagAcoustic;
                    return true;

                case H8AppliedLoreHashes.P250_FIRST_ATLAS_REPAIR_TRACE_SCENE:
                    impact.BiomeHash = H8Hashes.Biomes.BiomePlayFossilReefHash;
                    impact.AcousticIntensity01 = 0.56f;
                    impact.AcousticPitchScale = 0.82f;
                    impact.Flags = WorldImpactFlagBiome | WorldImpactFlagAcoustic;
                    return true;

                default:
                    return false;
            }
        }

        private static float ResolveDepth01(in AbsoluteUniversePosition aup)
        {
            if (!AbsoluteUniversePosition.IsFinite(in aup))
                return 0f;

            double absoluteY = ((double)aup.GridY * AbsoluteUniversePosition.CellSizeMeters) + aup.LocalY;
            if (!math.isfinite(absoluteY))
                return 0f;

            float depthMeters = (float)math.max(0.0, DefaultSeaLevelY - absoluteY);
            if (!math.isfinite(depthMeters))
                return 0f;

            return Sanitize01(depthMeters / MaxLoreDepthMeters);
        }

        private static float Sanitize01(float value)
        {
            return math.isfinite(value) ? math.saturate(value) : 0f;
        }

        private static float SanitizePositive01(float value, float fallback)
        {
            if (!math.isfinite(value) || value <= 0f)
                return fallback;

            return math.saturate(value);
        }

        private static bool TryDecodeUtf8ToChars(ReadOnlySpan<byte> source, Span<char> destination, out int written)
        {
            written = 0;
            int sourceCursor = 0;
            while (sourceCursor < source.Length)
            {
                byte b0 = source[sourceCursor];
                if (b0 < 0x80)
                {
                    if (written >= destination.Length)
                        return false;

                    destination[written++] = (char)b0;
                    sourceCursor++;
                    continue;
                }

                if ((b0 & 0xE0) == 0xC0 && sourceCursor + 1 < source.Length)
                {
                    byte b1 = source[sourceCursor + 1];
                    int scalar = ((b0 & 0x1F) << 6) | (b1 & 0x3F);
                    if (IsUtf8Continuation(b1) && scalar >= 0x80)
                    {
                        if (written >= destination.Length)
                            return false;

                        destination[written++] = (char)scalar;
                        sourceCursor += 2;
                        continue;
                    }
                }
                else if ((b0 & 0xF0) == 0xE0 && sourceCursor + 2 < source.Length)
                {
                    byte b1 = source[sourceCursor + 1];
                    byte b2 = source[sourceCursor + 2];
                    int scalar = ((b0 & 0x0F) << 12) | ((b1 & 0x3F) << 6) | (b2 & 0x3F);
                    if (IsUtf8Continuation(b1) &&
                        IsUtf8Continuation(b2) &&
                        scalar >= 0x800 &&
                        !IsUtf16SurrogateScalar(scalar))
                    {
                        if (written >= destination.Length)
                            return false;

                        destination[written++] = (char)scalar;
                        sourceCursor += 3;
                        continue;
                    }
                }
                else if ((b0 & 0xF8) == 0xF0 && sourceCursor + 3 < source.Length)
                {
                    byte b1 = source[sourceCursor + 1];
                    byte b2 = source[sourceCursor + 2];
                    byte b3 = source[sourceCursor + 3];
                    int scalar = ((b0 & 0x07) << 18) | ((b1 & 0x3F) << 12) | ((b2 & 0x3F) << 6) | (b3 & 0x3F);
                    if (IsUtf8Continuation(b1) &&
                        IsUtf8Continuation(b2) &&
                        IsUtf8Continuation(b3) &&
                        scalar >= 0x10000 &&
                        scalar <= 0x10FFFF)
                    {
                        if (written + 1 >= destination.Length)
                            return false;

                        int shifted = scalar - 0x10000;
                        destination[written++] = (char)(0xD800 + (shifted >> 10));
                        destination[written++] = (char)(0xDC00 + (shifted & 0x3FF));
                        sourceCursor += 4;
                        continue;
                    }
                }

                if (written >= destination.Length)
                    return false;

                destination[written++] = '\uFFFD';
                sourceCursor++;
            }

            return written > 0;
        }

        private static bool IsUtf8Continuation(byte value)
        {
            return (value & 0xC0) == 0x80;
        }

        private static bool IsUtf16SurrogateScalar(int scalar)
        {
            return scalar >= 0xD800 && scalar <= 0xDFFF;
        }
    }
}
