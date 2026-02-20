using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vibrant.Models {
    /// <summary>
    /// Manages vibr files in the file system and also keeps track of the currently selected song
    /// </summary>
    public static class SongSelector {
        private static int selectedSongIndex = 0; 
        private static List<Song> songFileList = new List<Song>();

        /// <summary>
        /// Sets the current song to the specified <see cref="Song"/> instance.
        /// </summary>
        /// <param name="song">The <see cref="Song"/> to set as the current song. Can be <see langword="null"/> to clear the current
        /// selection.</param>
        public static void SetCurrentSong(int index) {
            selectedSongIndex = index;
        }
        /// <summary>
        /// returns the currently selected song
        /// </summary>
        /// <returns><see cref="Song"> object of the currently selected song</returns>
        public static Song GetSelectedSong() {
            if(songFileList.Count == 0) return null;
            return songFileList[selectedSongIndex];
        }

        public static int GetSelectedIndex() { return selectedSongIndex; }

        public static List<Song> GetSongList() { return songFileList; }

        /// <summary>
        /// copies a .vibr file into the directory and also adds it to the song list
        /// </summary>
        /// <param name="fileLoc"></param>
        public static void AddVibr(string fileLoc) {
            string fileName = Path.GetFileName(fileLoc);
            string newPath = Path.Combine(DirectoryConfig.vibrFilesLocation, fileName);
            if (!File.Exists(newPath))
                File.Copy(fileLoc, newPath);
            songFileList.Add(new Song(newPath));
        }

        /// <summary>
        /// constructs the songFileList from the vibr files in the directory
        /// </summary>
        public static void ImportSongList() {
            songFileList = new List<Song>();
            foreach(string file in Directory.EnumerateFiles(DirectoryConfig.vibrFilesLocation, "*.vibr")) {
                songFileList.Add(new Song(file));
            }
        }

        /// <summary>
        /// removes the currently selected song from the directory
        /// </summary>
        public static void RemoveSelectedSong() {
            if (GetSelectedSong() != null) {
                File.Delete(GetSelectedSong().filePath);
                songFileList.RemoveAt(selectedSongIndex);
                if (selectedSongIndex > 0) selectedSongIndex--;
            }
        }
    }
}
