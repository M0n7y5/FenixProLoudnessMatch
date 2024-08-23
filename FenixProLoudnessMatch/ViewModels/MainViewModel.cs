using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Threading.Tasks;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Diagnostics;
using DynamicData;
using FenixProLoudnessMatch.Lang;
using FenixProLoudnessMatch.Models;
using FluentAvalonia.Core;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;

namespace FenixProLoudnessMatch.ViewModels;

static class Exx
{
    internal static async Task WithAggregateException(this Task task)
    {
        // Disable exception throwing using ConfigureAwaitOptions.SuppressThrowing as it
        // will be handled by `task.Wait()`
        await task.ConfigureAwait(ConfigureAwaitOptions.SuppressThrowing);

        // The task is already completed, so Wait only throws an AggregateException if the task failed
        task.Wait();
    }
}

public class MainViewModel : ViewModelBase
{
    // Interaction
    public Interaction<string, string> PickAFolder { get; } = new();

    public Interaction<string[], Unit> MsgBoxError { get; } = new();

    public Interaction<string[], Unit> MsgBoxInfo { get; } = new();

    public Interaction<string[], bool> MsgBoxYesNo { get; } = new();

    public Interaction<string, Unit> AddConsoleLine { get; } = new();

    public Interaction<Unit, Unit> ClearConsole { get; } = new();

    // ---------

    [Reactive]
    public bool IsPathsReadOnly { get; set; } = false;

    [Reactive]
    public string OriginalPath { get; set; } = string.Empty;

    [Reactive]
    public string ReplacementPath { get; set; } = string.Empty;

    [Reactive]
    public string OutputPath { get; set; } = string.Empty;

    [Reactive]
    public string ProgressText { get; set; } = Lang.Resources.progressIdle;

    [Reactive]
    public int ProgressMaximum { get; set; } = 1;

    [Reactive]
    public int ProgressValue { get; set; } = 0;

    public ReactiveCommand<Unit, Unit> PickOriginalFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> PickReplacementFolderCommand { get; }
    public ReactiveCommand<Unit, Unit> PickOutputFolderCommand { get; }

    public ReactiveCommand<Unit, Unit> MatchLoudnessCommand { get; }
    public ReactiveCommand<Unit, Unit> CancelOperationCommand { get; }

    public ReactiveCommand<Unit, Unit> AnalyzeOriginalCommand { get; }
    public ReactiveCommand<Unit, Unit> AnalyzeReplacementCommand { get; }
    public ReactiveCommand<Unit, Unit> PrintDifferencesCommand { get; }

    private CancellationTokenSource _cts = new CancellationTokenSource();

    int maxParralelism = Environment.ProcessorCount <= 4 ? 3 : Environment.ProcessorCount - 1;

    public class LoudnessInfo
    {
        public string OriginalLoudness { get; set; } = string.Empty;

        public string ReplacementLoudness { get; set; } = string.Empty;

        public string SampleRate { get; set; } = string.Empty;
    }

    Dictionary<string, LoudnessInfo> OriginalFiles { get; } = new();

