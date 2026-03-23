using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using NAudio.Wave;
using vibrant.Controls;
using vibrant.Converter;
using vibrant.Models;


namespace vibrant {
    public partial class MainForm : Form {

        public static MainForm instance;
        private WaveOutEvent audioOut;
        private AudioFileReader audioReader;
        private CancellationTokenSource playCts;
        private bool audioWarmupDone;
        public MainForm() {
            instance = this;
            InitializeComponent();
        }

        /// <summary>
        /// displays the currentSong on the right side of the application
        /// </summary>
        public void DisplaySong(Song song) {
            if (song != null) {
                currentSongArtist.Text = song.artist;
                currentSongTitle.Text = song.title;
                currentSongCover.Image = song.bmp_image;
            }
        }

        /// <summary>
        /// displays the list of songs on the left
        /// </summary>
        public void DisplaySongList(List<Song> list) {
            
            songList.Controls.Clear();
            int index = 0;
            foreach (Song song in list) {
                SongCard card = new SongCard(song, index++);
                songList.Controls.Add(card);
            }
            ColorPalette.ApplyTheme(songList);

        }

        private async void Form1_Load(object sender, EventArgs e) {
            DirectoryConfig.SetupDirectory();
            SongSelector.ImportSongList();
            SongSelector.SetCurrentSong(0);
            DisplaySong(SongSelector.GetSelectedSong());
            DisplaySongList(SongSelector.GetSongList());
            ColorPalette.ApplyTheme(this);
            await VibrPlayer.ScanBleDevicesAsync();
            TryConnectBleOnLaunch();
            WarmupAudioOnLaunch();
        }

        private async void WarmupAudioOnLaunch() {
            if (audioWarmupDone)
                return;
            audioWarmupDone = true;

            try {
                using (var warmupOut = new WaveOutEvent()) {
                    var format = new WaveFormat(44100, 16, 2);
                    var provider = new BufferedWaveProvider(format);
                    int warmupMs = 50;
                    int bytes = format.AverageBytesPerSecond * warmupMs / 1000;
                    if (bytes < format.BlockAlign) bytes = format.BlockAlign;
                    provider.AddSamples(new byte[bytes], 0, bytes);

                    warmupOut.Init(provider);
                    warmupOut.Play();
                    await Task.Delay(warmupMs);
                    warmupOut.Stop();
                }
            }
            catch {
                // ignore warmup failures
            }
        }

        private async void TryConnectBleOnLaunch() {
            string port = comPortTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(port))
                return;

            if (!port.StartsWith("BLE", StringComparison.OrdinalIgnoreCase))
                return;

            string bleName = "VibrantBLE";
            if (port.StartsWith("BLE:", StringComparison.OrdinalIgnoreCase)) {
                bleName = port.Substring(4).Trim();
                if (string.IsNullOrWhiteSpace(bleName))
                    bleName = "VibrantBLE";
            }

            await VibrPlayer.ConnectBleDeviceAsync(bleName);
        }

        /// <summary>
        /// open file selector to select a MP3 file
        /// </summary>
        private void importButtonClick(object sender, EventArgs e) {
            using (OpenFileDialog dialog = new OpenFileDialog()) {
                dialog.Title = "select a vibr file";
                dialog.Filter = "Vibr files (*.vibr)|*.vibr";
                if (dialog.ShowDialog() == DialogResult.OK) {
                    string path = dialog.FileName;
                    SongSelector.AddVibr(path);

                    DisplaySongList(SongSelector.GetSongList());
                    DisplaySong(SongSelector.GetSelectedSong());
                }
            }
        }
        /// <summary>
        /// removes a song from the song list
        /// </summary>
        private void RemoveSongButtonClick(object sender, EventArgs e) {
            SongSelector.RemoveSelectedSong();
            DisplaySongList(SongSelector.GetSongList());
            DisplaySong(SongSelector.GetSelectedSong());
        }

        private void ImportButtonClick(object sender, EventArgs e) {
            ImportAudio();
        }


