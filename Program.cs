// Replace your Program.cs with this (only changes: defaults and optional interpolation)
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;

class Program
{
    static async Task<int> Main(string[] args)
    {
        Console.WriteLine("Simple Video -> GIF converter (FFmpeg must be installed).");

        if (args.Length < 2)
        {
            Console.WriteLine("Usage: dotnet run -- <inputVideo> <outputGif> [ffmpegPath] [fps] [width] [startSec] [durationSec] [interpolate]");
            Console.WriteLine("Example: dotnet run -- input.mp4 output.gif C:\\\\ffmpeg\\\\bin\\\\ffmpeg.exe 25 1920 0 8 interpolate");
            return 1;
        }

        string inputVideo = args[0];
        string outputGif = args[1];
        string ffmpegPath = args.Length >= 3 && !string.IsNullOrWhiteSpace(args[2]) ? args[2] : "ffmpeg";
        int fps = args.Length >= 4 && int.TryParse(args[3], out var _fps) ? _fps : 25;      // default 25
        int width = args.Length >= 5 && int.TryParse(args[4], out var _w) ? _w : 1920;     // default 1920 (HD)
        int startSec = args.Length >= 6 && int.TryParse(args[5], out var _s) ? _s : 0;
        int durationSec = args.Length >= 7 && int.TryParse(args[6], out var _d) ? _d : 8;
        bool doInterpolate = args.Length >= 8 && args[7]?.ToLower() == "interpolate";

        if (!File.Exists(inputVideo))
        {
            Console.Error.WriteLine($"Input file not found: {inputVideo}");
            return 2;
        }

        var outputDir = Path.GetDirectoryName(Path.GetFullPath(outputGif)) ?? Directory.GetCurrentDirectory();
        Directory.CreateDirectory(outputDir);
        string palettePath = Path.Combine(outputDir, $"palette_{Guid.NewGuid():N}.png");

        try
        {
            Console.WriteLine("Generating color palette (first pass)...");
            string trimArgs = BuildTrimArgs(startSec, durationSec);

            string paletteFilter;
            if (doInterpolate)
            {
             
                int interpFps = fps * 2; // e.g. 25 -> 50
                paletteFilter = $"minterpolate='mi_mode=mci:mc_mode=aobmc:vsbmc=1:fps={interpFps}',scale={width}:-1:flags=lanczos,palettegen=stats_mode=full:max_colors=256";
            }
            else
            {
                paletteFilter = $"fps={fps},scale={width}:-1:flags=lanczos,palettegen=stats_mode=full:max_colors=256";
            }

            string paletteArgs = $"{trimArgs}-i \"{inputVideo}\" -vf \"{paletteFilter}\" -y \"{palettePath}\"";
            await RunProcessAsync(ffmpegPath, paletteArgs);

            Console.WriteLine("Creating GIF using palette (second pass)...");
            string paletteUseFilter;
            if (doInterpolate)
            {
                int interpFps = fps * 2;
                paletteUseFilter = $"minterpolate='mi_mode=mci:mc_mode=aobmc:vsbmc=1:fps={interpFps}',scale={width}:-1:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=5";
            }
            else
            {
                paletteUseFilter = $"fps={fps},scale={width}:-1:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=5";
            }

            string paletteUseArgs = $"{trimArgs}-i \"{inputVideo}\" -i \"{palettePath}\" -filter_complex \"{paletteUseFilter}\" -loop 0 -y \"{outputGif}\"";
            await RunProcessAsync(ffmpegPath, paletteUseArgs);

            Console.WriteLine($"Done. GIF created at: {outputGif}");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Error: {ex.Message}");
            return 3;
        }
        finally
        {
            try { if (File.Exists(palettePath)) File.Delete(palettePath); } catch { /* ignore */ }
        }
    }

    static string BuildTrimArgs(int startSec, int durationSec)
    {
        string trim = "";
        if (startSec > 0) trim += $"-ss {startSec} ";
        if (durationSec > 0) trim += $"-t {durationSec} ";
        return trim;
    }

//     static Task RunProcessAsync(string exe, string arguments)
//     {
//         var tcs = new TaskCompletionSource<object?>();
//         var psi = new ProcessStartInfo
//         {
//             FileName = exe,
//             Arguments = arguments,
//             UseShellExecute = false,
//             RedirectStandardError = true,
//             RedirectStandardOutput = true,
//             CreateNoWindow = true
//         };

//         var proc = new Process { StartInfo = psi, EnableRaisingEvents = true };

//         proc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.WriteLine(e.Data); };
//         proc.ErrorDataReceived  += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) Console.Error.WriteLine(e.Data); };

//         proc.Exited += (s, e) =>
//         {
//             if (proc.ExitCode == 0) tcs.TrySetResult(null);
//             else tcs.TrySetException(new Exception($"Process exited with code {proc.ExitCode}"));
//             proc.Dispose();
//         };

//         try
//         {
//             if (!proc.Start()) throw new Exception("Failed to start process: " + exe);
//             proc.BeginOutputReadLine();
//             proc.BeginErrorReadLine();
//         }
//         catch (Exception ex)
//         {
//             tcs.TrySetException(ex);
//         }

//         return tcs.Task;
//     }
// }
