using System.Security.Cryptography;
using System.Text;

namespace ExplorerPreview.Cache;

public sealed class LocalCopyResult
{
    public bool Success { get; init; }
    public string? LocalPath { get; init; }
    public string? FailureReason { get; init; }

    public static LocalCopyResult Ok(string path) => new() { Success = true, LocalPath = path };
    public static LocalCopyResult Fail(string reason) => new() { Success = false, FailureReason = reason };
}

/// <summary>
/// Kopiert Dateien (insbesondere von Netzlaufwerken/NAS) zuerst in einen lokalen
/// Cache, bevor irgendein Renderer sie anfasst. Das entkoppelt das Rendering
/// komplett von Netzwerklatenz/-ausfällen: entweder die Kopie klappt innerhalb
/// des Timeouts, oder wir zeigen sauber "nicht verfügbar" statt die UI hängen
/// zu lassen (das Kernproblem, das die native Windows-Vorschau hat).
/// </summary>
public sealed class PreviewCache
{
    private readonly string _cacheDir;

    public PreviewCache()
    {
        _cacheDir = AppSettings.CacheDirectory;
        Directory.CreateDirectory(_cacheDir);
    }

    /// <summary>
    /// Liefert einen lokalen, garantiert vollständig kopierten Pfad zur Datei.
    /// Nutzt einen Cache-Eintrag wieder, solange Größe + letztes Änderungsdatum
    /// der Quelle unverändert sind - das macht ein zweites Öffnen sofort schnell,
    /// selbst wenn das NAS gerade lahmt.
    /// </summary>
    public async Task<LocalCopyResult> GetLocalCopyAsync(string sourcePath, CancellationToken ct = default)
    {
        FileInfo sourceInfo;
        try
        {
            sourceInfo = new FileInfo(sourcePath);
            if (!sourceInfo.Exists)
            {
                return LocalCopyResult.Fail("Datei nicht gefunden.");
            }
        }
        catch (Exception ex)
        {
            return LocalCopyResult.Fail($"Datei nicht erreichbar: {ex.Message}");
        }

        string cacheKey = BuildCacheKey(sourcePath, sourceInfo.Length, sourceInfo.LastWriteTimeUtc);
        string destPath = Path.Combine(_cacheDir, cacheKey + Path.GetExtension(sourcePath));

        if (File.Exists(destPath))
        {
            return LocalCopyResult.Ok(destPath);
        }

        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeoutCts.CancelAfter(AppSettings.NetworkCopyTimeout);

        const int maxAttempts = 2;
        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            try
            {
                await CopyWithShareAsync(sourcePath, destPath, timeoutCts.Token);
                EnforceCacheSizeLimit();
                return LocalCopyResult.Ok(destPath);
            }
            catch (OperationCanceledException)
            {
                TryDeletePartial(destPath);
                return LocalCopyResult.Fail(
                    $"Timeout beim Kopieren (>{AppSettings.NetworkCopyTimeout.TotalSeconds:0}s) - Quelle vermutlich langsam/nicht erreichbar (NAS/Netzwerk).");
            }
            catch (IOException) when (attempt < maxAttempts)
            {
                // Kurzer, transienter Netzwerkfehler - einmal erneut versuchen.
                await Task.Delay(250, ct);
            }
            catch (Exception ex)
            {
                TryDeletePartial(destPath);
                return LocalCopyResult.Fail($"Kopieren fehlgeschlagen: {ex.Message}");
            }
        }

        return LocalCopyResult.Fail("Kopieren nach mehreren Versuchen fehlgeschlagen.");
    }

    private static async Task CopyWithShareAsync(string source, string dest, CancellationToken ct)
    {
        // FileShare.ReadWrite: verhindert, dass wir eine Datei blockieren, die
        // der User/eine andere App gerade noch offen hat (typisch bei NAS-Shares).
        await using var input = new FileStream(source, FileMode.Open, FileAccess.Read,
            FileShare.ReadWrite, bufferSize: 81920, useAsync: true);
        string tmpDest = dest + ".tmp";
        await using (var output = new FileStream(tmpDest, FileMode.Create, FileAccess.Write,
            FileShare.None, bufferSize: 81920, useAsync: true))
        {
            await input.CopyToAsync(output, ct);
        }
        File.Move(tmpDest, dest, overwrite: true);
    }

    private static void TryDeletePartial(string dest)
    {
        try { File.Delete(dest + ".tmp"); } catch { /* best effort */ }
    }

    private static string BuildCacheKey(string path, long length, DateTime lastWriteUtc)
    {
        string raw = $"{path}|{length}|{lastWriteUtc:O}";
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(raw));
        return Convert.ToHexString(hash)[..32];
    }

    /// <summary>Simple LRU-artige Bereinigung: älteste Dateien löschen, bis unter dem Limit.</summary>
    private void EnforceCacheSizeLimit()
    {
        var files = new DirectoryInfo(_cacheDir).GetFiles()
            .OrderBy(f => f.LastAccessTimeUtc)
            .ToList();

        long total = files.Sum(f => f.Length);
        int i = 0;
        while (total > AppSettings.MaxCacheBytes && i < files.Count)
        {
            try
            {
                total -= files[i].Length;
                files[i].Delete();
            }
            catch { /* best effort, Datei evtl. gerade in Benutzung */ }
            i++;
        }
    }
}
