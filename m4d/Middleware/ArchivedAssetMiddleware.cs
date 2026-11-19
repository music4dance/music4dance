using System.Globalization;

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
        var found = FindArchived(context);
        if (found == null)
        {
            await next(context);
            return;
        }

        // Opened before any header is set: another instance can prune the build between the
        // lookup and here, and then the request just passes through (and 404s) instead of failing.
        // Once open, the copy is ours to finish even if the folder is deleted meanwhile.
        FileStream stream;
        try
        {
            stream = new FileStream(found.Served, FileMode.Open, FileAccess.Read,
                FileShare.Read | FileShare.Delete, bufferSize: 1, FileOptions.Asynchronous);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            await next(context);
            return;
        }

        await using (stream)
        {
            var response = context.Response;
            response.StatusCode = StatusCodes.Status200OK;
            response.ContentType = ContentTypes.TryGetContentType(found.Name, out var type)
                ? type
                : "application/octet-stream";
            response.Headers.Vary = HeaderNames.AcceptEncoding;
            if (found.Encoding != null)
            {
                response.Headers.ContentEncoding = found.Encoding;
            }
            response.ContentLength = stream.Length;

            if (!HttpMethods.IsHead(context.Request.Method))
            {
                await stream.CopyToAsync(response.Body, context.RequestAborted);
            }
        }
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

        // The names carry content hashes, so a precompressed copy from any archived build is this
        // same file compressed
        var accepted = request.Headers.AcceptEncoding.ToString();
        return Encodings
            .Where(e => Accepts(accepted, e.Encoding))
            .Select(e => (e.Encoding, Path: archive.Find(name + e.Extension)))
            .Where(e => e.Path != null)
            .Select(e => new ArchivedFile(name, e.Path, e.Encoding))
            .FirstOrDefault() ?? new ArchivedFile(name, original, null);
    }

    // True when Accept-Encoding lists the encoding with a q-value above zero (no q means 1)
    internal static bool Accepts(string acceptEncoding, string encoding) =>
        acceptEncoding.Split(',')
            .Select(part => part.Split(';'))
            .Any(parts => parts[0].Trim().Equals(encoding, StringComparison.OrdinalIgnoreCase)
                && Quality(parts.Skip(1)) > 0);

    private static double Quality(IEnumerable<string> parameters)
    {
        var q = parameters
            .Select(p => p.Trim())
            .FirstOrDefault(p => p.StartsWith("q=", StringComparison.OrdinalIgnoreCase));
        if (q == null)
        {
            return 1;
        }

        // An unparsable q-value counts as a refusal rather than a guess
        return double.TryParse(q[2..], NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var value)
            ? value
            : 0;
    }

    private sealed record ArchivedFile(string Name, string Served, string Encoding);
}
