using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace vibrant.Models {
    /// <summary>
    /// information on the directory, created in documents
    /// </summary>
    public static class DirectoryConfig {
        public static string dirName = "Vibrant";
        public static string author = "roland_john";

        public static string documentsPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        public static string fullPath = Path.Combine(documentsPath, author, dirName);
        public static string vibrFilesLocation = Path.Combine(fullPath, "vibrFiles");
        public static string tempFilesLocation = Path.Combine(fullPath, "temp");

        /// <summary>
        /// creates the directory in documents
        /// </summary>
        public static void SetupDirectory() {

            Directory.CreateDirectory(fullPath);
            Directory.CreateDirectory(vibrFilesLocation);
            Directory.CreateDirectory(tempFilesLocation);
        }
    }
}
