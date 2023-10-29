using System;
using System.Windows;
using System.Windows.Forms;
using MyYoloApp;

namespace MyYoloApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new Viewer(new FileLoad(), new DirectoryManager());
        }
    }

    public class FileLoad : NuGetYOLO.FileDownloader
    {
        public void Download(string url, string fileName)
        {
            using (var client = new System.Net.WebClient())
            {
                while (true)
                {
                    try
                    {
                        client.DownloadFile(url, fileName);
                        break;
                    }
                    catch (System.Net.WebException)
                    {
                        // Handle the exception, if needed
                    }
                }
            }
        }

        public bool Exists(string path) => System.IO.File.Exists(path);

        public void Print(string msg) => Console.WriteLine(msg);
    }

    public class DirectoryManager : DirectoryManaging
    {
        public System.Collections.Generic.List<string> ChooseFolder()
        {
            var dialog = new FolderBrowserDialog
            {
                Description = "Choose a folder with images"
            };

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                return new System.Collections.Generic.List<string>(System.IO.Directory.GetFiles(dialog.SelectedPath));
            }

            return new System.Collections.Generic.List<string>();
        }

        public void Print(string msg) => System.Windows.MessageBox.Show(msg);
    }
}
