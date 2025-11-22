namespace m4d.Utilities;

public static class HttpClientHelper
{
    public static HttpClient Client => _client;
    private static readonly HttpClient _client = new();
}
