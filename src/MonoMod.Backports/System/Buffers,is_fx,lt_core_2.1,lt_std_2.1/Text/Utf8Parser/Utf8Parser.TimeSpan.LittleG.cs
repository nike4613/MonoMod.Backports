// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.Buffers.Text
{
    public static partial class Utf8Parser
    {
        private static bool TryParseTimeSpanLittleG(ReadOnlySpan<byte> source, out TimeSpan value, out int bytesConsumed)
        {
            TimeSpanSplitter s = default;
            if (!s.TrySplitTimeSpan(source, periodUsedToSeparateDay: false, out bytesConsumed))
            {
                value = default;
                return false;
            }

            bool isNegative = s.IsNegative;

            bool success;
            switch (s.Separators)
            {
                case 0x00000000: // dd
                    success = TryCreateTimeSpan(isNegative: isNegative, days: s.V1, hours: 0, minutes: 0, seconds: 0, fraction: 0, out value);
                    break;

                case 0x01000000: // hh:mm
                    success = TryCreateTimeSpan(isNegative: isNegative, days: 0, hours: s.V1, minutes: s.V2, seconds: 0, fraction: 0, out value);
                    break;

                case 0x01010000: // hh:mm:ss
                    success = TryCreateTimeSpan(isNegative: isNegative, days: 0, hours: s.V1, minutes: s.V2, seconds: s.V3, fraction: 0, out value);
                    break;

                case 0x01010100: // dd:hh:mm:ss
                    success = TryCreateTimeSpan(isNegative: isNegative, days: s.V1, hours: s.V2, minutes: s.V3, seconds: s.V4, fraction: 0, out value);
                    break;

                case 0x01010200: // hh:mm:ss.fffffff
                    success = TryCreateTimeSpan(isNegative: isNegative, days: 0, hours: s.V1, minutes: s.V2, seconds: s.V3, fraction: s.V4, out value);
                    break;

                case 0x01010102: // dd:hh:mm:ss.fffffff
                    success = TryCreateTimeSpan(isNegative: isNegative, days: s.V1, hours: s.V2, minutes: s.V3, seconds: s.V4, fraction: s.V5, out value);
                    break;

                default:
                    value = default;
                    success = false;
                    break;
            }

            if (!success)
            {
                bytesConsumed = 0;
                return false;
            }

            return true;
        }
    }
}
