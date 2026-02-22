using System;
using System.Collections.Generic;
using System.IO;
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

        private void Form1_Load(object sender, EventArgs e) {
            DirectoryConfig.SetupDirectory();
            SongSelector.ImportSongList();
            SongSelector.SetCurrentSong(0);
            DisplaySong(SongSelector.GetSelectedSong());
            DisplaySongList(SongSelector.GetSongList());
            ColorPalette.ApplyTheme(this);
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

            try {
                await PlayVibrAsync();
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
            }
        }

        private async Task PlayVibrAsync() {
            Song selected = SongSelector.GetSelectedSong();
            if (selected == null) {
                MessageBox.Show("No song selected.", "Playback Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string port = comPortTextBox.Text?.Trim();
            if (string.IsNullOrWhiteSpace(port))
                port = "COM3";

            string mp3Path = FindSongMp3(selected);
            if (!string.IsNullOrWhiteSpace(mp3Path) && File.Exists(mp3Path)) {
                StartAudioPlayback(mp3Path);
            }

            await Task.Run(() => VibrPlayer.StreamVibrFileAsync(
                path: selected.filePath,
                sampleRate: 3000,
                serialPort: port
            ));
        }

        private void StartAudioPlayback(string path) {
            StopAudioPlayback();
            audioReader = new AudioFileReader(path);
            audioOut = new WaveOutEvent();
            audioOut.Init(audioReader);
            audioOut.Play();
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
