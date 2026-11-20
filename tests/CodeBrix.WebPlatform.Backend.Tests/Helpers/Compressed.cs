using System;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace CodeBrix.WebPlatform.Backend.Tests.Helpers;

/// <summary>Compressed copies for the web roots of the tests, and requests that ask for them.</summary>
public static class Compressed
{
    public static byte[] Brotli(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var brotli = new BrotliStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            brotli.Write(bytes, 0, bytes.Length);
        }

        return output.ToArray();
    }

    public static byte[] Gzip(byte[] bytes)
    {
        using var output = new MemoryStream();
        using (var gzip = new GZipStream(output, CompressionLevel.SmallestSize, leaveOpen: true))
        {
            gzip.Write(bytes, 0, bytes.Length);
        }

        return output.ToArray();
    }

    /// <summary>The bytes of a body sent with a content coding (or none).</summary>
    public static byte[] Decode(byte[] body, string coding)
    {
        if (string.IsNullOrEmpty(coding))
        {
            return body;
        }

        using var input = new MemoryStream(body);
        using Stream decoder = coding switch
        {
            "br" => new BrotliStream(input, CompressionMode.Decompress),
            "gzip" => new GZipStream(input, CompressionMode.Decompress),
            _ => throw new ArgumentException("Unknown coding " + coding, nameof(coding)),
        };
        using var output = new MemoryStream();
        decoder.CopyTo(output);
        return output.ToArray();
    }

    /// <summary>Writes a file and the copies asked for beside it.</summary>
    public static void WriteWithCopies(this TempWebRoot root, string relativePath, byte[] content, bool brotli = true, bool gzip = true)
    {
        root.Write(relativePath, content);
        if (brotli)
        {
            root.Write(relativePath + ".br", Brotli(content));
        }

        if (gzip)
        {
            root.Write(relativePath + ".gz", Gzip(content));
        }
    }

    /// <summary>Sends a request with the headers given (name, value pairs; a null value is left out).</summary>
    public static Task<HttpResponseMessage> SendWithAsync(this HttpClient client, string method, string path, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), new Uri(BackendHost.OwnOrigin + path));
        foreach (var (name, value) in headers)
        {
            if (value != null)
            {
                request.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return client.SendAsync(request, TestContext.Current.CancellationToken);
    }

    public static Task<byte[]> BytesAsync(this HttpResponseMessage response)
        => response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);

    /// <summary>The answer's Content-Encoding, or null.</summary>
    public static string Coding(this HttpResponseMessage response)
        => response.Content.Headers.ContentEncoding.Count == 0 ? null : string.Join(",", response.Content.Headers.ContentEncoding);

    /// <summary>The decoded body of the answer.</summary>
    public static async Task<byte[]> DecodedAsync(this HttpResponseMessage response)
        => Decode(await response.BytesAsync(), response.Coding());
}
