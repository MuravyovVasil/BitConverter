using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using BitConverter.Core;

namespace BitConverter.UI.ViewModels;

/// <summary>
/// Main ViewModel handling the UI logic, file processing pipeline, and real-time metrics.
/// </summary>
public partial class MainWindowViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progressValue;

    [ObservableProperty]
    private string _logText = "Waiting for file input...\n";

    private CancellationTokenSource? _cts;

    public async Task ProcessDroppedFileAsync(string filePath)
    {
        if (IsBusy || string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath)) return;

        long fileSize = new FileInfo(filePath).Length;
        if (fileSize == 0)
        {
            AppendLog($"[ERROR] File '{Path.GetFileName(filePath)}' is empty (0 bytes). Processing aborted.\n\n");
            return;
        }

        IsBusy = true;
        ProgressValue = 0;
        _cts = new CancellationTokenSource();

        try
        {
            var progress = new Progress<double>(p =>
            {
                if (double.IsNaN(p) || double.IsInfinity(p)) return;

                Dispatcher.UIThread.Post(() =>
                {
                    ProgressValue = Math.Clamp(p, 0, 100);
                });
            });

            if (filePath.EndsWith(".mbit", StringComparison.OrdinalIgnoreCase))
            {
                AppendLog($"Starting decompression pipeline: {Path.GetFileName(filePath)}\n");

                string directory = Path.GetDirectoryName(filePath) ?? string.Empty;
                string originalFileName = Path.GetFileNameWithoutExtension(filePath);
                string originalExtension = Path.GetExtension(originalFileName);
                string nameWithoutExt = Path.GetFileNameWithoutExtension(originalFileName);

                string safeFileName = $"{nameWithoutExt}_restored{originalExtension}";
                string outPath = Path.Combine(directory, safeFileName);

                var reverseEngine = new ReverseEngine();
                var stopwatch = Stopwatch.StartNew();

                await Task.Run(() => reverseEngine.ReverseFileAsync(filePath, outPath, progress, _cts.Token));

                stopwatch.Stop();

                long restoredSize = new FileInfo(outPath).Length;
                double seconds = stopwatch.Elapsed.TotalSeconds;
                if (seconds < 0.01) seconds = 0.01;

                double restoredSizeMb = restoredSize / 1048576.0;
                double throughput = restoredSizeMb / seconds;

                AppendLog($"[OK] File successfully restored as: {safeFileName}\n" +
                          $"Extraction time: {seconds:F2} sec\n" +
                          $"Throughput: {throughput:F1} MB/s\n" +
                          $"Restored size: {FormatSize(restoredSize)}\n------------------------\n\n");
            }
            else
            {
                AppendLog($"Starting compression pipeline: {Path.GetFileName(filePath)}\n");
                string outPath = filePath + ".mbit";

                var engine = new PipelineEngine();
                long originalSize = new FileInfo(filePath).Length;

                // Select compression profile
                var options = CompressionOptions.Ultra();
                AppendLog($"Profile: Ultra (Block: {options.BlockSize / 1048576}MB, Threads: {options.MaxConcurrency})\n");

                var stopwatch = Stopwatch.StartNew();

                // Run the processing pipeline
                await Task.Run(() => engine.ProcessFileAsync(filePath, outPath, progress, _cts.Token, options));

                stopwatch.Stop();

                long compressedSize = new FileInfo(outPath).Length;
                double seconds = stopwatch.Elapsed.TotalSeconds;
                if (seconds < 0.01) seconds = 0.01;

                // Calculate performance metrics
                double originalSizeMb = originalSize / 1048576.0;
                double throughput = originalSizeMb / seconds;
                double savedPercent = 100.0 - ((double)compressedSize / originalSize * 100.0);

                // Display 0% if data is incompressible
                if (savedPercent < 0) savedPercent = 0;

                string report = $"[OK] Successfully processed: {Path.GetFileName(outPath)}\n" +
                                $"Compression time: {seconds:F2} sec\n" +
                                $"Throughput: {throughput:F1} MB/s\n" +
                                $"Original: {FormatSize(originalSize)} -> Compressed: {FormatSize(compressedSize)}\n" +
                                $"Space saved: {savedPercent:F2}%\n" +
                                "------------------------\n\n";

                AppendLog(report);
            }

            // Force 100% for visual completion
            Dispatcher.UIThread.Post(() => ProgressValue = 100);
        }
        catch (OperationCanceledException)
        {
            AppendLog("[CANCELLED] Operation aborted by user.\n\n");
        }
        catch (Exception ex)
        {
            AppendLog($"[ERROR] Pipeline failure: {ex.Message}\n\n");
        }
        finally
        {
            Dispatcher.UIThread.Post(() => IsBusy = false);
        }
    }

    public void Cancel()
    {
        _cts?.Cancel();
    }

    private void AppendLog(string message)
    {
        Dispatcher.UIThread.Post(() => LogText += message);
    }

    private string FormatSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB", "TB" };
        int order = 0;
        double len = bytes;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}