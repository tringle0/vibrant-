using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using vibrant.Models;

namespace vibrant.Controls {

    public partial class SongCard : UserControl {
        public const int SONGCARD_IMAGE_SIZE = 64;
        public Song songData;
        public int index;

        public SongCard() {
            InitializeComponent();
        }
        public SongCard(Song songData, int index) {
            InitializeComponent();
            this.songData = songData;   
            this.index = index;
            Display();
        }
        public void Display() {
            songTitle.Text = songData.title;
            songArtist.Text = songData.artist;
            if (index == SongSelector.GetSelectedIndex()) BackColor = ColorPalette.AccentHover;
            else BackColor = ColorPalette.Background;
                //resize image 
                songCover.Image = DownscaleBitmap(songData.bmp_image, SONGCARD_IMAGE_SIZE, SONGCARD_IMAGE_SIZE);
        }

        public static Bitmap DownscaleBitmap(Bitmap originalBitmap, int newWidth, int newHeight) {
            // Create a new bitmap with the desired downscaled dimensions
            Bitmap downscaledBitmap = new Bitmap(newWidth, newHeight);

            using (Graphics graphics = Graphics.FromImage(downscaledBitmap)) {
                // Set the interpolation mode for better quality during downscaling
                graphics.InterpolationMode = InterpolationMode.NearestNeighbor;

                // Draw the original bitmap onto the new bitmap, scaling it to the target size
                graphics.DrawImage(originalBitmap, 0, 0, newWidth, newHeight);
            }

            return downscaledBitmap;
        }

        private void SelectThisCard() {
            SongSelector.SetCurrentSong(index);
            MainForm.instance.DisplaySongList(SongSelector.GetSongList());
            MainForm.instance.DisplaySong(SongSelector.GetSelectedSong());
        }

        private void SongCard_Click(object sender, EventArgs e) {
            SelectThisCard();
        }

        private void songTitle_Click(object sender, EventArgs e) {
            SelectThisCard();
        }

        private void songArtist_Click(object sender, EventArgs e) {
            SelectThisCard();
        }

        private void songCover_Click(object sender, EventArgs e) {
            SelectThisCard();
        }
    }
}
