using System.Net;
using System.Net.Sockets;
using System.Security.Claims;
using AvaluxAuth.Models;

namespace AvaluxAuth.Api;

public static class Extensions
{
    extension(ClaimsPrincipal claimsPrincipal)
    {
        public bool IsAdmin => claimsPrincipal.IsInRole("Admin");

        public bool HasPermission(string permission)
        {
            if (claimsPrincipal.IsAdmin) return true;
            var permissions = claimsPrincipal.FindFirst("Permissions")?.Value;
            return permissions != null && permissions.Split(';').Contains(permission);
        }

        public bool HasPermission(TokenPermission permission)
        {
            return claimsPrincipal.HasPermission(permission.Key);
        }

        public Guid ApplicationId
        {
            get
            {
                if (!Guid.TryParse(claimsPrincipal.FindFirst("ApplicationId")?.Value, out var applicationId))
                    throw new Exception("ApplicationId is not a Guid");
                return applicationId;
            }
        }

        public bool HasApplication(Guid? applicationId)
        {
            return claimsPrincipal.IsAdmin || claimsPrincipal.ApplicationId == applicationId;
        }
    }

    extension(HttpRequest request)
    {
        public string GetBaseUrl()
        {
            var scheme = request.Headers["X-Forwarded-Proto"].FirstOrDefault() ?? request.Scheme;
            var host = request.Headers["X-Forwarded-Host"].FirstOrDefault() ?? request.Host.ToString();

            return $"{scheme}://{host}";
        }
    }

    extension(HttpContext context)
    {
        /// <summary>
        /// Rate-limit key (H-06): the peer address, or the rightmost
        /// <c>X-Forwarded-For</c> hop when the request arrives from a private
        /// network. The API is published only through Traefik, which appends the
        /// real client address, so entries prepended by a client are ignored.
        /// </summary>
        public string GetClientIpAddress()
        {
            var address = context.Connection.RemoteIpAddress;
            if (address is null)
                return "unknown";

            if (IsPrivateAddress(address))
            {
                var forwarded = context.Request.Headers["X-Forwarded-For"].ToString();
                if (!string.IsNullOrEmpty(forwarded))
                {
                    var candidate = forwarded.Split(',', StringSplitOptions.TrimEntries)[^1];
                    if (IPAddress.TryParse(candidate, out var client))
                        address = client;
                }
            }

            if (address.IsIPv4MappedToIPv6)
                address = address.MapToIPv4();

            return address.ToString();
        }
    }

    private static bool IsPrivateAddress(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (IPAddress.IsLoopback(address))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            var bytes = address.GetAddressBytes();
            return bytes[0] == 10
                   || (bytes[0] == 172 && bytes[1] is >= 16 and <= 31)
                   || (bytes[0] == 192 && bytes[1] == 168)
                   || (bytes[0] == 169 && bytes[1] == 254);
        }

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return address.IsIPv6LinkLocal || address.IsIPv6SiteLocal
                   || (address.GetAddressBytes()[0] & 0xfe) == 0xfc; // fc00::/7
        }

        return false;
    }
}