using System;
using System.Diagnostics;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Management;
using System.ServiceProcess;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Principal;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Foundation;
using Windows.Storage.Streams;

namespace vibrant.Models {
    public static class VibrPlayer {
        private const int BAUD_RATE = 115200;
        private const int HOP_SIZE = 128;
        private const int PRE_ROLL_MS = 500;
        private const int AUDIO_LEAD_MS = 0; // audio output latency compensation (tune as needed)
        private static int _vibDelayMs = 0; // add delay to vibration only (positive delays vibration)
        private static readonly object ActiveLock = new object();
        private static SerialPort ActiveSerial;
        private static BleConnection ActiveBle;
        private static int ActiveFrameSize;
        private static readonly SemaphoreSlim BleConnectLock = new SemaphoreSlim(1, 1);

        public static int VibDelayMs {
            get { return Volatile.Read(ref _vibDelayMs); }
            set {
                if (value < 0) value = 0;
                Interlocked.Exchange(ref _vibDelayMs, value);
            }
        }

        public static async Task StopNowAsync() {
            SerialPort ser;
            BleConnection ble;
            int frameSize;
            lock (ActiveLock) {
                ser = ActiveSerial;
                ble = ActiveBle;
                frameSize = ActiveFrameSize;
            }

            if (ble != null) {
                try {
                    await SendBlePacketAsync(ble.Characteristic, new byte[] { PKT_STOP });
                }
                catch { }
            }

            if (ser != null && ser.IsOpen && frameSize > 0) {
                try {
                    byte[] zeros = new byte[frameSize];
                    ser.Write(zeros, 0, zeros.Length);
                }
                catch { }
            }
        }

        private static void UpdateActive(SerialPort ser, BleConnection ble, int frameSize) {
            lock (ActiveLock) {
                ActiveSerial = ser;
                ActiveBle = ble;
                ActiveFrameSize = frameSize;
            }
        }

        private static BleConnection GetActiveBleConnected() {
            lock (ActiveLock) {
                if (ActiveBle?.Device != null &&
                    ActiveBle.Device.ConnectionStatus == BluetoothConnectionStatus.Connected) {
                    return ActiveBle;
                }
                return null;
            }
        }

        public static async Task<bool> ConnectBleDeviceAsync(string deviceName) {
            if (string.IsNullOrWhiteSpace(deviceName))
                deviceName = DefaultBleDeviceName;

            try {
                await GetOrConnectBleAsync(deviceName);
                return true;
            }
            catch (Exception ex) {
                LogMsg("BLE connect failed: " + ex.Message);
                return false;
            }
        }

        private static async Task<BleConnection> GetOrConnectBleAsync(string deviceName) {
            var existing = GetActiveBleConnected();
            if (existing != null)
                return existing;

            await BleConnectLock.WaitAsync();
            try {
                existing = GetActiveBleConnected();
                if (existing != null)
                    return existing;

                BleConnection ble = await ConnectBleWithRetryAsync(deviceName, BleServiceUuid, BleRxUuid, 8, 2);
                UpdateActive(null, ble, 0);
                LogMsg("BLE connected to " + deviceName);
                return ble;
            }
            finally {
                BleConnectLock.Release();
            }
        }

        private static async Task<BleConnection> ConnectBleWithRetryAsync(
            string deviceName,
            Guid serviceUuid,
            Guid characteristicUuid,
            int scanSeconds,
            int attempts) {
            Exception last = null;
            for (int i = 1; i <= attempts; i++) {
                try {
                    return await ConnectBleAsync(deviceName, serviceUuid, characteristicUuid, scanSeconds);
                }
                catch (Exception ex) {
                    last = ex;
                    LogMsg("BLE connect attempt " + i + " failed: " + ex.Message);
                    await Task.Delay(500);
                }
            }
            throw last ?? new InvalidOperationException("BLE connect failed.");
        }

