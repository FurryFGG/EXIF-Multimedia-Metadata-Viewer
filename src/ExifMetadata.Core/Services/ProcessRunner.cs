using System.Diagnostics;
using System.Text;

namespace ExifMetadata.Core.Services;

internal sealed record ProcessExecution(
    string StandardOutput,
    string StandardError,
    int? ExitCode,
    TimeSpan Elapsed,
    bool TimedOut);

internal sealed record BinaryProcessExecution(
    byte[] StandardOutput,
    string StandardError,
    int? ExitCode,
    TimeSpan Elapsed,
    bool TimedOut);

internal static class ProcessRunner
{
    public static async Task<ProcessExecution> RunTextAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken,
        string? standardInput = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var startInfo = CreateStartInfo(executable, arguments, workingDirectory);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardOutputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        startInfo.StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        if (standardInput is not null)
        {
            startInfo.RedirectStandardInput = true;
            startInfo.StandardInputEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);
        }

        using var process = Start(startInfo);
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();
        var inputTask = standardInput is null
            ? Task.CompletedTask
            : WriteStandardInputAsync(process, standardInput);
        var exit = await WaitForExitAsync(process, timeout, cancellationToken).ConfigureAwait(false);
        await inputTask.ConfigureAwait(false);
        return new ProcessExecution(
            await outputTask.ConfigureAwait(false),
            await errorTask.ConfigureAwait(false),
            exit.ExitCode,
            exit.Elapsed,
            exit.TimedOut);
    }

    public static async Task<BinaryProcessExecution> RunBinaryAsync(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var startInfo = CreateStartInfo(executable, arguments, workingDirectory);
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardErrorEncoding = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

        using var process = Start(startInfo);
        using var memory = new MemoryStream();
        var outputTask = process.StandardOutput.BaseStream.CopyToAsync(memory);
        var errorTask = process.StandardError.ReadToEndAsync();
        var exit = await WaitForExitAsync(process, timeout, cancellationToken).ConfigureAwait(false);
        await outputTask.ConfigureAwait(false);
        return new BinaryProcessExecution(
            memory.ToArray(),
            await errorTask.ConfigureAwait(false),
            exit.ExitCode,
            exit.Elapsed,
            exit.TimedOut);
    }

    private static ProcessStartInfo CreateStartInfo(
        string executable,
        IEnumerable<string> arguments,
        string workingDirectory)
    {
        var info = new ProcessStartInfo
        {
            FileName = executable,
            WorkingDirectory = workingDirectory,
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        foreach (var argument in arguments)
            info.ArgumentList.Add(argument);

        // Strawberry Perl on the Windows ExifTool package warns when the
        // inherited POSIX-style C.UTF-8 locale is unavailable on Windows.
        // The JSON remains UTF-8 and filenames are still passed as Unicode argv.
        info.Environment["LC_ALL"] = "C";
        return info;
    }

    private static async Task WriteStandardInputAsync(Process process, string value)
    {
        try
        {
            await process.StandardInput.WriteAsync(value).ConfigureAwait(false);
        }
        catch (IOException)
        {
        }
        finally
        {
            process.StandardInput.Close();
        }
    }

    private static Process Start(ProcessStartInfo startInfo)
    {
        var process = new Process { StartInfo = startInfo };
        try
        {
            if (!process.Start())
                throw new InvalidOperationException($"Could not start {startInfo.FileName}.");
            return process;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            process.Dispose();
            throw new InvalidOperationException($"Could not start parser: {startInfo.FileName}", exception);
        }
    }

    private static async Task<(int? ExitCode, TimeSpan Elapsed, bool TimedOut)> WaitForExitAsync(
        Process process,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var timer = Stopwatch.StartNew();
        try
        {
            await process.WaitForExitAsync(timeoutSource.Token).ConfigureAwait(false);
            timer.Stop();
            return (process.ExitCode, timer.Elapsed, false);
        }
        catch (OperationCanceledException)
        {
            TryKill(process);
            await WaitForExitIgnoringErrorsAsync(process).ConfigureAwait(false);
            timer.Stop();
            if (cancellationToken.IsCancellationRequested)
                throw;
            return (null, timer.Elapsed, true);
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
                process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
        }
        catch (System.ComponentModel.Win32Exception)
        {
        }
    }

    private static async Task WaitForExitIgnoringErrorsAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
        }
    }

}
