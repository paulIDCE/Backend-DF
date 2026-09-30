using System.Security.Claims;

namespace BackendDF.Common.Utils
{
    public static class ClaimsPrincipalExtensions
    {
        /// <summary>
        /// Lee un claim del usuario ya autenticado por el middleware y lo convierte al tipo T.
        /// Uso: <c>User.GetClaim&lt;int&gt;("institucionID")</c>. No re-lee ni re-valida el token.
        /// </summary>
        public static T GetClaim<T>(this ClaimsPrincipal user, string claimType, T defaultValue = default!)
        {
            var value = user.FindFirst(claimType)?.Value;

            if (string.IsNullOrEmpty(value))
                return defaultValue;

            try { return (T)Convert.ChangeType(value, typeof(T)); }
            catch { return defaultValue; }
        }
    }
}