    public MainViewModel()
    {
        this.PickOriginalFolderCommand = ReactiveCommand.CreateFromTask(PickOriginalFolder);
        this.PickReplacementFolderCommand = ReactiveCommand.CreateFromTask(PickReplacementFolder);
        this.PickOutputFolderCommand = ReactiveCommand.CreateFromTask(PickOutputFolder);

        this.MatchLoudnessCommand = ReactiveCommand.CreateFromTask(async () =>
        {
            _cts = new CancellationTokenSource();
            await MatchLoudness(_cts.Token);
        });

        var origPathValid = this.WhenAnyValue(x => x.OriginalPath).Select(x => Directory.Exists(x));
        var replacementPathValid = this.WhenAnyValue(x => x.ReplacementPath, y => y.ReplacementPath)
            .Select(x => Directory.Exists(x.Item1) && Directory.Exists(x.Item2));

        this.AnalyzeOriginalCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                _cts = new CancellationTokenSource();
                OriginalFiles.Clear();
                ScanOriginalDirectory();
                await AnalyzeOriginalFolder(_cts.Token);
            },
            origPathValid
        );

        this.AnalyzeReplacementCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                _cts = new CancellationTokenSource();
                OriginalFiles.Clear();
                ScanOriginalDirectory();
                await AnalyzeReplacementFolder(_cts.Token);
            },
            replacementPathValid
        );

        this.PrintDifferencesCommand = ReactiveCommand.CreateFromTask(
            async () =>
            {
                _cts = new CancellationTokenSource();
                OriginalFiles.Clear();
                ScanOriginalDirectory();
                await AnalyzeOriginalFolder(_cts.Token);
                await AnalyzeReplacementFolder(_cts.Token);
                await PrintDifferences();
            },
            replacementPathValid
        );

        var canCancel = Observable
            .CombineLatest(
                new[]
                {
                    MatchLoudnessCommand.IsExecuting,
                    AnalyzeOriginalCommand.IsExecuting,
                    AnalyzeReplacementCommand.IsExecuting
                }
            )
            .Select(values => values.Any(v => v));

        this.CancelOperationCommand = ReactiveCommand.Create(
            () =>
            {
                _cts.Cancel();
                _cts.Dispose();
            },
            canCancel
        );

        this.MatchLoudnessCommand.ThrownExceptions.Subscribe(async ex =>
        {
            await MsgBoxError
                .Handle(
                    new string[]
                    {
                        Resources.msgErrorTitle,
                        string.Format(Resources.msgErrorFmt, ex.Message)
                    }
                )
                .ToTask();
        });

        this.AnalyzeOriginalCommand.ThrownExceptions.SubscribeOn(RxApp.MainThreadScheduler)
            .Subscribe(async ex =>
            {
                await MsgBoxError
                    .Handle(
                        new string[]
                        {
                            Resources.msgErrorTitle,
                            string.Format(Resources.msgErrorFmt, ex.Message)
                        }
                    )
                    .ToTask();
            });

        this.AnalyzeReplacementCommand.ThrownExceptions.Subscribe(async ex =>
        {
            await MsgBoxError
                .Handle(
                    new string[]
                    {
                        Resources.msgErrorTitle,
                        string.Format(Resources.msgErrorFmt, ex.Message)
                    }
                )
                .ToTask();
        });

        this.CancelOperationCommand.ThrownExceptions.Subscribe(async ex =>
        {
            if (ex is not OperationCanceledException)
            {
                await MsgBoxError
                    .Handle(
                        new string[]
                        {
                            Resources.msgErrorTitle,
                            string.Format(Resources.msgErrorFmt, ex.Message)
                        }
                    )
                    .ToTask();
            }
        });
    }

    async Task PickOriginalFolder()
    {
        var path = await PickAFolder.Handle(Resources.origFolderDialogPickup).ToTask();

        if (path! == string.Empty)
            return;

        OriginalPath = path;
    }

    async Task PickReplacementFolder()
    {
        var path = await PickAFolder.Handle(Resources.replFolderDialogPickup).ToTask();

        if (path! == string.Empty)
            return;

        ReplacementPath = path;
    }

    async Task PickOutputFolder()
    {
        var path = await PickAFolder.Handle(Resources.outputFolderDialogPickup).ToTask();

        if (path! == string.Empty)
            return;

        OutputPath = path;
    }

    async Task GetOriginalSampleRate(CancellationToken ct = default)
    {
        await AddConsoleLine
            .Handle($"Getting Samplerate from Orignal {OriginalFiles.Count} files")
            .ToTask();
        await Parallel.ForEachAsync(
            OriginalFiles,
            new ParallelOptions()
            {
                MaxDegreeOfParallelism = maxParralelism,
                CancellationToken = ct
            },
            async (item, ct) =>
            {
                //TODO: Handle errors
                var (_, stdOut, stdError) = ProcessX.GetDualAsyncEnumerable(
                    $"Libs\\ffprobe.exe -v quiet -print_format json -show_format -show_streams \"{Path.Combine(OriginalPath, item.Key)}\""
                );

                StringBuilder sb = new StringBuilder();

                bool jsonStarted = false;
                await foreach (var err in stdOut)
                {
                    sb.AppendLine(err);
                }

                try
                {
                    var jsonContent = sb.ToString();
                    var output = JsonSerializer.Deserialize<FFProbeOutput>(jsonContent)!;

                    // fully wait for process to finish
                    await stdError.ToTask();

                    var sr = output.Streams.FirstOrDefault(x => x.CodecType == "audio")?.SampleRate;

                    if (sr == null)
                    {
                        throw new Exception("No audio stream found!");
                    }

                    OriginalFiles[item.Key].SampleRate = sr.ToString();
                    await AddConsoleLine.Handle($"{item.Key}: {sr} Hz").ToTask();
                }
                catch (Exception ex)
                {
                    await AddConsoleLine.Handle($"Error: {ex.Message}").ToTask();
                }
            }
        );

        await AddConsoleLine.Handle($"Done!").ToTask();
    }

    async Task AnalyzeOriginalFolder(CancellationToken ct = default)
    {
        //try
        //{
        await GetOriginalSampleRate(ct);

        await AddConsoleLine.Handle($"Analyzing Orignal {OriginalFiles.Count} files").ToTask();

        await Parallel.ForEachAsync(
            OriginalFiles,
            new ParallelOptions()
            {
                MaxDegreeOfParallelism = maxParralelism,
                CancellationToken = ct
            },
            async (item, ct) =>
            {
                ct.ThrowIfCancellationRequested();

                //TODO: Handle errors
                var (p, stdOut, stdError) = ProcessX.GetDualAsyncEnumerable(
                    $"Libs\\ffmpeg.exe -hide_banner -nostats -i \"{Path.Combine(OriginalPath, item.Key)}\" -af loudnorm=print_format=json -f null -"
                );

                ct.ThrowIfCancellationRequested();

                StringBuilder sb = new StringBuilder();

                bool jsonStarted = false;
                await foreach (var err in stdError)
                {
                    if (jsonStarted == false)
                    {
                        if (err == "{")
                        {
                            jsonStarted = true;
                            sb.AppendLine(err);
                            continue;
                        }

                        continue;
                    }
                    else if (err == "}")
                    {
                        sb.AppendLine(err);
                        break;
                    }

                    sb.AppendLine(err);
                }

                try
                {
                    var jsonContent = sb.ToString();
                    var output = JsonSerializer.Deserialize<NormalizationOutput>(jsonContent);

                    // fully wait for process to finish
                    await stdError.ToTask();

                    OriginalFiles[item.Key].OriginalLoudness = output.InputI;
                    await AddConsoleLine
                        .Handle($"{item.Key}: {output.InputI} LUFS (Integrated)")
                        .ToTask();
                }
                catch (Exception ex)
                {
                    await AddConsoleLine.Handle($"Error: {ex.Message}").ToTask();
                }
            }
        );

        //}
        //catch (Exception)
        //{
        //    throw;
        //}

        await AddConsoleLine.Handle($"Done!").ToTask();
    }

    async Task AnalyzeReplacementFolder(CancellationToken ct = default)
    {
        await AddConsoleLine.Handle($"Analyzing Replacement {OriginalFiles.Count} files").ToTask();

        await Parallel.ForEachAsync(
            OriginalFiles,
            new ParallelOptions()
            {
                MaxDegreeOfParallelism = maxParralelism,
                CancellationToken = ct
            },
            async (item, ct) =>
            {
                ct.ThrowIfCancellationRequested();
                //TODO: Handle errors
                var (_, stdOut, stdError) = ProcessX.GetDualAsyncEnumerable(
                    $"Libs\\ffmpeg.exe -hide_banner -nostats -i \"{Path.Combine(ReplacementPath, item.Key)}\" -af loudnorm=print_format=json -f null -"
                );

                StringBuilder sb = new StringBuilder();

                bool jsonStarted = false;
                await foreach (var err in stdError)
                {
                    if (jsonStarted == false)
                    {
                        if (err == "{")
                        {
                            jsonStarted = true;
                            sb.AppendLine(err);
                            continue;
                        }

                        continue;
                    }
                    else if (err == "}")
                    {
                        sb.AppendLine(err);
                        break;
                    }

                    sb.AppendLine(err);
                }

                var jsonContent = sb.ToString();
                var output = JsonSerializer.Deserialize<NormalizationOutput>(jsonContent);

                // fully wait for process to finish
                await stdError.ToTask();

                if (OriginalFiles.TryGetValue(item.Key, out var info))
                {
                    info.ReplacementLoudness = output.InputI;
                    await AddConsoleLine
                        .Handle($"{item.Key}: {output.InputI} LUFS (Integrated)")
                        .ToTask();
                }
                else
                {
                    await AddConsoleLine.Handle($"MISSING: {item.Key}!").ToTask();
                }
            }
        );

        await AddConsoleLine.Handle($"Done!").ToTask();
    }

    async Task PrintDifferences()
    {
        await AddConsoleLine.Handle($"").ToTask();
        await AddConsoleLine.Handle($"Printing Differences").ToTask();

        foreach (var f in OriginalFiles)
        {
            var original = double.Parse(
                f.Value.OriginalLoudness.Replace(',', '.'),
                CultureInfo.InvariantCulture
            );
            var replacement = double.Parse(
                f.Value.ReplacementLoudness.Replace(',', '.'),
                CultureInfo.InvariantCulture
            );
            var originalAbs = Math.Abs(original);
            var replacementAbs = Math.Abs(replacement);

            var diff = Math.Abs(originalAbs - replacementAbs);

            await AddConsoleLine
                .Handle(
                    $"diff: {diff:00.00} LUFS\tO: {original:00.00}\tR: {replacement:00.00}\t{f.Key}"
                )
                .ToTask();
        }
    }

    void ScanOriginalDirectory()
    {
        foreach (
            var fs in Directory
                .EnumerateFiles(OriginalPath)
                .Where(f => f.EndsWith(".mp3") || f.EndsWith(".wav") || f.EndsWith(".ogg"))
        )
        {
            var filename = Path.GetFileName(fs);

            OriginalFiles.Add(filename, new());
        }
    }

    async Task MatchLoudness(CancellationToken ct = default)
    {
        IsPathsReadOnly = true;

        try
        {
            if (
                string.IsNullOrWhiteSpace(OriginalPath)
                || string.IsNullOrWhiteSpace(ReplacementPath)
                || string.IsNullOrWhiteSpace(OutputPath)
            )
                throw new Exception("Paths can't be empty!");

            if (Directory.Exists(OriginalPath) == false)
                throw new Exception("Set Original Path doesn't exist!");

            if (Directory.Exists(ReplacementPath) == false)
                throw new Exception("Set Replacement Path doesn't exist!");

            if (Directory.Exists(OutputPath) == false)
                throw new Exception("Set Output Path doesn't exist!");

            var proceed = await MsgBoxYesNo
                .Handle(
                    new string[]
                    {
                        "Warning!",
                        "Following operation will delete all the existing files in the output folder! Do you want to proceed?"
                    }
                )
                .ToTask();

            if (proceed == false)
            {
                IsPathsReadOnly = false;
                return;
            }

            OriginalFiles.Clear();

            Directory.Delete(OutputPath, true);

            Directory.CreateDirectory(OutputPath);

            ScanOriginalDirectory();

            foreach (var item in OriginalFiles.Keys)
            {
                if (File.Exists(Path.Combine(ReplacementPath, item)) == false)
                {
                    throw new Exception(
                        $"Files don't match! File {item} doesn't exist in Replacement Folder!"
                    );
                }
            }

            await AnalyzeOriginalFolder(ct);

            await AnalyzeReplacementFolder(ct);

            await PrintDifferences();

            // matching the loudness
            await AddConsoleLine.Handle($"Mathing the loudness ...").ToTask();
            await Parallel.ForEachAsync(
                OriginalFiles,
                new ParallelOptions()
                {
                    MaxDegreeOfParallelism = maxParralelism,
                    CancellationToken = ct
                },
                async (item, ct) =>
                {
                    ct.ThrowIfCancellationRequested();
                    //TODO: Handle errors
                    // -i track246.wav -af loudnorm=I=-28.4 track246_new.wav
                    var (_, stdOut, stdError) = ProcessX.GetDualAsyncEnumerable(
                        $"Libs\\ffmpeg.exe -hide_banner -nostats -i \"{Path.Combine(ReplacementPath, item.Key)}\" -ar {item.Value.SampleRate} -af loudnorm=I={item.Value.OriginalLoudness} \"{Path.Combine(OutputPath, item.Key)}\""
                    );

                    // fully wait for process to finish
                    await stdError.ToTask();

                    //OriginalFiles[item.Key].ReplacementLoudness = output.InputI;
                    await AddConsoleLine.Handle($"{item.Key} Done!").ToTask();
                }
            );

            await AddConsoleLine.Handle($"All Done!").ToTask();
        }
        finally
        {
            IsPathsReadOnly = false;
        }
    }
}
