using m4d.Services;

using Microsoft.AspNetCore.StaticFiles;
using Microsoft.Net.Http.Headers;

namespace m4d.Middleware;

/// <summary>
/// Serves a /vclient/assets bundle from an earlier build when the current build doesn't have it,
/// so HTML cached before a deploy still loads its scripts (see <see cref="AssetArchive"/>).
/// Requests for bundles the current build has, and for names no archived build has, pass through
/// untouched to the static-file middleware, which serves or 404s them as before.
///
/// Registered after <see cref="StaticAssetCacheMiddleware"/> (so archived bundles get the same
/// immutable Cache-Control and Cloudflare keeps them) and ahead of the static-file middleware.
/// </summary>
public class ArchivedAssetMiddleware(
    RequestDelegate next, AssetArchive archive, IWebHostEnvironment environment)
{
    private static readonly PathString HashedAssets = new("/vclient/assets");
    private static readonly FileExtensionContentTypeProvider ContentTypes = new();

    // Precompressed copies the publish step writes next to each bundle, best first
    private static readonly (string Encoding, string Extension)[] Encodings =
    [
        ("br", ".br"),
        ("gzip", ".gz"),
    ];

    public async Task InvokeAsync(HttpContext context)
    {
        var path = FindArchived(context);
        if (path == null)
        {
            await next(context);
            return;
        }

        var response = context.Response;
        response.StatusCode = StatusCodes.Status200OK;
        response.ContentType = ContentTypes.TryGetContentType(path.Original, out var type)
            ? type
            : "application/octet-stream";
        response.Headers.Vary = HeaderNames.AcceptEncoding;
        if (path.Encoding != null)
        {
            response.Headers.ContentEncoding = path.Encoding;
        }

        var file = new FileInfo(path.Served);
        response.ContentLength = file.Length;
        if (HttpMethods.IsHead(context.Request.Method))
        {
            return;
        }

        await response.SendFileAsync(file.FullName, context.RequestAborted);
    }

    private ArchivedFile FindArchived(HttpContext context)
    {
        var request = context.Request;
        if (!HttpMethods.IsGet(request.Method) && !HttpMethods.IsHead(request.Method)
            || !request.Path.StartsWithSegments(HashedAssets, StringComparison.OrdinalIgnoreCase, out var rest))
        {
            return null;
        }

        var name = rest.Value?.TrimStart('/');
        if (!AssetArchive.IsSafeName(name)
            || environment.WebRootFileProvider.GetFileInfo($"vclient/assets/{name}").Exists)
        {
            return null;
        }

        var original = archive.Find(name);
        if (original == null)
        {
            return null;
        }

        var accepted = request.Headers.AcceptEncoding.ToString();
        var compressed = Encodings
            .Where(e => Accepts(accepted, e.Encoding))
            .Select(e => (e.Encoding, Path: original + e.Extension))
            .FirstOrDefault(e => File.Exists(e.Path));

        return compressed.Path != null
            ? new ArchivedFile(original, compressed.Path, compressed.Encoding)
            : new ArchivedFile(original, original, null);
    }

    private static bool Accepts(string acceptEncoding, string encoding) =>
        acceptEncoding.Split(',')
            .Select(part => part.Split(';'))
            .Any(parts => parts[0].Trim().Equals(encoding, StringComparison.OrdinalIgnoreCase)
                && !parts.Skip(1).Any(p => p.Replace(" ", "").Equals("q=0", StringComparison.Ordinal)));

    private sealed record ArchivedFile(string Original, string Served, string Encoding);
}
