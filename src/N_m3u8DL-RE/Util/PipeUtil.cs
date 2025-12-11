using N_m3u8DL_RE.Common.Log;
using Spectre.Console;
using System.Diagnostics;
using System.IO.Pipes;
using System.Text;
using N_m3u8DL_RE.Config;

namespace N_m3u8DL_RE.Util;

internal static class PipeUtil
{
    public static Stream CreatePipe(string pipeName)
    {
        if (OperatingSystem.IsWindows())
        {
            return new NamedPipeServerStream(pipeName, PipeDirection.InOut);
        }

        var path = Path.Combine(Path.GetTempPath(), pipeName);
        using var p = new Process();
        p.StartInfo = new ProcessStartInfo()
        {
            FileName = "mkfifo",
            Arguments = path,
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
        };
        p.Start();
        p.WaitForExit();
        Thread.Sleep(200);
        return new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.Read);
    }

    public static async Task<bool> StartPipeMuxAsync(string binary, string? options, string[] pipeNames, string outputPath)
    {
        return await Task.Run(async () =>
        {
            await Task.Delay(1000);
            return StartPipeMux(binary, options, pipeNames, outputPath);
        });
    }

    public static bool StartPipeMux(string binary, string? options, string[] pipeNames, string outputPath)
    {
        var dateString = DateTime.Now.ToString("o");
        var command = new StringBuilder(!string.IsNullOrEmpty(options) ? options : "-y -fflags +genpts -loglevel quiet ");

        var customDest = OtherUtil.GetEnvironmentVariable(EnvConfigKey.ReLivePipeOptions);
        var pipeDir = OtherUtil.GetEnvironmentVariable(EnvConfigKey.ReLivePipeTmpDir, Path.GetTempPath());

        if (string.IsNullOrEmpty(options) && !string.IsNullOrEmpty(customDest))
        {
            command.Append(" -re ");
        }

        // 输入部分
        StringBuilder inputs = new StringBuilder("");
        foreach (var item in pipeNames)
        {
            if (OperatingSystem.IsWindows())
                inputs.Append($" -i \"\\\\.\\pipe\\{item}\" ");
            else
                // command.Append($" -i \"unix://{Path.Combine(Path.GetTempPath(), $"CoreFxPipe_{item}")}\" ");
                inputs.Append($" -i \"{Path.Combine(pipeDir, item)}\" ");
        }

        for (var i = 0; i < pipeNames.Length; i++)
        {
            inputs.Append($" -map {i} ");
        }

        if (string.IsNullOrEmpty(options))
        {
            command.Append(inputs);
            command.Append(" -strict unofficial -c copy ");
            command.Append($" -metadata date=\"{dateString}\" ");
            command.Append($" -ignore_unknown -copy_unknown ");


            if (!string.IsNullOrEmpty(customDest))
            {
                if (customDest.Trim().StartsWith('-'))
                    command.Append(customDest);
                else
                    command.Append($" -f mpegts -shortest \"{customDest}\"");
                Logger.WarnMarkUp($"[deepskyblue1]{command.ToString().EscapeMarkup()}[/]");
            }
            else
            {
                command.Append($" -f mpegts -shortest \"{outputPath}\"");
            }
        }
        else
        {
            command.Replace("{INPUTS}", $"{inputs}");
            command.Replace("{DATE}", $"\"{dateString}\"");
            Logger.WarnMarkUp($"[deepskyblue1]{command.ToString().EscapeMarkup()}[/]");
        }

        using var p = new Process();
        p.StartInfo = new ProcessStartInfo()
        {
            WorkingDirectory = Environment.CurrentDirectory,
            FileName = binary,
            Arguments = command.ToString(),
            CreateNoWindow = true,
            UseShellExecute = false
        };
        // p.StartInfo.Environment.Add("FFREPORT", "file=ffreport.log:level=42");
        p.Start();
        p.WaitForExit();

        return p.ExitCode == 0;
    }
}
