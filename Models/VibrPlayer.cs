using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Text;
using System.Threading.Tasks;

namespace vibrant.Models {
    public static class VibrPlayer {
        private const int BAUD_RATE = 115200;
        private const int HOP_SIZE = 128;

        // Optional WinForms logger hook
        public static Action<string> Log;

        private static void LogMsg(string msg) {
            Debug.WriteLine(msg);

            if (Log != null)
                Log(msg);
        }

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
            int delayMs = Math.Max(1, (int)(delaySeconds * 1000));

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
    }
}