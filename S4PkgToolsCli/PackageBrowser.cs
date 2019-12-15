using CASPartResource;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Windows.Forms;

namespace S4PkgToolsCli
{
    public partial class PackageBrowser : Form
    {
        public static Dictionary<BodyType, HashSet<string>> Entries = null;
        public static int PageSize = 100;
        public static int LastPage = 0;

        public PackageBrowser()
        {
            InitializeComponent();

            var binaryFormatter = new BinaryFormatter();
            using (var output = File.OpenRead(@"D:\SimsFileShareDump\CasParts.data"))
                Entries = (Dictionary<BodyType, HashSet<string>>)binaryFormatter.Deserialize(output);

            comboBox1.Items.AddRange(Entries.Keys.Select(o => (object)o).ToArray());
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            flowLayoutPanel1.Controls.Clear();
            RenderImages((BodyType)comboBox1.SelectedItem);
        }

        private void RenderImages(BodyType entry)
        {
            var files = Entries[entry].Select(o => $"{Path.GetFileName(Path.GetDirectoryName(o))}\\{Path.GetFileName(o)}").Skip(LastPage * PageSize).Take(PageSize);

            foreach (var file in files)
            {
                if (Directory.Exists(Path.Combine(@"D:\SimsFileShareDump\FileThumbnails\", file)))
                    foreach (var image in Directory.EnumerateFiles(Path.Combine(@"D:\SimsFileShareDump\FileThumbnails\", file), "*.png"))
                    {
                        var panelEx = new PanelEx(image);
                        panelEx.Tag = Path.Combine(@"D:\SimsFileShareDump\FileContents\", file);
                        panelEx.MouseDoubleClick += PictureBox_MouseDoubleClick;
                        flowLayoutPanel1.Controls.Add(panelEx);
                    }
            }
            LastPage++;
        }

        private void PictureBox_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var panelEx = sender as PanelEx;
            Process.Start("explorer.exe", "/select, \"" + panelEx.Tag.ToString() + "\"");
        }

        private void listBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            flowLayoutPanel1.Controls.Clear();
            var entry = listBox1.SelectedItem.ToString();

            if (!Directory.Exists(Path.Combine(@"D:\SimsFileShareDump\FileThumbnails\", entry))) return;
            foreach (var image in Directory.EnumerateFiles(Path.Combine(@"D:\SimsFileShareDump\FileThumbnails\", entry), "*.png"))
            {
                var pictureBox = new PictureBox();
                pictureBox.Image = Image.FromFile(image);
                pictureBox.Width = pictureBox.Image.Width;
                pictureBox.Height = pictureBox.Image.Height;
                flowLayoutPanel1.Controls.Add(pictureBox);
            }
        }

        public class PanelEx : Panel
        {
            private string _imagePath { get; set; }

            public PanelEx(string imagePath)
            {
                Width = 104;
                Height = 146;
                _imagePath = imagePath;
                BackgroundImage = Image.FromFile(_imagePath);
            }
        }

        private void flowLayoutPanel1_Scroll(object sender, ScrollEventArgs e)
        {
            if (flowLayoutPanel1.VerticalScroll.Value > (int)(flowLayoutPanel1.VerticalScroll.Maximum * 0.9))
                RenderImages((BodyType)comboBox1.SelectedItem);
        }

        private void vScrollBar1_Scroll(object sender, ScrollEventArgs e)
        {
            //flowLayoutPanel1.VerticalScroll.Value = vScrollBar1.Value;
        }
    }
}
