namespace Ecom.Core.Constants
{
    /// <summary>
    /// Named application roles used by the authorization policy. The catalog write endpoints
    /// (product/category create, update, delete and discount operations) are reserved for
    /// administrators, so ordinary authenticated shoppers cannot mutate the catalog.
    /// </summary>
    public static class AppRoles
    {
        public const string Admin = "Admin";
    }
}
