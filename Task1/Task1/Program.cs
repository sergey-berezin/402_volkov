using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using NuGetYOLO;

namespace Task1
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            try
            {
                var cancellationTokenSource = new CancellationTokenSource();

                // Создание экземпляра детектора
                var detector = new NuGetYOLO.Detection(new DownloadFile());

                // Загрузка изображений и анализ
                var tasks = args.Select((arg, index) => AnalyzeImage(detector, arg, cancellationTokenSource.Token, index)).ToArray();

                // Ожидание завершения всех задач
                await Task.WhenAll(tasks);

                // Сохранение результатов
                foreach (var result in tasks.Select(t => t.Result))
                {
                    SaveResults(result);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Detecting cancelled!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
            }
        }

        private static async Task<(string fileName, string curCsv, Image<Rgb24> curImg)> AnalyzeImage(NuGetYOLO.Detection detector, string imagePath, CancellationToken cancellationToken, int index)
        {
            Console.WriteLine($"Analyzing image {index}...");

            try
            {
                var result = await detector.Analyze(imagePath, cancellationToken);

                Console.WriteLine($"Image {index} analyzed.");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error analyzing image {index}: {ex.Message}");
                return (null, null, null);
            }
        }

        private static void SaveResults((string fileName, string curCsv, Image<Rgb24> curImg) result)
        {
            if (result.fileName != null)
            {
                if (!File.Exists("resultsAsync.csv") || !File.ReadLines("resultsAsync.csv").Any(s => string.CompareOrdinal(s + "\n", result.curCsv) == 0))
                {
                    File.AppendAllText("resultsAsync.csv", result.curCsv);
                }

                result.curImg.Save(result.fileName);
            }
        }

        public class DownloadFile : NuGetYOLO.FileDownloader
        {
            public void Download(string url, string fileName)
            {
                using (var client = new WebClient())
                {
                    while (true)
                    {
                        try
                        {
                            Console.WriteLine($"Downloading {url}...");
                            client.DownloadFile(url, fileName);
                            Console.WriteLine($"Downloaded {url}!");
                            break;
                        }
                        catch (WebException)
                        {
                            Console.WriteLine($"Error downloading {url}. Retrying...");
                            continue;
                        }
                    }
                }
            }

            public bool Exists(string path) => File.Exists(path);

            public void Print(string msg) => Console.WriteLine(msg);
        }
    }
}

