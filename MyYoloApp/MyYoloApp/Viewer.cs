using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using SixLabors.ImageSharp;

namespace MyYoloApp
{
    public interface DirectoryManaging
    {
        List<string> ChooseFolder();
        void Print(string message);
    }

    public class Detection
    {
        public string ImageSource { get; set; }
        public string Class { get; set; }
        public double Confidence { get; set; }

        public Detection(string path, string dclass, double conf)
        {
            ImageSource = path;
            Class = dclass;
            Confidence = conf;
        }
    }

    public class ClassInfo
    {
        public string ClassName { get; set; }
        public int ObjectCount { get; set; }
    }

    public abstract class Change : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        protected void RaisePropertyChanged([CallerMemberName] string propertyName = "") =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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
        private ObservableCollection<string> filteredImages;
        public ObservableCollection<string> FilteredImages
        {
            get => filteredImages;
            set
            {
                filteredImages = value;
                RaisePropertyChanged(nameof(FilteredImages));
            }
        }
        private ClassInfo? selectedClassInfo;
        public ClassInfo? SelectedClassInfo
        {
            get => selectedClassInfo;
            set
            {
                if (selectedClassInfo != value)
                {
                    selectedClassInfo = value;
                    RaisePropertyChanged(nameof(SelectedClassInfo));
                    UpdateFilteredImages();
                }
            }
        }
        public AsyncRelayCommand SelectClassCommand { get; private set; }

        public string ImagePath => SelectedImg != null ? Path.ChangeExtension(SelectedImg.ImageSource, "Detected.jpg") : Directory.GetCurrentDirectory();
        private NuGetYOLO.Detection detector;
        private CancellationTokenSource tokenSource;

        private List<ClassInfo> detectionClasses;
        public List<ClassInfo> DetectionClasses
        {
            get => detectionClasses;
            set
            {
                detectionClasses = value;
                RaisePropertyChanged(nameof(DetectionClasses));
            }
        }

        private ObservableCollection<ClassInfo> classObjectCounts;
        public ObservableCollection<ClassInfo> ClassObjectCounts
        {
            get => classObjectCounts;
            set
            {
                classObjectCounts = value;
                RaisePropertyChanged(nameof(ClassObjectCounts));
            }
        }

        public Viewer(NuGetYOLO.FileDownloader FileDr, DirectoryManaging DirManager)
        {
            detector = new NuGetYOLO.Detection(FileDr);
            DetectionsList = new List<Detection>();
            ClassObjectCounts = new ObservableCollection<ClassInfo>();
            FilteredImages = new ObservableCollection<string>();
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

                    DetectionsList = DetectionsList.OrderBy(x => x.Class).ThenBy(y => y.Confidence).ToList();
                    RaisePropertyChanged(nameof(DetectionsList));
                    UpdateClassObjectCounts();
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

            SelectClassCommand = new AsyncRelayCommand(_ =>
            {
                UpdateFilteredImages();
                return Task.CompletedTask;
            });
        }

        public void UpdateClassObjectCounts()
        {
            ClassObjectCounts = new ObservableCollection<ClassInfo>
            {
                new ClassInfo { ClassName = "All Objects", ObjectCount = DetectionsList.Count }
            };

            foreach (var detection in DetectionsList)
            {
                var existingClassInfo = ClassObjectCounts.FirstOrDefault(c => c.ClassName == detection.Class);

                if (existingClassInfo == null)
                {
                    ClassObjectCounts.Add(new ClassInfo { ClassName = detection.Class, ObjectCount = 1 });
                }
                else
                {
                    existingClassInfo.ObjectCount++;
                }
            }
            UpdateFilteredImages();
        }

        private void UpdateFilteredImages()
        {
            if (SelectedClassInfo == null || SelectedClassInfo.ClassName == "All Objects")
            {
                FilteredImages = new ObservableCollection<string>(DetectionsList.Select(d => d.ImageSource));
            }
            else
            {
                FilteredImages = new ObservableCollection<string>(DetectionsList
                    .Where(d => d.Class == SelectedClassInfo.ClassName)
                    .Select(d => d.ImageSource));
            }
        }
    }
}

