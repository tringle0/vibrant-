using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Forms;
using vibrant.Controls;
using vibrant.Converter;
using vibrant.Models;


namespace vibrant {
    public partial class MainForm : Form {

        public static MainForm instance;
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
            await VibrPlayer.StreamVibrFileAsync(
                path: @"C:\Users\User\Documents\roland_john\Vibrant\vibrFiles\GHOUL_Camellia.vibr",
                sampleRate: 3000,
                serialPort: "COM6"
            );
        }
    }
}