        private async Task ImportAudio() {
            using (OpenFileDialog dialog = new OpenFileDialog()) {
                dialog.Title = "select an MP3 file";
                dialog.Filter = "Audio files (*.mp3;*.wav;*.flac;*.aac;*.ogg;*.m4a)|*.mp3;*.wav;*.flac;*.aac;*.ogg;*.m4a";
                if (dialog.ShowDialog() == DialogResult.OK) {
                    string path = dialog.FileName;
                    await AudioToVibrConverter.SplitMP3Async(path);
                    SongSelector.ImportSongList();
                    DisplaySongList(SongSelector.GetSongList());
                    MessageBox.Show("finished");
                }
            }
        }
        private async void button3_Click(object sender, EventArgs e) {
            button3.Enabled = false;
            if (buttonStop != null)
                buttonStop.Enabled = true;

            playCts?.Cancel();
            playCts?.Dispose();
            playCts = new CancellationTokenSource();

            try {
                await PlayVibrAsync(playCts.Token);
            }
            catch (Exception ex) {
                MessageBox.Show(
                    ex.Message,
                    "Playback Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally {
                button3.Enabled = true;
                if (buttonStop != null)
                    buttonStop.Enabled = false;
            }
        }

        private async Task PlayVibrAsync(CancellationToken cancellationToken) {
            Song selected = SongSelector.GetSelectedSong();
            if (selected == null) {
                MessageBox.Show("No song selected.", "Playback Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string port = comPortTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(port))
                port = "COM3";

            int vibDelayMs = 0;
            if (vibDelayTextBox != null &&
                int.TryParse(vibDelayTextBox.Text?.Trim(), out int parsedDelay) &&
                parsedDelay >= 0) {
                vibDelayMs = parsedDelay;
            }
            VibrPlayer.VibDelayMs = vibDelayMs;

            bool useBle = port.StartsWith("BLE", StringComparison.OrdinalIgnoreCase);
            string bleName = "VibrantBLE";
            if (useBle && port.StartsWith("BLE:", StringComparison.OrdinalIgnoreCase)) {
                bleName = port.Substring(4).Trim();
                if (string.IsNullOrWhiteSpace(bleName))
                    bleName = "VibrantBLE";
            }

            string mp3Path = FindSongMp3(selected);
            Action<int> startAudioDelay = null;
            if (!string.IsNullOrWhiteSpace(mp3Path) && File.Exists(mp3Path)) {
                startAudioDelay = delayMs => {
                    Task.Run(async () => {
                        if (InvokeRequired) {
                            BeginInvoke(new Action(() => PrepareAudioPlayback(mp3Path)));
                        }
                        else {
                            PrepareAudioPlayback(mp3Path);
                        }

                        if (delayMs > 0) {
                            await Task.Delay(delayMs);
                        }

                        if (InvokeRequired) {
                            BeginInvoke(new Action(PlayPreparedAudio));
                        }
                        else {
                            PlayPreparedAudio();
                        }
                    });
                };
            }

            await Task.Run(() => VibrPlayer.StreamVibrFileAsync(
                path: selected.filePath,
                sampleRate: 3000,
                serialPort: port,
                useBle: useBle,
                bleDeviceName: bleName,
                onAudioStartDelayMs: startAudioDelay,
                cancellationToken: cancellationToken
            ));
        }

        private async void buttonStop_Click(object sender, EventArgs e) {
            if (buttonStop != null)
                buttonStop.Enabled = false;

            try {
                playCts?.Cancel();
                await VibrPlayer.StopNowAsync();
                StopAudioPlayback();
            }
            catch { }
            finally {
                if (buttonStop != null)
                    buttonStop.Enabled = true;
            }
        }

        private void PrepareAudioPlayback(string path) {
            StopAudioPlayback();
            audioReader = new AudioFileReader(path);
            audioOut = new WaveOutEvent();
            audioOut.Init(audioReader);
        }

        private void PlayPreparedAudio() {
            try {
                if (audioOut != null) {
                    audioOut.Play();
                }
            }
            catch { }
        }

        private void StopAudioPlayback() {
            try {
                if (audioOut != null) {
                    audioOut.Stop();
                    audioOut.Dispose();
                    audioOut = null;
                }
            }
            catch { }

            try {
                if (audioReader != null) {
                    audioReader.Dispose();
                    audioReader = null;
                }
            }
            catch { }
        }

        private string FindSongMp3(Song song) {
            if (song == null)
                return null;

            string baseName = Path.GetFileNameWithoutExtension(song.filePath);
            string root = Path.Combine(DirectoryConfig.tempFilesLocation, "htdemucs");
            string direct = Path.Combine(root, baseName, "song.mp3");
            if (File.Exists(direct))
                return direct;

            if (!Directory.Exists(root))
                return null;

            string bestMatch = null;
            DateTime bestTime = DateTime.MinValue;

            foreach (string path in Directory.EnumerateFiles(root, "song.mp3", SearchOption.AllDirectories)) {
                string dirName = Path.GetFileName(Path.GetDirectoryName(path));
                if (string.Equals(dirName, baseName, StringComparison.OrdinalIgnoreCase))
                    return path;

                DateTime t = File.GetLastWriteTime(path);
                if (t > bestTime) {
                    bestTime = t;
                    bestMatch = path;
                }
            }

            return bestMatch;
        }
    }
}



