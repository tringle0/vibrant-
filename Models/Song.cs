using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vibrant.Models {
    /// <summary>
    /// class for a song file, stores metadata and filepath to the vibr file
    /// </summary>
    public class Song {
        public static int THUMBNAIL_SIZE = 256;
        public static Image defaultImage = Properties.Resources.DEFAULT_ALBUM_ART;
        
        public string title { get; private set; }
        public string artist { get; private set; }
        public Bitmap bmp_image;
        public string filePath { get; private set; }

        /// <summary>
        /// constructor, creates the song object from a vibr file
        /// </summary>
        /// <param name="filePath"></param>
        public Song(string filePath) {
              
            string[] lines = File.ReadAllLines(filePath);
            //title and artist
            title = lines[0];
            artist = lines[1];
            if (lines[2].Equals("NO_ALBUM_ART")) {
                bmp_image = new Bitmap(defaultImage);
            }
            else {
                string[] imageData = lines[2].Split(' ');
                ReadAlbumArt(imageData);
            }

            this.filePath = filePath;
        }

        /// <summary>
        /// converts the stored image bytes into a bitmap image
        /// </summary>
        /// <param name="imageData"></param>
        private void ReadAlbumArt(string[] imageData) {
            Color[] imageColors = new Color[THUMBNAIL_SIZE * THUMBNAIL_SIZE * 3];
            for (int k = 0; k < imageData.Length / 3; k++) {
                int i = k * 3;
                imageColors[k] = Color.FromArgb(int.Parse(imageData[i]), int.Parse(imageData[i + 1]), int.Parse(imageData[i + 2]));
            }
            
            Bitmap bmp = new Bitmap(THUMBNAIL_SIZE, THUMBNAIL_SIZE);
            for (int i = 0; i < THUMBNAIL_SIZE * THUMBNAIL_SIZE; i++) {
                bmp.SetPixel(i % THUMBNAIL_SIZE, i / THUMBNAIL_SIZE, imageColors[i]);
            }

            bmp_image = bmp;
        }
    }
}
