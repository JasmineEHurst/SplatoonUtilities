﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Windows.Forms;
using System.Xml.Serialization;

namespace MusicRandomizer
{
    public partial class MainForm : Form
    {
        public static readonly Random random = new Random();
        public List<MusicFile> musicFiles;

        private XmlSerializer serializer = new XmlSerializer(typeof(List<MusicFile>), new XmlRootAttribute("Tracks"));
        private CafiineServer cafiineServer;
        private List<FileTracker> fileTrackers = new List<FileTracker>();
        private PlayMode playMode = PlayMode.Shuffle;
        private MusicFile playNext = null;

        public MainForm()
        {
            InitializeComponent();
        }

        private void Main_Load(object sender, EventArgs e)
        {
            if (!Directory.Exists("tracks"))
            {
                Directory.CreateDirectory("tracks");
            }

            if (!Directory.Exists("other_files"))
            {
                Directory.CreateDirectory(Path.Combine("other_files", "vol", "content"));
            }

            if (!File.Exists("Tracks.xml"))
            {
                musicFiles = new List<MusicFile>();

                lstOutput.Items.Add("Please import your first track.");
            }
            else
            {
                using (FileStream stream = File.OpenRead("Tracks.xml"))
                {
                    musicFiles = (List<MusicFile>)serializer.Deserialize(stream);
                }
            }

            RefreshTrackList();

            cafiineWorker.RunWorkerAsync();
            updateWorker.RunWorkerAsync(false);
        }

        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
        }

        public void RefreshTrackList()
        {
            lsvTracks.Items.Clear();

            int numberOfTrackers = Enum.GetValues(typeof(TrackType)).Length - 1; // subtract 1 so Unknown is not included

            fileTrackers.Clear();

            for (int i = 0; i < numberOfTrackers; i++)
            {
                fileTrackers.Add(new FileTracker());
            }

            foreach (MusicFile file in musicFiles)
            {
                String types = "";
                foreach (TrackType type in file.types)
                {
                    fileTrackers[(int)type].files.Add(file);

                    types += type.ToUIString() + ", ";
                }

                types = types.Substring(0, types.Length - 2);
                String name = Path.GetFileName(file.path);
                lsvTracks.Items.Add(new ListViewItem(new String[] { name.Substring(0, name.Length - 6), types }));
            }

            SaveTrackList();
        }

        public void SaveTrackList()
        {
            File.Delete("Tracks.xml");
            using (FileStream writer = File.OpenWrite("Tracks.xml"))
            {
                serializer.Serialize(writer, musicFiles);
            }
        }

        public MusicFile GetFile(String strippedPath)
        {
            TrackType trackType = TrackTypeUtils.FileNameToTrackType(Path.GetFileName(strippedPath));
            if (trackType == TrackType.Unknown)
            {
                return null;
            }

            MusicFile musicFile;
            if (playNext != null)
            {
                musicFile = playNext;
                playNext = null;
            }
            else
            {
                musicFile = fileTrackers[(int)trackType].getTrack(this.playMode);
                if (musicFile == null)
                {
                    Log(LogType.NowPlaying, strippedPath);
                    return null;
                }
            }

            return musicFile;
        }

        public void Log(LogType type, String str)
        {
            cafiineWorker.ReportProgress((int) type, str);
        }

        private void cafiineWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            try
            {
                cafiineServer = new CafiineServer(this);
                cafiineServer.Run();
            }
            catch (Exception ex)
            {
                e.Result = ex.Message;
            }
        }

        private void cafiineWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            String message = e.UserState.ToString();
            LogType type = (LogType)e.ProgressPercentage;

            switch (type)
            {
                case LogType.NowPlaying:
                    lblNowPlaying.Text = "Now Playing: " + message;
                    break;
                default:
                    lstOutput.Items.Add(message);
                    lstOutput.TopIndex = lstOutput.Items.Count - 1;
                    break;
            }
        }

        private void updateWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            String version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();
            UpdateInfo updateInfo = UpdateChecker.CheckForUpdate();

            if (!version.Equals(updateInfo.latestVersion))
            {
                String dialogString = "Version " + updateInfo.latestVersion + " is now available for MusicRandomizer with the following changes:\n\n" + updateInfo.changes;
                dialogString += "\n\nWould you like to download the latest update from GitHub now?";

                DialogResult result = MessageBox.Show(dialogString, "Update available", MessageBoxButtons.YesNo, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);

                if (result == DialogResult.Yes)
                {
                    this.usageToolStripMenuItem_Click(null, null);
                }
            }
            else
            {
                if ((Boolean)e.Argument)
                {
                    MessageBox.Show("There is no update available right now.", "No update available", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
        }

        private void lsvTracks_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Right)
            {
                if (lsvTracks.FocusedItem.Bounds.Contains(e.Location) == true)
                {
                    contextMenu.Show(Cursor.Position);
                }
            }
        }

        private void editToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MusicFile file = musicFiles[lsvTracks.SelectedIndices[0]];

            ImportForm importForm = new ImportForm(file);
            importForm.ShowDialog(this);
        }

        private void removeToolStripMenuItem_Click(object sender, EventArgs e)
        {
            MusicFile file = musicFiles[lsvTracks.SelectedIndices[0]];

            foreach (FileTracker tracker in fileTrackers)
            {
                tracker.files.Remove(file);
            }

            musicFiles.Remove(file);
            lsvTracks.Items.RemoveAt(lsvTracks.SelectedIndices[0]);

            SaveTrackList();
        }

        private void btnPlayNext_Click(object sender, EventArgs e)
        {
            if (lsvTracks.SelectedIndices == null)
            {
                return;
            }

            playNext = musicFiles[lsvTracks.SelectedIndices[0]];
        }

        private void btnImport_Click(object sender, EventArgs e)
        {
            ImportForm importForm = new ImportForm();
            importForm.ShowDialog(this);
        }

        private void radShuffle_CheckedChanged(object sender, EventArgs e)
        {
            if (radShuffle.Checked)
            {
                this.playMode = PlayMode.Shuffle;
            }
        }

        private void radInOrder_CheckedChanged(object sender, EventArgs e)
        {
            if (radInOrder.Checked)
            {
                this.playMode = PlayMode.InOrder;
            }
        }

        private void radTrueRandom_CheckedChanged(object sender, EventArgs e)
        {
            if (radTrueRandom.Checked)
            {
                this.playMode = PlayMode.TrueRandom;
            }
        }

        private void checkForUpdateToolStripMenuItem_Click(object sender, EventArgs e)
        {
            updateWorker.RunWorkerAsync(true);
        }

        private void exitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void usageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://github.com/OatmealDome/SplatoonUtilities/blob/master/MusicRandomizer/README.md");
        }

        private void aboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            String version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString();

            // show a poor man's version of an about dialog
            MessageBox.Show("MusicRandomizer (" + version + ")\nCopyright (c) 2016 OatmealDome");
        }
    }
}
