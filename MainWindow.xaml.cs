using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace Fantasy.FileHashVerifier
{
    public partial class MainWindow : Window
    {
        private readonly Dictionary<string, HashResults> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly SolidColorBrush _defaultRowBrush;
        private readonly SolidColorBrush _defaultRowBorderBrush;
        private readonly SolidColorBrush _dropZoneDefaultBrush;
        private readonly SolidColorBrush _dropZoneDefaultBorderBrush;
        private string? _currentFilePath;

        public MainWindow()
        {
            InitializeComponent();
            _defaultRowBrush = (SolidColorBrush)FindResource("ControlBackgroundBrush");
            _defaultRowBorderBrush = (SolidColorBrush)FindResource("BorderBrushColor");
            _dropZoneDefaultBrush = (SolidColorBrush)FindResource("PanelBackgroundBrush");
            _dropZoneDefaultBorderBrush = (SolidColorBrush)FindResource("BorderBrushColor");
            ClearHashDisplay();
            UpdateFileCount();
        }

        private void DropZone_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                e.Effects = DragDropEffects.Copy;
                DropZone.Background = (SolidColorBrush)FindResource("ControlHoverBrush");
                DropZone.BorderBrush = (SolidColorBrush)FindResource("AccentBrush");
            }
            else
            {
                e.Effects = DragDropEffects.None;
            }
            e.Handled = true;
        }

        private void DropZone_DragOver(object sender, DragEventArgs e)
        {
            e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
            e.Handled = true;
        }

        private void DropZone_DragLeave(object sender, DragEventArgs e)
        {
            DropZone.Background = _dropZoneDefaultBrush;
            DropZone.BorderBrush = _dropZoneDefaultBorderBrush;
            e.Handled = true;
        }

        private async void DropZone_Drop(object sender, DragEventArgs e)
        {
            DropZone.Background = _dropZoneDefaultBrush;
            DropZone.BorderBrush = _dropZoneDefaultBorderBrush;
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var paths = (string[])e.Data.GetData(DataFormats.FileDrop);
                await AddFilesAsync(paths);
            }
            e.Handled = true;
        }

        private async void BrowseButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Multiselect = true,
                Title = "Select files to hash",
                Filter = "All files (*.*)|*.*"
            };
            if (dialog.ShowDialog(this) == true)
            {
                await AddFilesAsync(dialog.FileNames);
            }
        }

        private void ClearButton_Click(object sender, RoutedEventArgs e)
        {
            FilesListBox.Items.Clear();
            _cache.Clear();
            _currentFilePath = null;
            ClearHashDisplay();
            SelectedFileName.Text = "No file selected";
            SelectedFilePath.Text = string.Empty;
            SelectedFileSize.Text = string.Empty;
            ComputeStatusLabel.Text = string.Empty;
            VerifyStatusLabel.Text = string.Empty;
            UpdateFileCount();
        }

        private async Task AddFilesAsync(IEnumerable<string> paths)
        {
            var addedFiles = new List<string>();
            foreach (var path in paths)
            {
                if (!File.Exists(path)) continue;
                var existing = FilesListBox.Items.Cast<FileEntry>().FirstOrDefault(f => string.Equals(f.FullPath, path, StringComparison.OrdinalIgnoreCase));
                if (existing != null) continue;
                var entry = new FileEntry(path);
                FilesListBox.Items.Add(entry);
                addedFiles.Add(path);
            }
            UpdateFileCount();
            if (FilesListBox.SelectedItem == null && FilesListBox.Items.Count > 0)
            {
                FilesListBox.SelectedIndex = 0;
            }
            await Task.CompletedTask;
        }

        private async void FilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var entry = FilesListBox.SelectedItem as FileEntry;
            if (entry == null)
            {
                _currentFilePath = null;
                ClearHashDisplay();
                SelectedFileName.Text = "No file selected";
                SelectedFilePath.Text = string.Empty;
                SelectedFileSize.Text = string.Empty;
                return;
            }
            _currentFilePath = entry.FullPath;
            SelectedFileName.Text = entry.DisplayName;
            SelectedFilePath.Text = entry.FullPath;
            try
            {
                var info = new FileInfo(entry.FullPath);
                SelectedFileSize.Text = FormatBytes(info.Length);
            }
            catch
            {
                SelectedFileSize.Text = string.Empty;
            }
            ResetRowHighlights();
            VerifyStatusLabel.Text = string.Empty;

            if (_cache.TryGetValue(entry.FullPath, out var cached))
            {
                DisplayHashes(cached);
                ComputeStatusLabel.Text = "Loaded from cache";
                return;
            }

            await ComputeAndDisplayAsync(entry.FullPath);
        }

        private async Task ComputeAndDisplayAsync(string path)
        {
            ClearHashDisplay();
            ComputeProgress.Visibility = Visibility.Visible;
            ComputeStatusLabel.Text = "Computing hashes...";
            var started = DateTime.UtcNow;
            try
            {
                var result = await Task.Run(() => ComputeAllHashes(path));
                if (!string.Equals(_currentFilePath, path, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }
                _cache[path] = result;
                DisplayHashes(result);
                var elapsed = DateTime.UtcNow - started;
                ComputeStatusLabel.Text = $"Computed in {elapsed.TotalMilliseconds:F0} ms";
            }
            catch (Exception ex)
            {
                var errorResult = new HashResults
                {
                    Md5 = $"Error: {ex.Message}",
                    Sha1 = $"Error: {ex.Message}",
                    Sha256 = $"Error: {ex.Message}",
                    Sha512 = $"Error: {ex.Message}"
                };
                DisplayHashes(errorResult);
                ComputeStatusLabel.Text = "Failed to compute hashes";
            }
            finally
            {
                ComputeProgress.Visibility = Visibility.Collapsed;
            }
        }

        private static HashResults ComputeAllHashes(string path)
        {
            byte[] data;
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                using var ms = new MemoryStream();
                stream.CopyTo(ms);
                data = ms.ToArray();
            }

            var md5Task = Task.Run(() =>
            {
                using var alg = MD5.Create();
                return Convert.ToHexString(alg.ComputeHash(data));
            });
            var sha1Task = Task.Run(() =>
            {
                using var alg = SHA1.Create();
                return Convert.ToHexString(alg.ComputeHash(data));
            });
            var sha256Task = Task.Run(() =>
            {
                using var alg = SHA256.Create();
                return Convert.ToHexString(alg.ComputeHash(data));
            });
            var sha512Task = Task.Run(() =>
            {
                using var alg = SHA512.Create();
                return Convert.ToHexString(alg.ComputeHash(data));
            });

            Task.WaitAll(md5Task, sha1Task, sha256Task, sha512Task);

            return new HashResults
            {
                Md5 = md5Task.Result,
                Sha1 = sha1Task.Result,
                Sha256 = sha256Task.Result,
                Sha512 = sha512Task.Result
            };
        }

        private void DisplayHashes(HashResults results)
        {
            Md5TextBox.Text = results.Md5;
            Sha1TextBox.Text = results.Sha1;
            Sha256TextBox.Text = results.Sha256;
            Sha512TextBox.Text = results.Sha512;
        }

        private void ClearHashDisplay()
        {
            Md5TextBox.Text = string.Empty;
            Sha1TextBox.Text = string.Empty;
            Sha256TextBox.Text = string.Empty;
            Sha512TextBox.Text = string.Empty;
            ResetRowHighlights();
        }

        private void ResetRowHighlights()
        {
            Md5Row.Background = _defaultRowBrush;
            Sha1Row.Background = _defaultRowBrush;
            Sha256Row.Background = _defaultRowBrush;
            Sha512Row.Background = _defaultRowBrush;
            Md5Row.BorderBrush = _defaultRowBorderBrush;
            Sha1Row.BorderBrush = _defaultRowBorderBrush;
            Sha256Row.BorderBrush = _defaultRowBorderBrush;
            Sha512Row.BorderBrush = _defaultRowBorderBrush;
        }

        private void CopyButton_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button?.Tag is not string algo) return;
            var value = algo switch
            {
                "MD5" => Md5TextBox.Text,
                "SHA1" => Sha1TextBox.Text,
                "SHA256" => Sha256TextBox.Text,
                "SHA512" => Sha512TextBox.Text,
                _ => string.Empty
            };
            if (string.IsNullOrEmpty(value)) return;
            try
            {
                Clipboard.SetText(value);
                ComputeStatusLabel.Text = $"{algo} hash copied to clipboard";
            }
            catch
            {
                ComputeStatusLabel.Text = "Failed to access clipboard";
            }
        }

        private void VerifyButton_Click(object sender, RoutedEventArgs e)
        {
            ResetRowHighlights();
            var expected = ExpectedHashTextBox.Text?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(expected))
            {
                VerifyStatusLabel.Text = "Enter an expected hash value";
                VerifyStatusLabel.Foreground = (SolidColorBrush)FindResource("SubtleForegroundBrush");
                return;
            }
            if (_currentFilePath == null || !_cache.ContainsKey(_currentFilePath))
            {
                VerifyStatusLabel.Text = "Select a file first";
                VerifyStatusLabel.Foreground = (SolidColorBrush)FindResource("SubtleForegroundBrush");
                return;
            }
            var item = AlgoComboBox.SelectedItem as ComboBoxItem;
            var algo = item?.Content?.ToString() ?? "SHA256";
            var results = _cache[_currentFilePath];
            var computed = algo switch
            {
                "MD5" => results.Md5,
                "SHA1" => results.Sha1,
                "SHA256" => results.Sha256,
                "SHA512" => results.Sha512,
                _ => string.Empty
            };
            var normalizedExpected = new string(expected.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
            var normalizedComputed = computed.ToUpperInvariant();
            var target = algo switch
            {
                "MD5" => Md5Row,
                "SHA1" => Sha1Row,
                "SHA256" => Sha256Row,
                "SHA512" => Sha512Row,
                _ => null
            };
            if (normalizedExpected == normalizedComputed)
            {
                VerifyStatusLabel.Text = $"MATCH - {algo} hash verified successfully";
                VerifyStatusLabel.Foreground = (SolidColorBrush)FindResource("SuccessBrush");
                if (target != null)
                {
                    target.Background = (SolidColorBrush)FindResource("SuccessBrush");
                    target.BorderBrush = (SolidColorBrush)FindResource("SuccessBrush");
                }
            }
            else
            {
                VerifyStatusLabel.Text = $"MISMATCH - {algo} hash does not match expected value";
                VerifyStatusLabel.Foreground = (SolidColorBrush)FindResource("ErrorBrush");
                if (target != null)
                {
                    target.Background = (SolidColorBrush)FindResource("ErrorBrush");
                    target.BorderBrush = (SolidColorBrush)FindResource("ErrorBrush");
                }
            }
        }

        private void UpdateFileCount()
        {
            var count = FilesListBox.Items.Count;
            FileCountLabel.Text = count == 1 ? "1 file" : $"{count} files";
        }

        private static string FormatBytes(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unit = 0;
            while (size >= 1024 && unit < units.Length - 1)
            {
                size /= 1024;
                unit++;
            }
            return $"{size:0.##} {units[unit]}";
        }
    }

    public class FileEntry
    {
        public string FullPath { get; }
        public string DisplayName { get; }

        public FileEntry(string path)
        {
            FullPath = path;
            DisplayName = Path.GetFileName(path);
        }

        public override string ToString() => DisplayName;
    }

    public class HashResults
    {
        public string Md5 { get; set; } = string.Empty;
        public string Sha1 { get; set; } = string.Empty;
        public string Sha256 { get; set; } = string.Empty;
        public string Sha512 { get; set; } = string.Empty;
    }
}
