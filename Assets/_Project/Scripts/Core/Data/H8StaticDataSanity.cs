using Unity.Mathematics;

namespace Hecton8.Core.Data
{
    /// <summary>
    /// Cold-path binary sanity pass for static records.
    /// </summary>
    public static class H8StaticDataSanity
    {
        public static H8StaticDataSanityReport ScanForNaNs(StaticDataStore store)
        {
            if (store == null || !store.IsOpen)
            {
                return new H8StaticDataSanityReport
                {
                    IsClean = false,
                    Message = "StaticDataStore is not open."
                };
            }

            int scanned = 0;
            for (int i = 0; i < store.LookupCount; i++)
            {
                if (!store.TryGetLookupEntry(i, out H8StaticDataLookupEntry entry))
                    return Fail(store, scanned, 0u, 0, "Lookup entry unreadable.");

                bool finite;
                switch (entry.RecordType)
                {
                    case H8StaticDataFormat.RecordTypeItem:
                        ref readonly H8ItemStaticRecord item = ref store.FetchRecord<H8ItemStaticRecord>(entry.Hash);
                        finite = math.isfinite(item.MassKg) && math.isfinite(item.AccessFrequency);
                        break;
                    case H8StaticDataFormat.RecordTypeEconomy:
                        ref readonly H8EconomyStaticRecord economy = ref store.FetchRecord<H8EconomyStaticRecord>(entry.Hash);
                        finite =
                            math.isfinite(economy.BasePrice) &&
                            math.isfinite(economy.Scarcity01) &&
                            math.isfinite(economy.Demand01) &&
                            math.isfinite(economy.SupplyRefreshSeconds) &&
                            math.isfinite(economy.AccessFrequency);
                        break;
                    case H8StaticDataFormat.RecordTypePhysics:
                        ref readonly H8PhysicsStaticRecord physics = ref store.FetchRecord<H8PhysicsStaticRecord>(entry.Hash);
                        finite =
                            math.isfinite(physics.MassKg) &&
                            math.isfinite(physics.AddedMass) &&
                            math.isfinite(physics.LinearDrag) &&
                            math.isfinite(physics.Buoyancy) &&
                            math.isfinite(physics.CrushDepthM) &&
                            math.isfinite(physics.AccessFrequency);
                        break;
                    case H8StaticDataFormat.RecordTypeFauna:
                        ref readonly H8FaunaStaticRecord fauna = ref store.FetchRecord<H8FaunaStaticRecord>(entry.Hash);
                        finite =
                            math.isfinite(fauna.SwimSpeed) &&
                            math.isfinite(fauna.TurnRate) &&
                            math.isfinite(fauna.Aggression01) &&
                            math.isfinite(fauna.FleeDistanceM) &&
                            math.isfinite(fauna.BiolumIntensity) &&
                            math.isfinite(fauna.AccessFrequency);
                        break;
                    default:
                        return Fail(store, scanned, entry.Hash, entry.RecordType, "Unknown record type.");
                }

                if (!finite)
                    return Fail(store, scanned, entry.Hash, entry.RecordType, "NaN or Infinity detected.");

                scanned++;
            }

            return new H8StaticDataSanityReport
            {
                IsClean = true,
                RecordsScanned = scanned,
                Message = "Static data sanity scan clean."
            };
        }

        private static H8StaticDataSanityReport Fail(
            StaticDataStore store,
            int scanned,
            uint hash,
            ushort recordType,
            string message)
        {
            store.DumpBlackBox();
            return new H8StaticDataSanityReport
            {
                IsClean = false,
                RecordsScanned = scanned,
                FailedHash = hash,
                FailedRecordType = recordType,
                Message = message
            };
        }
    }
}
