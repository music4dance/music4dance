namespace m4d.Utilities
{
    public static class ConfigurationExtensions
    {
        // Accept IConfiguration so it works with injected _configuration
        public static bool UseVite(this IConfiguration configuration)
        {
            return configuration["ASPNETCORE_VITE"]?.ToLower() == "true";
        }
    }
}