        private const string DefaultBleDeviceName = "VibrantBLE";
        private static readonly Guid BleServiceUuid = new Guid("6E400001-B5A3-F393-E0A9-E50E24DCCA9E");
        private static readonly Guid BleRxUuid = new Guid("6E400002-B5A3-F393-E0A9-E50E24DCCA9E");
        private static readonly Guid BleTxUuid = new Guid("6E400003-B5A3-F393-E0A9-E50E24DCCA9E");

        private const byte PKT_PING = 0x01;
        private const byte PKT_START = 0x02;
        private const byte PKT_FRAME = 0x03;
        private const byte PKT_STOP = 0x04;
        private const byte PKT_PONG = 0x81;

        // Optional WinForms logger hook
        public static Action<string> Log;

        private static void LogMsg(string msg) {
            Debug.WriteLine(msg);

            if (Log != null)
                Log(msg);
        }

        private static void LogBleDiagnostics() {
            try {
                bool is64 = Environment.Is64BitProcess;
                bool os64 = Environment.Is64BitOperatingSystem;
                string os = Environment.OSVersion.VersionString;
                string fx = AppDomain.CurrentDomain.SetupInformation.TargetFrameworkName;
                string clr = Environment.Version.ToString();
                bool isElevated = new WindowsPrincipal(WindowsIdentity.GetCurrent())
                    .IsInRole(WindowsBuiltInRole.Administrator);
                int sessionId = Process.GetCurrentProcess().SessionId;
                LogMsg("BLE diag: OS=" + os + " x64proc=" + is64 + " x64os=" + os64 + " elevated=" + isElevated);
                LogMsg("BLE diag: CLR=" + clr + " TF=" + fx + " session=" + sessionId + " interactive=" + Environment.UserInteractive);
            }
            catch (Exception ex) {
                LogMsg("BLE diag failed: " + ex.Message);
            }

            try {
                ServiceController sc = new ServiceController("bthserv");
                LogMsg("BLE diag: bthserv=" + sc.Status);
            }
            catch (Exception ex) {
                LogMsg("BLE diag: bthserv check failed: " + ex.Message);
            }

            try {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Name, Status, PNPDeviceID FROM Win32_PnPEntity WHERE PNPClass='Bluetooth'")) {
                    foreach (ManagementObject obj in searcher.Get()) {
                        string name = obj["Name"]?.ToString() ?? "(unknown)";
                        string status = obj["Status"]?.ToString() ?? "(unknown)";
                        string pnp = obj["PNPDeviceID"]?.ToString() ?? "(unknown)";
                        LogMsg("BLE diag: PnP " + name + " status=" + status + " id=" + pnp);
                    }
                }
            }
            catch (Exception ex) {
                LogMsg("BLE diag: PnP check failed: " + ex.Message);
            }
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
            string serialPort = "COM3",
            bool useBle = false,
            string bleDeviceName = null,
            Action<int> onAudioStartDelayMs = null,
            CancellationToken cancellationToken = default) {
            if (!File.Exists(path)) {
                LogMsg("ERROR: .vibr file not found.");
                return;
            }

            if (string.IsNullOrWhiteSpace(bleDeviceName))
                bleDeviceName = DefaultBleDeviceName;

            double delaySeconds = (double)HOP_SIZE / sampleRate;
            double frameMs = delaySeconds * 1000.0;

            SerialPort ser = null;
            BleConnection ble = null;
            bool audioScheduled = false;
            bool canceled = false;
            bool keepBleConnection = true;

            if (useBle) {
                try {
                    ble = await GetOrConnectBleAsync(bleDeviceName);
                }
                catch (Exception ex) {
                    LogMsg("BLE unavailable — logging only");
                    LogMsg(ex.Message);
                    return;
                }

                try {
                    int audioDelayMs = await PerformBleSyncAsync(ble, PRE_ROLL_MS);
                    if (onAudioStartDelayMs != null) {
                        onAudioStartDelayMs(audioDelayMs);
                        audioScheduled = true;
                    }
                }
                catch (Exception ex) {
                    LogMsg("BLE sync failed: " + ex.Message);
                }
            }
            else {
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

                UpdateActive(ser, null, 0);
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
                    int timedIndex = 0;
                    Stopwatch sw = null;
                    int preRollFrames = useBle ? (int)Math.Ceiling(PRE_ROLL_MS / frameMs) : 0;

                    while ((line = reader.ReadLine()) != null) {
                        if (string.IsNullOrWhiteSpace(line))
                            continue;

                        if (channelCount < 0) {
                            channelCount = CountChannels(line);
                            if (channelCount <= 0) {
                                LogMsg("ERROR: no channels detected.");
                                return;
                            }
                            frameBuf = new byte[channelCount * 2];
                            UpdateActive(ser, ble, frameBuf.Length);
                            LogMsg("Binary stream channels: " + channelCount);
                            LogMsg("Frame delay: " + delaySeconds.ToString("F4") + "s");
                        }

                        if (cancellationToken.IsCancellationRequested) {
                            canceled = true;
                            break;
                        }

                        FillFrameBuffer(line, frameBuf, channelCount);

                        if (!useBle && !audioScheduled && onAudioStartDelayMs != null) {
                            onAudioStartDelayMs(0);
                            audioScheduled = true;
                        }

                        if (useBle) {
                            byte[] packet = new byte[1 + frameBuf.Length];
                            packet[0] = PKT_FRAME;
                            System.Buffer.BlockCopy(frameBuf, 0, packet, 1, frameBuf.Length);
                            await SendBlePacketAsync(ble.Characteristic, packet);
                        }
                        else if (ser != null && ser.IsOpen) {
                            ser.Write(frameBuf, 0, frameBuf.Length);
                        }

                        frameIndex++;
                        if (useBle) {
                            if (sw == null && frameIndex >= preRollFrames) {
                                sw = Stopwatch.StartNew();
                                timedIndex = 0;
                            }
                        }
                        else if (sw == null) {
                            sw = Stopwatch.StartNew();
                            timedIndex = 0;
                        }

                        if (sw != null) {
                            timedIndex++;
                            double targetMs = timedIndex * frameMs;
                            double remainingMs = targetMs - sw.Elapsed.TotalMilliseconds;
                            if (remainingMs > 0) {
                                await Task.Delay((int)Math.Round(remainingMs)).ConfigureAwait(false);
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
                if (canceled || cancellationToken.IsCancellationRequested) {
                    try {
                        await StopNowAsync();
                    }
                    catch { }
                }

                try {
                    if (ser != null && ser.IsOpen)
                        ser.Close();
                }
                catch { }

                try {
                    if (ble?.Device != null) {
                        if (!keepBleConnection || GetActiveBleConnected() != ble) {
                            ble.Device.Dispose();
                        }
                    }
                }
                catch { }

                if (!keepBleConnection)
                    UpdateActive(null, null, 0);
            }

            LogMsg("Done streaming vibr file.");
        }

        private sealed class BleConnection {
            public BluetoothLEDevice Device;
            public GattCharacteristic Characteristic;
            public GattCharacteristic TxCharacteristic;
        }

        private sealed class PongResult {
            public uint DeviceMs;
            public long RecvTicks;
        }

        private static async Task<BleConnection> ConnectBleAsync(
            string deviceName,
            Guid serviceUuid,
            Guid characteristicUuid,
            int scanSeconds = 8) {
            LogBleDiagnostics();
            var device = await FindBleDeviceByNameAsync(deviceName, scanSeconds);
            if (device == null) {
                throw new InvalidOperationException("BLE device not found: " + deviceName);
            }

            var servicesResult = await device.GetGattServicesAsync(BluetoothCacheMode.Uncached);
            if (servicesResult.Status != GattCommunicationStatus.Success) {
                throw new InvalidOperationException("BLE service query failed: " + servicesResult.Status);
            }

            var service = servicesResult.Services.FirstOrDefault(s => s.Uuid == serviceUuid);
            if (service == null) {
                throw new InvalidOperationException("BLE service not found: " + serviceUuid);
            }

            var charsResult = await service.GetCharacteristicsForUuidAsync(characteristicUuid, BluetoothCacheMode.Uncached);
            if (charsResult.Status != GattCommunicationStatus.Success) {
                throw new InvalidOperationException("BLE characteristic query failed: " + charsResult.Status);
            }

            var characteristic = charsResult.Characteristics.FirstOrDefault();
            if (characteristic == null) {
                var allChars = await service.GetCharacteristicsAsync(BluetoothCacheMode.Uncached);
                characteristic = allChars.Characteristics.FirstOrDefault(c => c.Uuid == characteristicUuid);
            }

            if (characteristic == null) {
                throw new InvalidOperationException("BLE characteristic not found: " + characteristicUuid);
            }

            var txChars = await service.GetCharacteristicsForUuidAsync(BleTxUuid, BluetoothCacheMode.Uncached);
            if (txChars.Status != GattCommunicationStatus.Success) {
                throw new InvalidOperationException("BLE TX characteristic query failed: " + txChars.Status);
            }
            var txCharacteristic = txChars.Characteristics.FirstOrDefault();
            if (txCharacteristic == null) {
                throw new InvalidOperationException("BLE TX characteristic not found: " + BleTxUuid);
            }

            var cccdStatus = await txCharacteristic.WriteClientCharacteristicConfigurationDescriptorAsync(
                GattClientCharacteristicConfigurationDescriptorValue.Notify);
            if (cccdStatus != GattCommunicationStatus.Success) {
                throw new InvalidOperationException("BLE notify enable failed: " + cccdStatus);
            }

            return new BleConnection {
                Device = device,
                Characteristic = characteristic,
                TxCharacteristic = txCharacteristic
            };
        }

        private static async Task<int> PerformBleSyncAsync(BleConnection ble, int preRollMs) {
            if (ble == null || ble.TxCharacteristic == null)
                return 0;

            var tcs = new TaskCompletionSource<PongResult>();
            TypedEventHandler<GattCharacteristic, GattValueChangedEventArgs> handler = (s, e) => {
                try {
                    if (e?.CharacteristicValue == null || e.CharacteristicValue.Length < 5)
                        return;
                    var data = new byte[e.CharacteristicValue.Length];
                    DataReader.FromBuffer(e.CharacteristicValue).ReadBytes(data);
                    if (data[0] != PKT_PONG)
                        return;
                    uint deviceMs = BitConverter.ToUInt32(data, 1);
                    tcs.TrySetResult(new PongResult {
                        DeviceMs = deviceMs,
                        RecvTicks = Stopwatch.GetTimestamp()
                    });
                }
                catch { }
            };

            ble.TxCharacteristic.ValueChanged += handler;
            try {
                long tSend = Stopwatch.GetTimestamp();
                await SendBlePacketAsync(ble.Characteristic, new byte[] { PKT_PING });

                Task completed = await Task.WhenAny(tcs.Task, Task.Delay(2000));
                if (completed != tcs.Task)
                    throw new System.TimeoutException("BLE ping timeout");

                PongResult pong = tcs.Task.Result;

                double ticksPerMs = (double)Stopwatch.Frequency / 1000.0;
                double rttMs = (pong.RecvTicks - tSend) / ticksPerMs;
                double pcNowMs = pong.RecvTicks / ticksPerMs;
                double deviceNowMs = pong.DeviceMs + (rttMs / 2.0);
                double offsetMs = deviceNowMs - pcNowMs;

                double startDeviceMsBase = deviceNowMs + preRollMs;
                double startPcMs = startDeviceMsBase - offsetMs;
                int delayMs = (int)Math.Round(startPcMs - pcNowMs);
                if (delayMs < 0) delayMs = 0;
                if (AUDIO_LEAD_MS > 0) {
                    delayMs = Math.Max(0, delayMs - AUDIO_LEAD_MS);
                }

                double startDeviceMs = startDeviceMsBase + VibDelayMs;
                uint startDeviceMsU = (uint)Math.Max(0, Math.Round(startDeviceMs));
                await SendStartAsync(ble, startDeviceMsU);

                return delayMs;
            }
            finally {
                ble.TxCharacteristic.ValueChanged -= handler;
            }
        }

        private static async Task SendStartAsync(BleConnection ble, uint startDeviceMs) {
            byte[] packet = new byte[1 + 4];
            packet[0] = PKT_START;
            byte[] timeBytes = BitConverter.GetBytes(startDeviceMs);
            System.Buffer.BlockCopy(timeBytes, 0, packet, 1, 4);
            await SendBlePacketAsync(ble.Characteristic, packet);
        }

        private static async Task SendBlePacketAsync(GattCharacteristic characteristic, byte[] data) {
            IBuffer buffer = data.AsBuffer();
            var status = await characteristic.WriteValueAsync(buffer, GattWriteOption.WriteWithoutResponse);
            if (status != GattCommunicationStatus.Success) {
                LogMsg("BLE write failed: " + status);
            }
        }

        public static async Task ScanBleDevicesAsync(
            int scanSeconds = 5,
            string nameFilter = null) {
            try {
                LogBleDiagnostics();
                var results = await ScanBleWinRtAsync(scanSeconds, nameFilter);
                if (results.Count == 0) {
                    LogMsg("BLE scan results: none");
                }
                else {
                    LogMsg("BLE scan results:");
                    foreach (var r in results) {
                        LogMsg(" - " + r.Name + " [" + r.Id + "]");
                    }
                }
            }
            catch (Exception ex) {
                LogMsg("BLE scan failed: " + ex.ToString());
            }
        }

        private sealed class BleScanResult {
            public string Name;
            public string Id;
            public ulong Address;
        }

        private static async Task<BluetoothLEDevice> FindBleDeviceByNameAsync(string deviceName, int scanSeconds) {
            var results = await ScanBleWinRtAsync(scanSeconds, deviceName);
            var match = results.FirstOrDefault(r =>
                string.Equals(r.Name, deviceName, StringComparison.OrdinalIgnoreCase));
            if (match == null)
                return null;
            return await BluetoothLEDevice.FromBluetoothAddressAsync(match.Address);
        }

        private static async Task<System.Collections.Generic.List<BleScanResult>> ScanBleWinRtAsync(
            int scanSeconds,
            string nameFilter) {
            var results = new System.Collections.Generic.Dictionary<ulong, BleScanResult>();
            var watcher = new BluetoothLEAdvertisementWatcher {
                ScanningMode = BluetoothLEScanningMode.Active
            };

            watcher.Received += (s, e) => {
                string name = e.Advertisement?.LocalName;
                if (string.IsNullOrWhiteSpace(name))
                    return;
                if (!string.IsNullOrWhiteSpace(nameFilter) &&
                    !string.Equals(name, nameFilter, StringComparison.OrdinalIgnoreCase))
                    return;
                lock (results) {
                    if (!results.ContainsKey(e.BluetoothAddress)) {
                        results[e.BluetoothAddress] = new BleScanResult {
                            Name = name,
                            Address = e.BluetoothAddress,
                            Id = "0x" + e.BluetoothAddress.ToString("X")
                        };
                    }
                }
            };

            watcher.Start();
            await Task.Delay(TimeSpan.FromSeconds(scanSeconds));
            watcher.Stop();

            lock (results) {
                return results.Values.ToList();
            }
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
