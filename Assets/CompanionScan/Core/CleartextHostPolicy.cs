using System;
using System.Collections.Generic;
using System.Globalization;

namespace CompanionScan.Core
{
    /// <summary>
    /// Decides which hosts may receive plain http in development builds.
    ///
    /// Only explicitly listed LAN hosts qualify: private, link-local or loopback
    /// IPv4 addresses, single-label hostnames, and names under LAN-only suffixes
    /// (.local, .lan, .home.arpa, .internal, .localdomain). Public addresses,
    /// carrier-grade NAT/overlay ranges, IPv6 literals, wildcards, ports and
    /// schemes are rejected.
    /// </summary>
    public static class CleartextHostPolicy
    {
        private static readonly string[] LanSuffixes = { ".local", ".lan", ".home.arpa", ".internal", ".localdomain" };

        /// <summary>
        /// Validates <paramref name="host"/> as a LAN host and returns its
        /// lower-case form. <paramref name="reason"/> explains a rejection.
        /// </summary>
        public static bool TryNormalizeLanHost(string host, out string normalized, out string reason)
        {
            normalized = null;
            reason = null;

            var value = (host ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();
            if (value.Length == 0)
            {
                reason = "empty host";
                return false;
            }

            if (value.IndexOf("://", StringComparison.Ordinal) >= 0 || value.IndexOfAny(new[] { '/', '@', '?', '#' }) >= 0)
            {
                reason = "use a bare host name or IP address, without scheme or path";
                return false;
            }

            if (value.IndexOf(':') >= 0)
            {
                reason = "ports and IPv6 literals are not supported; list the host only";
                return false;
            }

            if (value.IndexOf('*') >= 0)
            {
                reason = "wildcards are not allowed";
                return false;
            }

            if (LooksLikeIPv4(value))
            {
                if (!TryParseIPv4(value, out var octets))
                {
                    reason = "invalid IPv4 address";
                    return false;
                }

                if (!IsLanIPv4(octets))
                {
                    reason = "not a private, link-local or loopback IPv4 address";
                    return false;
                }

                normalized = value;
                return true;
            }

            if (!IsValidHostName(value))
            {
                reason = "invalid host name";
                return false;
            }

            if (value.IndexOf('.') >= 0 && !HasLanSuffix(value))
            {
                reason = "not a LAN host name (use a single-label name or a .local/.lan/.home.arpa/.internal name)";
                return false;
            }

            normalized = value;
            return true;
        }

        /// <summary>Returns the valid LAN hosts (normalized, de-duplicated) and the rejected entries with reasons.</summary>
        public static List<string> NormalizeLanHosts(IEnumerable<string> hosts, List<KeyValuePair<string, string>> rejected = null)
        {
            var result = new List<string>();
            if (hosts == null)
            {
                return result;
            }

            foreach (var host in hosts)
            {
                if (TryNormalizeLanHost(host, out var normalized, out var reason))
                {
                    if (!result.Contains(normalized))
                    {
                        result.Add(normalized);
                    }
                }
                else
                {
                    rejected?.Add(new KeyValuePair<string, string>(host ?? string.Empty, reason));
                }
            }

            return result;
        }

        /// <summary>True when <paramref name="host"/> is a valid LAN host listed in <paramref name="allowedHosts"/>.</summary>
        public static bool Contains(IEnumerable<string> allowedHosts, string host)
        {
            if (!TryNormalizeLanHost(host, out var normalized, out _))
            {
                return false;
            }

            return NormalizeLanHosts(allowedHosts).Contains(normalized);
        }

        private static bool HasLanSuffix(string value)
        {
            foreach (var suffix in LanSuffixes)
            {
                if (value.EndsWith(suffix, StringComparison.Ordinal) && value.Length > suffix.Length)
                {
                    return true;
                }
            }

            return false;
        }

        private static bool LooksLikeIPv4(string value)
        {
            foreach (var c in value)
            {
                if (c != '.' && (c < '0' || c > '9'))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryParseIPv4(string value, out int[] octets)
        {
            octets = new int[4];
            var parts = value.Split('.');
            if (parts.Length != 4)
            {
                return false;
            }

            for (int i = 0; i < 4; i++)
            {
                if (parts[i].Length == 0 || parts[i].Length > 3 ||
                    !int.TryParse(parts[i], NumberStyles.None, CultureInfo.InvariantCulture, out octets[i]) ||
                    octets[i] > 255)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsLanIPv4(int[] o)
        {
            return o[0] == 10                                   // 10.0.0.0/8
                   || (o[0] == 172 && o[1] >= 16 && o[1] <= 31) // 172.16.0.0/12
                   || (o[0] == 192 && o[1] == 168)              // 192.168.0.0/16
                   || (o[0] == 169 && o[1] == 254)              // 169.254.0.0/16 link-local
                   || o[0] == 127;                              // loopback (adb reverse)
        }

        private static bool IsValidHostName(string value)
        {
            if (value.Length > 253)
            {
                return false;
            }

            foreach (var label in value.Split('.'))
            {
                if (label.Length == 0 || label.Length > 63 || label[0] == '-' || label[label.Length - 1] == '-')
                {
                    return false;
                }

                foreach (var c in label)
                {
                    bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-';
                    if (!ok)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
