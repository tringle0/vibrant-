namespace vibrant.Controls {
    partial class SongCard {
        /// <summary> 
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary> 
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing) {
            if (disposing && (components != null)) {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Component Designer generated code

        /// <summary> 
        /// Required method for Designer support - do not modify 
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.songCover = new System.Windows.Forms.PictureBox();
            this.songTitle = new System.Windows.Forms.Label();
            this.songArtist = new System.Windows.Forms.Label();
            this.panel1 = new System.Windows.Forms.Panel();
            ((System.ComponentModel.ISupportInitialize)(this.songCover)).BeginInit();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // songCover
            // 
            this.songCover.BackColor = System.Drawing.SystemColors.AppWorkspace;
            this.songCover.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.songCover.Location = new System.Drawing.Point(3, 3);
            this.songCover.Name = "songCover";
            this.songCover.Size = new System.Drawing.Size(64, 64);
            this.songCover.TabIndex = 0;
            this.songCover.TabStop = false;
            this.songCover.Click += new System.EventHandler(this.songCover_Click);
            // 
            // songTitle
            // 
            this.songTitle.Font = new System.Drawing.Font("Ticketing", 19.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.songTitle.ForeColor = System.Drawing.Color.Black;
            this.songTitle.Location = new System.Drawing.Point(73, 7);
            this.songTitle.Name = "songTitle";
            this.songTitle.Size = new System.Drawing.Size(272, 36);
            this.songTitle.TabIndex = 1;
            this.songTitle.Text = "song title";
            this.songTitle.Click += new System.EventHandler(this.songTitle_Click);
            // 
            // songArtist
            // 
            this.songArtist.AutoSize = true;
            this.songArtist.Font = new System.Drawing.Font("Ticketing", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.songArtist.ForeColor = System.Drawing.Color.Blue;
            this.songArtist.Location = new System.Drawing.Point(75, 43);
            this.songArtist.Name = "songArtist";
            this.songArtist.Size = new System.Drawing.Size(108, 20);
            this.songArtist.TabIndex = 2;
            this.songArtist.Text = "song artist";
            this.songArtist.Click += new System.EventHandler(this.songArtist_Click);
            // 
            // panel1
            // 
            this.panel1.Controls.Add(this.songCover);
            this.panel1.Controls.Add(this.songArtist);
            this.panel1.Controls.Add(this.songTitle);
            this.panel1.Location = new System.Drawing.Point(3, 3);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(350, 70);
            this.panel1.TabIndex = 3;
            // 
            // SongCard
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(8F, 16F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.Controls.Add(this.panel1);
            this.Name = "SongCard";
            this.Size = new System.Drawing.Size(356, 76);
            this.Tag = "";
            this.Click += new System.EventHandler(this.SongCard_Click);
            ((System.ComponentModel.ISupportInitialize)(this.songCover)).EndInit();
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.PictureBox songCover;
        private System.Windows.Forms.Label songTitle;
        private System.Windows.Forms.Label songArtist;
        private System.Windows.Forms.Panel panel1;
    }
}
