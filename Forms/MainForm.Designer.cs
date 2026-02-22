namespace vibrant {
    partial class MainForm {
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

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent() {
            this.songList = new System.Windows.Forms.FlowLayoutPanel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.comPortTextBox = new System.Windows.Forms.TextBox();
            this.comPortLabel = new System.Windows.Forms.Label();
            this.currentSongCover = new System.Windows.Forms.PictureBox();
            this.button3 = new System.Windows.Forms.Button();
            this.currentSongArtist = new System.Windows.Forms.Label();
            this.currentSongTitle = new System.Windows.Forms.Label();
            this.panel2 = new System.Windows.Forms.Panel();
            this.button6 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.stop = new System.Windows.Forms.Button();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.currentSongCover)).BeginInit();
            this.panel2.SuspendLayout();
            this.SuspendLayout();
            // 
            // songList
            // 
            this.songList.AutoScroll = true;
            this.songList.BackColor = System.Drawing.Color.White;
            this.songList.FlowDirection = System.Windows.Forms.FlowDirection.TopDown;
            this.songList.Location = new System.Drawing.Point(10, 11);
            this.songList.Margin = new System.Windows.Forms.Padding(2);
            this.songList.Name = "songList";
            this.songList.Size = new System.Drawing.Size(280, 301);
            this.songList.TabIndex = 0;
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.White;
            this.panel1.Controls.Add(this.stop);
            this.panel1.Controls.Add(this.comPortTextBox);
            this.panel1.Controls.Add(this.comPortLabel);
            this.panel1.Controls.Add(this.currentSongCover);
            this.panel1.Controls.Add(this.button3);
            this.panel1.Controls.Add(this.currentSongArtist);
            this.panel1.Controls.Add(this.currentSongTitle);
            this.panel1.Location = new System.Drawing.Point(304, 11);
            this.panel1.Margin = new System.Windows.Forms.Padding(2);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(358, 374);
            this.panel1.TabIndex = 1;
            // 
            // comPortTextBox
            // 
            this.comPortTextBox.Location = new System.Drawing.Point(83, 300);
            this.comPortTextBox.Margin = new System.Windows.Forms.Padding(2);
            this.comPortTextBox.Name = "comPortTextBox";
            this.comPortTextBox.Size = new System.Drawing.Size(192, 20);
            this.comPortTextBox.TabIndex = 4;
            this.comPortTextBox.Text = "COM3";
            // 
            // comPortLabel
            // 
            this.comPortLabel.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)));
            this.comPortLabel.Location = new System.Drawing.Point(83, 282);
            this.comPortLabel.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.comPortLabel.Name = "comPortLabel";
            this.comPortLabel.Size = new System.Drawing.Size(192, 16);
            this.comPortLabel.TabIndex = 3;
            this.comPortLabel.Text = "COM port";
            this.comPortLabel.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // currentSongCover
            // 
            this.currentSongCover.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)));
            this.currentSongCover.BackgroundImageLayout = System.Windows.Forms.ImageLayout.Center;
            this.currentSongCover.Cursor = System.Windows.Forms.Cursors.Arrow;
            this.currentSongCover.Location = new System.Drawing.Point(83, 19);
            this.currentSongCover.Margin = new System.Windows.Forms.Padding(2);
            this.currentSongCover.Name = "currentSongCover";
            this.currentSongCover.Size = new System.Drawing.Size(192, 208);
            this.currentSongCover.TabIndex = 0;
            this.currentSongCover.TabStop = false;
            // 
            // button3
            // 
            this.button3.Font = new System.Drawing.Font("Ticketing", 13.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button3.Location = new System.Drawing.Point(83, 336);
            this.button3.Margin = new System.Windows.Forms.Padding(2);
            this.button3.Name = "button3";
            this.button3.Size = new System.Drawing.Size(89, 36);
            this.button3.TabIndex = 2;
            this.button3.Text = "play";
            this.button3.UseVisualStyleBackColor = true;
            this.button3.Click += new System.EventHandler(this.button3_Click);
            // 
            // currentSongArtist
            // 
            this.currentSongArtist.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)));
            this.currentSongArtist.Font = new System.Drawing.Font("Ticketing", 12F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.currentSongArtist.ForeColor = System.Drawing.Color.Blue;
            this.currentSongArtist.Location = new System.Drawing.Point(83, 262);
            this.currentSongArtist.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.currentSongArtist.Name = "currentSongArtist";
            this.currentSongArtist.Size = new System.Drawing.Size(192, 19);
            this.currentSongArtist.TabIndex = 2;
            this.currentSongArtist.Text = "artist";
            this.currentSongArtist.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            // 
            // currentSongTitle
            // 
            this.currentSongTitle.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom)));
            this.currentSongTitle.Font = new System.Drawing.Font("Ticketing", 24F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.currentSongTitle.ForeColor = System.Drawing.Color.Black;
            this.currentSongTitle.Location = new System.Drawing.Point(83, 236);
            this.currentSongTitle.Margin = new System.Windows.Forms.Padding(2, 0, 2, 0);
            this.currentSongTitle.Name = "currentSongTitle";
            this.currentSongTitle.Size = new System.Drawing.Size(192, 41);
            this.currentSongTitle.TabIndex = 1;
            this.currentSongTitle.Text = "song title";
            this.currentSongTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // panel2
            // 
            this.panel2.Controls.Add(this.button6);
            this.panel2.Controls.Add(this.button2);
            this.panel2.Location = new System.Drawing.Point(10, 318);
            this.panel2.Margin = new System.Windows.Forms.Padding(2);
            this.panel2.Name = "panel2";
            this.panel2.Size = new System.Drawing.Size(280, 67);
            this.panel2.TabIndex = 2;
            // 
            // button6
            // 
            this.button6.Font = new System.Drawing.Font("Ticketing", 13.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button6.Location = new System.Drawing.Point(10, 15);
            this.button6.Margin = new System.Windows.Forms.Padding(2);
            this.button6.Name = "button6";
            this.button6.Size = new System.Drawing.Size(100, 36);
            this.button6.TabIndex = 2;
            this.button6.Text = "import";
            this.button6.UseVisualStyleBackColor = true;
            this.button6.Click += new System.EventHandler(this.ImportButtonClick);
            // 
            // button2
            // 
            this.button2.Font = new System.Drawing.Font("Ticketing", 13.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.button2.Location = new System.Drawing.Point(158, 15);
            this.button2.Margin = new System.Windows.Forms.Padding(2);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(110, 36);
            this.button2.TabIndex = 1;
            this.button2.Text = "remove";
            this.button2.UseVisualStyleBackColor = true;
            this.button2.Click += new System.EventHandler(this.RemoveSongButtonClick);
            // 
            // stop
            // 
            this.stop.Font = new System.Drawing.Font("Ticketing", 13.8F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.stop.Location = new System.Drawing.Point(176, 336);
            this.stop.Margin = new System.Windows.Forms.Padding(2);
            this.stop.Name = "stop";
            this.stop.Size = new System.Drawing.Size(89, 36);
            this.stop.TabIndex = 5;
            this.stop.Text = "stop";
            this.stop.UseVisualStyleBackColor = true;
            this.stop.Click += new System.EventHandler(this.button1_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(672, 397);
            this.Controls.Add(this.panel2);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.songList);
            this.Cursor = System.Windows.Forms.Cursors.IBeam;
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.Margin = new System.Windows.Forms.Padding(2);
            this.Name = "MainForm";
            this.Text = "vibrant! media player";
            this.Load += new System.EventHandler(this.Form1_Load);
            this.panel1.ResumeLayout(false);
            this.panel1.PerformLayout();
            ((System.ComponentModel.ISupportInitialize)(this.currentSongCover)).EndInit();
            this.panel2.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.FlowLayoutPanel songList;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.PictureBox currentSongCover;
        private System.Windows.Forms.Label currentSongTitle;
        private System.Windows.Forms.Label currentSongArtist;
        private System.Windows.Forms.TextBox comPortTextBox;
        private System.Windows.Forms.Label comPortLabel;
        private System.Windows.Forms.Panel panel2;
        private System.Windows.Forms.Button button2;
        private System.Windows.Forms.Button button3;
        private System.Windows.Forms.Button button6;
        private System.Windows.Forms.Button stop;
    }
}

