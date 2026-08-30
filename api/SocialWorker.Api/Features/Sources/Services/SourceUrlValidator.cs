using System;
using System.Net;
using System.Net.Sockets;

namespace SocialWorker.Api.Features.Sources;

public sealed class SourceUrlValidator : ISourceUrlValidator
{
    public void EnsureAbsoluteHttpUrl(string reference, string paramName = "reference")
    {
        if (!Uri.TryCreate(reference, UriKind.Absolute, out var uri))
        {
            throw new ArgumentException("Source URLs must be absolute HTTP or HTTPS URLs.", paramName);
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new ArgumentException("Source URLs must use http:// or https://.", paramName);
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            throw new ArgumentException("Source URLs must include a valid host.", paramName);
        }

        var host = uri.Host.Trim();

        if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Localhost and loopback URLs are not permitted.", paramName);
        }

        if (IPAddress.TryParse(host, out var ip))
        {
            if (IsDisallowedIpAddress(ip))
            {
                throw new ArgumentException($"Private or loopback IP addresses ({ip}) are not permitted.", paramName);
            }
        }
    }

    public static bool IsDisallowedIpAddress(IPAddress ip)
    {
        if (IPAddress.IsLoopback(ip) || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.IPv6Any) || ip.Equals(IPAddress.None))
        {
            return true;
        }

        if (ip.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            if (bytes[0] == 10) return true;
            if (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31) return true;
            if (bytes[0] == 192 && bytes[1] == 168) return true;
            if (bytes[0] == 169 && bytes[1] == 254) return true;
            if (bytes[0] == 127) return true;
            if (bytes[0] == 0) return true;
        }
        else if (ip.AddressFamily == AddressFamily.InterNetworkV6)
        {
            if (ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal || ip.IsIPv6Multicast)
            {
                return true;
            }

            var bytes = ip.GetAddressBytes();
            if ((bytes[0] & 0xFE) == 0xFC) return true;
            if (ip.IsIPv4MappedToIPv6)
            {
                return IsDisallowedIpAddress(ip.MapToIPv4());
            }
        }

        return false;
    }
}

