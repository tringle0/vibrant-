using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace vibrant.Models {
    public static class VibrPlayer {
        private const int BAUD_RATE = 115200;
        private const int HOP_SIZE = 128;

        // Optional WinForms logger hook
        public static Action<string> Log;

        // Cancellation support
        private static CancellationTokenSource _cts;

        public static void Stop() {
            if (_cts != null && !_cts.IsCancellationRequested) {
                _cts.Cancel();
                LogMsg("Stop requested.");
            }
        }

        private static void LogMsg(string msg) {
            Debug.WriteLine(msg);

            if (Log != null)
                Log(msg);
        }

        // === CHARACTER STREAM METHOD (legacy) ===
        /*
        public static async Task StreamVibrFileAsync(
            string path,
            int sampleRate = 3000,
            string serialPort = "COM3") {
            if (!File.Exists(path)) {
                LogMsg("ERROR: .vibr file not found.");
                return;
            }

            string[] lines;
            try {
                lines = File.ReadAllLines(path, Encoding.UTF8);
            }
            catch (Exception ex) {
                LogMsg("ERROR reading file: " + ex.Message);
                return;
            }

            if (lines.Length <= 3) {
                LogMsg("ERROR: .vibr file too short.");
                return;
            }

            string[] frameLines = new string[lines.Length - 3];
            Array.Copy(lines, 3, frameLines, 0, frameLines.Length);

            double delaySeconds = (double)HOP_SIZE / sampleRate;
            double frameMs = delaySeconds * 1000.0;

            LogMsg("Streaming " + frameLines.Length + " frames");
            LogMsg("Frame delay: " + delaySeconds.ToString("F4") + "s");

            SerialPort ser = null;

            try {
                ser = new SerialPort(serialPort, BAUD_RATE);
                ser.Encoding = Encoding.ASCII;
                ser.NewLine = "\n";
                ser.WriteTimeout = 1000;
                ser.ReadTimeout = 1000;

                ser.ErrorReceived += (s, e) => {
                    LogMsg("Serial error: " + e.EventType);
                };

                ser.Open();
                LogMsg("Connected to " + serialPort);
            }
            catch (Exception ex) {
                LogMsg("Serial unavailable — logging only");
                LogMsg(ex.Message);
                ser = null;
                return;
            }

            for (int i = 0; i < frameLines.Length; i++) {
                string msg = frameLines[i] + "\n";

                if (ser != null) {
                    try {
                        if (ser.IsOpen) {
                            ser.Write(msg);
                        }
                        else {
                            LogMsg("Serial port closed.");
                            break;
                        }
                    }
                    catch (IOException ex) {
                        LogMsg("Serial IO error: " + ex.Message);
                        break;
                    }
                    catch (InvalidOperationException ex) {
                        LogMsg("Serial state error: " + ex.Message);
                        break;
                    }
                }

                LogMsg("Sent [" + i + "]: " + frameLines[i]);
                await Task.Delay(delayMs);
            }

            try {
                if (ser != null && ser.IsOpen)
                    ser.Close();
            }
            catch { }

            LogMsg("Done streaming vibr file.");
        }
        */

        // === BINARY STREAM METHOD (fixed-size frames) ===
        public static async Task StreamVibrFileAsync(
            string path,
            int sampleRate = 3000,
            string serialPort = "COM3") {
            if (!File.Exists(path)) {
                LogMsg("ERROR: .vibr file not found.");
                return;
            }

            // Create a fresh CancellationTokenSource for this playback session
            _cts?.Dispose();
            _cts = new CancellationTokenSource();
            CancellationToken ct = _cts.Token;

            double delaySeconds = (double)HOP_SIZE / sampleRate;
            double frameMs = delaySeconds * 1000.0;

            SerialPort ser = null;
            try {
                ser = new SerialPort(serialPort, BAUD_RATE);
                ser.WriteTimeout = 1000;
                ser.ReadTimeout = 1000;
                ser.ErrorReceived += (s, e) => {
                    LogMsg("Serial error: " + e.EventType);
                };
                ser.Open();
                LogMsg("Connected to " + serialPort);
            }
            catch (Exception ex) {
                LogMsg("Serial unavailable — logging only");
                LogMsg(ex.Message);
                ser = null;
                return;
            }

            try {
                using (var reader = new StreamReader(path, Encoding.UTF8)) {
                    // Skip header (title, artist, album art)
                    if (reader.ReadLine() == null ||
                        reader.ReadLine() == null ||
                        reader.ReadLine() == null) {
                        LogMsg("ERROR: .vibr file too short.");
                        return;
                    }

                    string line;
                    int channelCount = -1;
                    byte[] frameBuf = null;
                    int frameIndex = 0;

                    var sw = Stopwatch.StartNew();
                    while ((line = reader.ReadLine()) != null) {
                        if (ct.IsCancellationRequested) {
                            LogMsg("Playback stopped.");
                            break;
                        }

                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        if (channelCount < 0) {
                            channelCount = CountChannels(line);
                            if (channelCount <= 0) {
                                LogMsg("ERROR: no channels detected.");
                                return;
                            }
                            frameBuf = new byte[channelCount * 2];
                            LogMsg("Binary stream channels: " + channelCount);
                            LogMsg("Frame delay: " + delaySeconds.ToString("F4") + "s");
                        }

                        FillFrameBuffer(line, frameBuf, channelCount);

                        if (ser != null && ser.IsOpen) {
                            ser.Write(frameBuf, 0, frameBuf.Length);
                        }

                        frameIndex++;
                        double targetMs = frameIndex * frameMs;
                        double remainingMs = targetMs - sw.Elapsed.TotalMilliseconds;
                        if (remainingMs > 0) {
                            try {
                                await Task.Delay((int)Math.Round(remainingMs), ct).ConfigureAwait(false);
                            }
                            catch (TaskCanceledException) {
                                LogMsg("Playback stopped.");
                                break;
                            }
                        }
                    }

                    LogMsg("Streamed frames: " + frameIndex);
                }
            }
            catch (Exception ex) {
                LogMsg("ERROR streaming .vibr: " + ex.Message);
            }
            finally {
                try {
                    if (ser != null && ser.IsOpen)
                        ser.Close();
                }
                catch { }
            }

            LogMsg("Done streaming vibr file.");
        }

        private static int CountChannels(string line) {
            int count = 1;
            for (int i = 0; i < line.Length; i++) {
                if (line[i] == ';') count++;
            }
            return count;
        }

        private static void FillFrameBuffer(string line, byte[] buffer, int channelCount) {
            Array.Clear(buffer, 0, buffer.Length);

            int ch = 0;
            int start = 0;
            for (int i = 0; i <= line.Length && ch < channelCount; i++) {
                bool end = (i == line.Length) || (line[i] == ';');
                if (!end) continue;

                int len = i - start;
                if (len > 0) {
                    ParseSegment(line, start, len, out int amp, out int freq);
                    if (amp < 0) amp = 0;
                    if (amp > 127) amp = 127;
                    if (freq < 0) freq = 0;
                    if (freq > 100) freq = 100;
                    int baseIdx = ch * 2;
                    buffer[baseIdx] = (byte)amp;
                    buffer[baseIdx + 1] = (byte)freq;
                }

                ch++;
                start = i + 1;
            }
        }

        private static void ParseSegment(string line, int start, int len, out int amp, out int freq) {
            amp = 0;
            freq = 0;

            int comma = -1;
            int end = start + len;
            for (int i = start; i < end; i++) {
                if (line[i] == ',') {
                    comma = i;
                    break;
                }
            }

            if (comma <= start || comma >= end - 1)
                return;

            int a = 0;
            for (int i = start; i < comma; i++) {
                char c = line[i];
                if (c < '0' || c > '9') return;
                a = a * 10 + (c - '0');
            }

            int f = 0;
            for (int i = comma + 1; i < end; i++) {
                char c = line[i];
                if (c < '0' || c > '9') return;
                f = f * 10 + (c - '0');
            }

            amp = a;
            freq = f;
        }
    }
}