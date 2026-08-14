using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ExplorerPreview.Rendering;

public sealed class OfficeConversionResult
{
    public bool Success { get; init; }
    public string? PdfPath { get; init; }
    public string? FailureReason { get; init; }

    public static OfficeConversionResult Ok(string path) => new() { Success = true, PdfPath = path };
    public static OfficeConversionResult Fail(string reason) => new() { Success = false, FailureReason = reason };
}

/// <summary>
/// Wandelt Office-Dokumente über eine lokale LibreOffice-Installation (headless)
/// in PDF um, statt sie mit der echten Office-Engine im Preview-Handler zu öffnen.
///
/// Sicherheits-Überlegung (siehe Diskussion zu MOTW-Warnungen):
/// - LibreOffice führt bei der reinen "--convert-to pdf"-Konvertierung keine
///   Makros aus (kein aktiver Code-Pfad wie bei "Dokument öffnen + Makros aktivieren").
/// - Jede Konvertierung läuft mit einem frischen, isolierten Nutzerprofil
///   (-env:UserInstallation=...), damit ein manipuliertes Dokument nicht dauerhaft
///   Zustand/Erweiterungen in einem gemeinsamen Profil hinterlassen kann.
/// - Es gibt einen harten Timeout, damit ein absichtlich pathologisches Dokument
///   (Zip-Bomb-artige Office-Datei, Endlosschleife im Layout) den Preview-Prozess
///   nicht dauerhaft blockiert.
/// - Das ist eine Verbesserung gegenüber "Datei blind im echten Office öffnen",
///   aber KEINE vollständige Sandbox (kein AppContainer/keine VM). Für wirklich
///   nicht vertrauenswürdige Quellen wäre ein zusätzlicher Prozess-Isolationslayer
///   (z.B. Windows Sandbox / Job Object mit restriktivem Token) ein sinnvoller
///   nächster Ausbauschritt - hier bewusst als bekannte Grenze dokumentiert statt
///   stillschweigend übergangen.
/// </summary>
public sealed class OfficeConverter
{
    public async Task<OfficeConversionResult> ConvertToPdfAsync(string sourcePath, CancellationToken ct)
    {
        if (!File.Exists(AppSettings.LibreOfficePath))
        {
            return OfficeConversionResult.Fail(
                $"LibreOffice nicht gefunden unter \"{AppSettings.LibreOfficePath}\". " +
                "Pfad in AppSettings.LibreOfficePath anpassen oder LibreOffice installieren.");
        }

        string outDir = Path.Combine(Path.GetTempPath(), "ExplorerPreview", "office-out", Guid.NewGuid().ToString("N"));
        string profileDir = Path.Combine(Path.GetTempPath(), "ExplorerPreview", "office-profile", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(outDir);
        Directory.CreateDirectory(profileDir);

        var psi = new ProcessStartInfo
        {
            FileName = AppSettings.LibreOfficePath,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("--headless");
        psi.ArgumentList.Add("--norestore");
        psi.ArgumentList.Add($"-env:UserInstallation=file:///{profileDir.Replace('\\', '/')}");
        psi.ArgumentList.Add("--convert-to");
        psi.ArgumentList.Add("pdf");
        psi.ArgumentList.Add("--outdir");
        psi.ArgumentList.Add(outDir);
        psi.ArgumentList.Add(sourcePath);

        try
        {
            using var process = new Process { StartInfo = psi };
            process.Start();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(AppSettings.OfficeConversionTimeout);

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException)
            {
                TryKill(process);
                return OfficeConversionResult.Fail(
                    $"Konvertierung hat länger als {AppSettings.OfficeConversionTimeout.TotalSeconds:0}s gedauert und wurde abgebrochen.");
            }

            if (process.ExitCode != 0)
            {
                string stderr = await process.StandardError.ReadToEndAsync(ct);
                return OfficeConversionResult.Fail($"LibreOffice-Fehler (Exit {process.ExitCode}): {stderr}");
            }

            string expectedPdf = Path.Combine(outDir, Path.GetFileNameWithoutExtension(sourcePath) + ".pdf");
            if (!File.Exists(expectedPdf))
            {
                return OfficeConversionResult.Fail("Konvertierung lief durch, aber keine PDF-Ausgabedatei gefunden.");
            }

            return OfficeConversionResult.Ok(expectedPdf);
        }
        catch (Exception ex)
        {
            return OfficeConversionResult.Fail($"Konvertierung fehlgeschlagen: {ex.Message}");
        }
        finally
        {
            TryDeleteDirectory(profileDir);
        }
    }

    private static void TryKill(Process process)
    {
        try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
    }

    private static void TryDeleteDirectory(string dir)
    {
        try { Directory.Delete(dir, recursive: true); } catch { /* best effort, profile locking etc. */ }
    }
}
