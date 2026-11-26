using System;
using System.Globalization;

namespace Companion.Spatial.Extras
{
    /// <summary>
    /// Picks the earliest or the latest of some ISO 8601 times held as text. Times are compared as instants, not as
    /// text ("2026-11-25T10:00:00+02:00" is earlier than "2026-11-25T09:00:00Z"); a time with no zone is taken as UTC.
    /// Text that is not a time is used only when nothing else is. The result is the original text, unchanged.
    /// </summary>
    internal static class ExtrasTime
    {
        internal static string Earliest(params string[] times)
        {
            return Pick(times, true);
        }

        internal static string Latest(params string[] times)
        {
            return Pick(times, false);
        }

        private static string Pick(string[] times, bool earliest)
        {
            if (times == null)
            {
                return null;
            }

            string best = null;
            DateTimeOffset bestTime = default;
            bool bestParsed = false;
            foreach (string time in times)
            {
                if (string.IsNullOrWhiteSpace(time))
                {
                    continue;
                }

                DateTimeOffset parsed;
                bool isTime = DateTimeOffset.TryParse(time, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces, out parsed);
                if (best == null)
                {
                    best = time;
                    bestTime = parsed;
                    bestParsed = isTime;
                    continue;
                }

                bool better;
                if (isTime && bestParsed)
                {
                    better = earliest ? parsed < bestTime : parsed > bestTime;
                }
                else if (isTime)
                {
                    better = true;
                }
                else if (bestParsed)
                {
                    better = false;
                }
                else
                {
                    int order = string.CompareOrdinal(time, best);
                    better = earliest ? order < 0 : order > 0;
                }

                if (better)
                {
                    best = time;
                    bestTime = parsed;
                    bestParsed = isTime;
                }
            }

            return best;
        }
    }
}
