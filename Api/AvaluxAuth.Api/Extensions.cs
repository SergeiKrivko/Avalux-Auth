using System.Security.Claims;

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
}