using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MyYoloApp
{
    public interface DirectoryManaging
    {
        List<string> ChooseFolder();
        void Print(string message);
    }

    public class Detection
    {
        public string SourcePath { get; set; }
        public string DetectedClass { get; set; }
        public double Confidence { get; set; }

        public Detection(string p, string c, double d)
        {
            SourcePath = p;
            DetectedClass = c;
            Confidence = d;
        }
    }

    public class Viewer : Change
    {
        public AsyncRelayCommand ChooseDirectory { get; private set; }
        public AsyncRelayCommand CancelDetection { get; private set; }
        public List<Detection> DetectionsList { get; private set; }
        private Detection? selection;

        public Detection? SelectedImg
        {
            get => selection;
            set
            {
                selection = value;
                RaisePropertyChanged(nameof(ImagePath));
            }
        }

        public string ImagePath => SelectedImg != null ? Path.ChangeExtension(SelectedImg.SourcePath, "Detected.jpg") : Directory.GetCurrentDirectory();

        private NuGetYOLO.Detection detector;
        private CancellationTokenSource tokenSource;

        public Viewer(NuGetYOLO.FileDownloader FileDr, DirectoryManaging DirManager)
        {
            detector = new NuGetYOLO.Detection(FileDr);
            DetectionsList = new List<Detection>();
            ChooseDirectory = new AsyncRelayCommand(async _ =>
            {
                try
                {
                    List<string> selectedFiles = DirManager.ChooseFolder()
                        .Where(file => file.EndsWith(".jpg") && !file.EndsWith("Detected.jpg"))
                        .ToList();

                    tokenSource = new CancellationTokenSource();
                    var analyzeTasks = selectedFiles
                        .Select(async file =>
                        {
                            var result = await detector.AnalyzeAsync(file, tokenSource.Token);
                            return (file, result);
                        })
                        .ToList();

                    var results = await Task.WhenAll(analyzeTasks);

                    foreach (var (file, result) in results)
                    {
                        foreach (var (item1, item2) in result.detections)
                        {
                            DetectionsList.Add(new Detection(file, item1, item2));
                        }

                        result.detectedImage.Save(Path.ChangeExtension(file, "Detected.jpg"));
                    }

                    DetectionsList = DetectionsList.OrderBy(x => x.DetectedClass).ThenBy(y => y.Confidence).ToList();
                    RaisePropertyChanged(nameof(DetectionsList));
                }
                catch (OperationCanceledException)
                {
                    DirManager.Print("Detection cancelled!");
                }
            });

            CancelDetection = new AsyncRelayCommand(_ =>
            {
                tokenSource?.Cancel();
                return Task.CompletedTask;
            }, _ => tokenSource != null);
        }
    }
}

