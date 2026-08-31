using OpenIddict.Validation.AspNetCore;

namespace m4d.PublicApi;

public static class PublicApiDefaults
{
    public const string BearerScheme = OpenIddictValidationAspNetCoreDefaults.AuthenticationScheme;
    public const string SubscriberPolicy = "PublicApiSubscriber";

    public static class Endpoints
    {
        public const string Authorization = "/connect/authorize";
        public const string Revocation = "/connect/revocation";
        public const string Token = "/connect/token";
    }

    public static class Clients
    {
        public const string DanzQ = "danzq-ios";
        public const string DanzQDisplayName = "DanzQ";
        public const string DanzQRedirectUri = "com.domke.danzq:/oauth/callback";
    }

    public static class Scopes
    {
        public const string AccountRead = "account:read";
        public const string SongsRead = "songs:read";
    }
}
