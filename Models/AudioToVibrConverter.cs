using NAudio.Dsp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using vibrant.Models;

namespace vibrant.Converter {
    public static class AudioToVibrConverter {
        const int WindowSize = 256;
        const int HopSize = 128;
        const double SectionDuration = 4.0;
        const int ThumbnailSize = 256;

        const int AmpMax = 127;
        const int FreqMax = 99;


        // Local ThirdParty tools shipped with the project
        static readonly string ffmpegPath = GetToolPath("ffmpeg.exe");
        static readonly string demucsPath = GetToolPath("demucs_cli.exe");

        // =========================
        // DEMUCS SPLIT
        // =========================
        public static async Task SplitMP3Async(string filePath) {
            await Task.Run(() =>
            {
                Process process = new Process();
                // Local ThirdParty tools shipped with the project
                process.StartInfo.FileName = demucsPath;
                process.StartInfo.Arguments =
                    "\"" + filePath + "\" -o " +
                    DirectoryConfig.tempFilesLocation +
                    " -n htdemucs";
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.StartInfo.CreateNoWindow = true;

                process.OutputDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        Debug.WriteLine("[demucs] " + e.Data);
                };
                process.ErrorDataReceived += (s, e) =>
                {
                    if (!string.IsNullOrWhiteSpace(e.Data))
                        Debug.WriteLine("[demucs-err] " + e.Data);
                };
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();
                process.WaitForExit();

                if (process.ExitCode != 0)
                {
                    MessageBox.Show(
                        "demucs failed (exit " + process.ExitCode + ").\n" +
                        "Check Output window (Debug) for details.",
                        "Audio Import Error",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                string modelDir = Path.Combine(
                    DirectoryConfig.tempFilesLocation,
                    "htdemucs"
                );

                if (!Directory.Exists(modelDir))
                    return;

                string songDir = Directory.GetDirectories(modelDir)
                    .OrderByDescending(Directory.GetCreationTime)
                    .FirstOrDefault();

                if (songDir == null)
                    return;

                List<string> stems = Directory
                    .GetFiles(songDir)
                    .Where(f =>
                        f.EndsWith(".wav", StringComparison.OrdinalIgnoreCase) ||
                        f.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => StemOrderKey(Path.GetFileNameWithoutExtension(f)))
                    .ToList();

                if (stems.Count == 0)
                    return;

                EnsureSongMp3(origPath: filePath, songDir: songDir);

                ConstructVibr(stems, filePath);

                // Directory.Delete(songDir, true); // keep stems for inspection/debugging
            });
        }



        // =========================
        // MAIN ENTRY
        // =========================
        public static void ConstructVibr(List<string> filePaths, string origPath) {
            if (filePaths == null || filePaths.Count == 0)
                return;

            string outputPath = Path.Combine(
                DirectoryConfig.vibrFilesLocation,
                Path.GetFileNameWithoutExtension(origPath) + ".vibr"
            );

            string title = "Unknown Title";
            string artist = "Unknown Artist";
            Bitmap albumArt = null;

            // === METADATA (from first stem) ===
            try {
                TagLib.File tagFile = TagLib.File.Create(origPath);
                if (!string.IsNullOrEmpty(tagFile.Tag.Title))
                    title = tagFile.Tag.Title;

                if (!string.IsNullOrEmpty(tagFile.Tag.FirstPerformer))
                    artist = tagFile.Tag.FirstPerformer;

                if (tagFile.Tag.Pictures.Length > 0) {
                    using (MemoryStream ms =
                        new MemoryStream(tagFile.Tag.Pictures[0].Data.Data)) {
                        albumArt = new Bitmap(ms);
                        albumArt = new Bitmap(albumArt, ThumbnailSize, ThumbnailSize);
                    }
                }
            }
            catch { }

            // === PROCESS EACH STEM SEPARATELY ===
            List<List<int>> allAmps = new List<List<int>>();
            List<List<int>> allFreqs = new List<List<int>>();

            foreach (string layer in filePaths) {
                float[] samples = DecodeAudio(layer, 3000);
                if (samples == null || samples.Length < WindowSize)
                    continue;

                List<int> layerAmps = new List<int>();
                List<int> layerFreqs = new List<int>();

                ProcessLayer(samples, 3000, layerAmps, layerFreqs);

                allAmps.Add(layerAmps);
                allFreqs.Add(layerFreqs);
            }

            if (allAmps.Count == 0)
                return;

            // === FRAME ALIGNMENT ===
            int frameCount = allAmps.Min(a => a.Count);

            // === WRITE FILE ===
            using (StreamWriter writer = new StreamWriter(outputPath)) {
                writer.WriteLine(title);
                writer.WriteLine(artist);

                if (albumArt != null) {
                    for (int y = 0; y < albumArt.Height; y++)
                        for (int x = 0; x < albumArt.Width; x++) {
                            Color c = albumArt.GetPixel(x, y);
                            writer.Write(c.R + " " + c.G + " " + c.B + " ");
                        }
                    writer.WriteLine();
                }
                else {
                    writer.WriteLine("NO_ALBUM_ART");
                }

                // === PER-FRAME, SEMICOLON-SEPARATED CHANNELS ===
                for (int frame = 0; frame < frameCount; frame++) {
                    List<string> parts = new List<string>();

                    for (int ch = 0; ch < allAmps.Count; ch++) {
                        parts.Add(
                            allAmps[ch][frame] + "," +
                            allFreqs[ch][frame]
                        );
                    }

                    writer.WriteLine(string.Join(";", parts));
                }
            }

        }


        // =========================
        // FFT + ANALYSIS
        // =========================
        private static void ProcessLayer(
            float[] samples,
            int sampleRate,
            List<int> ampOut,
            List<int> freqOut) {
            float[] window = new float[WindowSize];
            for (int i = 0; i < WindowSize; i++)
                window[i] = (float)(0.5 - 0.5 *
                    Math.Cos(2 * Math.PI * i / WindowSize));

            double[] freqs = new double[WindowSize / 2 + 1];
            for (int i = 0; i < freqs.Length; i++)
                freqs[i] = i * sampleRate / (double)WindowSize;

            List<double> amps = new List<double>();
            List<double> freqAvg = new List<double>();

            for (int i = 0; i + WindowSize < samples.Length; i += HopSize) {
                Complex[] buf = new Complex[WindowSize];
                for (int j = 0; j < WindowSize; j++)
                    buf[j].X = samples[i + j] * window[j];

                FastFourierTransform.FFT( true, (int)(Math.Log(WindowSize) / Math.Log(2)), buf);

                double magSum = 0;
                double freqSum = 0;

                for (int k = 0; k < freqs.Length; k++) {
                    double mag = Math.Sqrt(
                        buf[k].X * buf[k].X +
                        buf[k].Y * buf[k].Y);

                    magSum += mag;
                    freqSum += freqs[k] * mag;
                }

                amps.Add(magSum > 0 ? magSum / freqs.Length : 0);
                freqAvg.Add(magSum > 0 ? freqSum / magSum : 0);
            }

            int sectionSize =
                Math.Max(1, (int)(SectionDuration * sampleRate / HopSize));

            int[] na = NormalizeAmplitudesRobust(
                amps.ToArray(), sectionSize);
            int[] nf = NormalizeFrequenciesWeighted(
                freqAvg.ToArray(), amps.ToArray());

            ampOut.AddRange(na);
            freqOut.AddRange(nf);
        }

        // =========================
        // FFMPEG DECODE
        // =========================
        private static float[] DecodeAudio(string input, int sampleRate) {
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = ffmpegPath;
            psi.Arguments =
                "-i \"" + input +
                "\" -f f32le -ac 1 -ar " +
                sampleRate + " pipe:1";
            psi.RedirectStandardOutput = true;
            psi.UseShellExecute = false;
            psi.CreateNoWindow = true;

            using (Process p = Process.Start(psi))
            using (MemoryStream ms = new MemoryStream()) {
                p.StandardOutput.BaseStream.CopyTo(ms);
                p.WaitForExit();

                byte[] bytes = ms.ToArray();
                float[] samples = new float[bytes.Length / 4];
                Buffer.BlockCopy(bytes, 0, samples, 0, bytes.Length);
                return samples;
            }
        }

        // =========================
        // NORMALIZATION
        // =========================
        private static int[] NormalizeAmplitudesRobust(
            double[] amps, int sectionSize) {
            int[] output = new int[amps.Length];

            for (int i = 0; i < amps.Length; i += sectionSize) {
                int end = Math.Min(i + sectionSize, amps.Length);
                double[] section = amps
                    .Skip(i)
                    .Take(end - i)
                    .Where(v => v > 0)
                    .ToArray();

                if (section.Length == 0)
                    continue;

                double low = Percentile(section, 0.05);
                double high = Percentile(section, 0.95);

                for (int j = i; j < end; j++) {
                    double v = amps[j];
                    output[j] =
                        v <= low ? 0 :
                        v >= high ? AmpMax :
                        (int)((v - low) / (high - low) * AmpMax);
                }
            }
            return output;
        }

        private static int[] NormalizeFrequenciesWeighted(
            double[] freqs, double[] amps) {
            double[] valid = freqs
                .Where((f, i) => amps[i] > 0)
                .ToArray();

            if (valid.Length == 0)
                return new int[freqs.Length];

            double min = valid.Min();
            double max = valid.Max();

            int[] output = new int[freqs.Length];
            for (int i = 0; i < freqs.Length; i++) {
                if (amps[i] <= 0)
                    output[i] = 0;
                else
                    output[i] = (int)Clamp(
                        (freqs[i] - min) / (max - min) * FreqMax,
                        0, FreqMax);
            }
            return output;
        }

        private static double Percentile(double[] data, double p) {
            double[] sorted = data.OrderBy(x => x).ToArray();
            double idx = (sorted.Length - 1) * p;
            int lo = (int)Math.Floor(idx);
            int hi = (int)Math.Ceiling(idx);

            if (lo == hi)
                return sorted[lo];

            return sorted[lo] +
                   (sorted[hi] - sorted[lo]) * (idx - lo);
        }

        private static double Clamp(double v, double min, double max) {
            if (v < min) return min;
            if (v > max) return max;
            return v;
        }

        private static int StemOrderKey(string stemName) {
            string name = stemName.ToLowerInvariant();
            if (name.Contains("drums")) return 0;
            if (name.Contains("bass")) return 1;
            if (name.Contains("vocals")) return 2;
            if (name.Contains("other")) return 3;
            return 99;
        }

        private static void EnsureSongMp3(string origPath, string songDir) {
            try {
                if (string.IsNullOrWhiteSpace(songDir))
                    return;

                string destPath = Path.Combine(songDir, "song.mp3");
                if (File.Exists(destPath))
                    return;

                string ext = Path.GetExtension(origPath);
                if (string.Equals(ext, ".mp3", StringComparison.OrdinalIgnoreCase)) {
                    File.Copy(origPath, destPath, true);
                    return;
                }

                ProcessStartInfo psi = new ProcessStartInfo();
                psi.FileName = ffmpegPath;
                psi.Arguments =
                    "-y -i \"" + origPath + "\" -vn -acodec libmp3lame -q:a 2 \"" + destPath + "\"";
                psi.UseShellExecute = false;
                psi.CreateNoWindow = true;
                psi.RedirectStandardOutput = true;
                psi.RedirectStandardError = true;

                using (Process p = Process.Start(psi)) {
                    p.WaitForExit();
                    if (p.ExitCode != 0) {
                        Debug.WriteLine("ffmpeg failed to create song.mp3 (exit " + p.ExitCode + ")");
                    }
                }
            }
            catch (Exception ex) {
                Debug.WriteLine("EnsureSongMp3 error: " + ex.Message);
            }
        }

        private static string GetToolPath(string exeName) {
            // Try to resolve relative to the running app first, then walk up a few levels.
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string candidate = Path.GetFullPath(Path.Combine(baseDir, "ThirdParty", "exes", exeName));
            if (File.Exists(candidate))
                return candidate;

            candidate = Path.GetFullPath(Path.Combine(baseDir, "ThirdParty", "ThirdParty", exeName));
            if (File.Exists(candidate))
                return candidate;

            DirectoryInfo dir = new DirectoryInfo(baseDir);
            for (int i = 0; i < 6 && dir != null; i++) {
                candidate = Path.GetFullPath(Path.Combine(dir.FullName, "ThirdParty", "exes", exeName));
                if (File.Exists(candidate))
                    return candidate;

                candidate = Path.GetFullPath(Path.Combine(dir.FullName, "ThirdParty", "ThirdParty", exeName));
                if (File.Exists(candidate))
                    return candidate;
                dir = dir.Parent;
            }

            throw new FileNotFoundException(
                "Required tool not found: " + exeName +
                ". Expected under ThirdParty\\exes (or ThirdParty\\ThirdParty) relative to the project or output folder.");
        }
    }
}
