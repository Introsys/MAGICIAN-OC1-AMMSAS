using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace iSent.Magician.ConsoleApp.Services.ImageSources
{
    public class FileFolderImageSource : IImageSourceService
    {
        public event Action<byte[]>? OnImageFrameReceived;

        private readonly string _folderPath;
        private List<string> _imageFiles = new();
        private int _currentIndex = 0;

        public FileFolderImageSource(string folderPath)
        {
            _folderPath = folderPath;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (!Directory.Exists(_folderPath))
            {
                Console.WriteLine($"[File Source Error] Directory '{_folderPath}' does not exist.");
                return Task.CompletedTask;
            }

            ReloadFiles();
            Console.WriteLine($"[File Source] Loaded {_imageFiles.Count} images from directory: '{_folderPath}'");
            return Task.CompletedTask;
        }

        public void ReloadFiles()
        {
            var validExtensions = new[] { ".jpg", ".png", ".bmp", ".raw", ".tiff", ".tif" };
            if (Directory.Exists(_folderPath))
            {
                _imageFiles = Directory.GetFiles(_folderPath)
                    .Where(file => validExtensions.Contains(Path.GetExtension(file).ToLower()))
                    .OrderBy(f => f)
                    .ToList();
            }
        }

        /// <summary>
        /// returns the next file on the list "round-robin" style.
        /// </summary>
        public string? GetNextFilePath()
        {
            if (!_imageFiles.Any())
            {
                ReloadFiles();
                if (!_imageFiles.Any()) return null;
            }

            string file = _imageFiles[_currentIndex];
            _currentIndex = (_currentIndex + 1) % _imageFiles.Count;
            return file;
        }

        public Task StopAsync() => Task.CompletedTask;
        public void Dispose() { }
    }
}