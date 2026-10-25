using System.IO;
using System.Diagnostics;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;

namespace PokemonRebornLauncher.Services;

public sealed class VerifiedDownload : IDisposable
{
    private readonly HttpClient client;
    public VerifiedDownload(HttpMessageHandler? handler = null)
    {
        client = new HttpClient(handler ?? new HttpClientHandler { AutomaticDecompression = DecompressionMethods.None }) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("RebornUpdater/7.0");
        client.DefaultRequestHeaders.CacheControl = new() { NoCache = true };
    }
    public static void ValidateUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !string.IsNullOrEmpty(uri.UserInfo))
            throw new InvalidDataException("Updates benötigen eine gültige HTTPS-Adresse.");
    }
    private async Task<HttpResponseMessage> OpenAsync(string url, CancellationToken ct)
    {
        ValidateUrl(url);
        using var headerTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        headerTimeout.CancelAfter(TimeSpan.FromSeconds(45));
        var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, headerTimeout.Token);
        try
        {
            ValidateUrl(response.RequestMessage?.RequestUri?.AbsoluteUri ?? url);
            if (response.StatusCode == HttpStatusCode.NotFound)
                throw new InvalidOperationException("Das neue Update-Manifest oder Paket wurde noch nicht veröffentlicht (404). Bitte später erneut prüfen.");
            response.EnsureSuccessStatusCode();
            return response;
        }
        catch { response.Dispose(); throw; }
    }
    public async Task<string> TextAsync(string url, CancellationToken ct)
    {
        using var overall = CancellationTokenSource.CreateLinkedTokenSource(ct);
        overall.CancelAfter(TimeSpan.FromMinutes(2));
        ct = overall.Token;
        using var response = await OpenAsync(url, ct);
        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        var buffer = new byte[16384];
        while (true)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(45));
            int read = await stream.ReadAsync(buffer, timeout.Token);
            if (read == 0) break;
            if (output.Length + read > 2 * 1024 * 1024) throw new InvalidDataException("Das Update-Manifest ist zu groß.");
            output.Write(buffer, 0, read);
        }
        return System.Text.Encoding.UTF8.GetString(output.ToArray()).TrimStart('\uFEFF');
    }
    public async Task DownloadAsync(UpdateAsset asset, string destination, IProgress<UpdateProgress> progress, CancellationToken ct)
    {
        using var overall = CancellationTokenSource.CreateLinkedTokenSource(ct);
        overall.CancelAfter(TimeSpan.FromHours(2));
        ct = overall.Token;
        asset.Validate();
        using var response = await OpenAsync(asset.Url, ct);
        if (response.Content.Headers.ContentLength is long length && length != asset.Size)
            throw new InvalidDataException("Die Downloadgröße stimmt nicht mit dem Manifest überein.");
        try
        {
            await using var source = await response.Content.ReadAsStreamAsync(ct);
            await using var target = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None, 131072, FileOptions.Asynchronous);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            byte[] buffer = new byte[131072];
            long total = 0, last = 0;
            var watch = Stopwatch.StartNew();
            while (true)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                timeout.CancelAfter(TimeSpan.FromSeconds(60));
                int read = await source.ReadAsync(buffer, timeout.Token);
                if (read == 0) break;
                total += read;
                if (total > asset.Size) throw new InvalidDataException("Das Paket ist größer als angekündigt.");
                hash.AppendData(buffer, 0, read);
                await target.WriteAsync(buffer.AsMemory(0, read), ct);
                if (watch.ElapsedMilliseconds >= 200 || total == asset.Size)
                {
                    progress.Report(new(UpdateStage.Downloading, total * 100d / asset.Size, total, asset.Size, (total - last) / Math.Max(.001, watch.Elapsed.TotalSeconds), "Update wird heruntergeladen …"));
                    last = total; watch.Restart();
                }
            }
            if (total != asset.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(asset.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Die SHA-256-Prüfung ist fehlgeschlagen. Es wurden keine Spieldateien geändert.");
            target.Flush(true);
        }
        catch { try { File.Delete(destination); } catch { } throw; }
    }
    public static string Hash(string path, CancellationToken ct = default)
    {
        using var stream = File.OpenRead(path);
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        byte[] buffer = System.Buffers.ArrayPool<byte>.Shared.Rent(131072);
        try
        {
            while (true)
            {
                ct.ThrowIfCancellationRequested();
                int read = stream.Read(buffer, 0, buffer.Length);
                if (read == 0) break;
                hash.AppendData(buffer, 0, read);
            }
            return Convert.ToHexString(hash.GetHashAndReset());
        }
        finally { System.Buffers.ArrayPool<byte>.Shared.Return(buffer); }
    }
    public void Dispose() => client.Dispose();
}
