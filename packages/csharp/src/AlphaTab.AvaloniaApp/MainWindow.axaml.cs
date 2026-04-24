using System.Collections.Generic;
using System.IO;
using System.Linq;
using AlphaTab.Model;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace AlphaTab.AvaloniaApp
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            ConfigureChineseFonts();
        }

        private void ConfigureChineseFonts()
        {
            var textFont = new Font("Microsoft YaHei, SimSun, Arial, sans-serif", 12, global::AlphaTab.Model.FontStyle.Italic);
            var scoreFont = new Font("Microsoft YaHei, SimSun, Arial, sans-serif", 20, global::AlphaTab.Model.FontStyle.Plain);
            var markerFont = new Font("Microsoft YaHei, SimSun, Arial, sans-serif", 14, global::AlphaTab.Model.FontStyle.Plain, global::AlphaTab.Model.FontWeight.Bold);

            AlphaTabView.Settings.Display.Resources.TitleFont = scoreFont.WithSize(32);
            AlphaTabView.Settings.Display.Resources.SubTitleFont = scoreFont;
            AlphaTabView.Settings.Display.Resources.WordsFont = scoreFont.WithSize(15);
            AlphaTabView.Settings.Display.Resources.MarkerFont = markerFont;
            AlphaTabView.Settings.Display.Resources.ElementFonts.Set(NotationElement.EffectText, textFont);
            AlphaTabView.Settings.Display.Resources.ElementFonts.Set(NotationElement.EffectLyrics, textFont);
            AlphaTabView.Settings.Display.Resources.ElementFonts.Set(NotationElement.TrackNames, textFont);
            AlphaTabView.Settings.Display.Resources.ElementFonts.Set(NotationElement.ScoreArtist, scoreFont);
            AlphaTabView.Settings.Display.Resources.ElementFonts.Set(NotationElement.ScoreAlbum, scoreFont);
            AlphaTabView.Settings.Display.Resources.ElementFonts.Set(NotationElement.ScoreMusic, scoreFont.WithSize(15));
            AlphaTabView.Settings.Display.Resources.ElementFonts.Set(NotationElement.ScoreWordsAndMusic, scoreFont.WithSize(15));
        }

        private async void OnOpenFileClick(object? sender, RoutedEventArgs e)
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "选择乐谱文件",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new FilePickerFileType("Score Files")
                    {
                        Patterns = new[] { "*.gp3", "*.gp4", "*.gp5", "*.gpx", "*.gp", "*.xml", "*.musicxml", "*.mxl", "*.alphaTex", "*.at" }
                    },
                    FilePickerFileTypes.All
                }
            });

            var file = files.FirstOrDefault();
            if (file == null)
            {
                return;
            }

            await using var stream = await file.OpenReadAsync();
            using var memory = new MemoryStream();
            await stream.CopyToAsync(memory);
            AlphaTabView.Settings.Importer.Encoding = GetImporterEncoding(file.Name);
            AlphaTabView.Api?.Load(memory.ToArray(), new List<double> { -1 });
        }

        private static string GetImporterEncoding(string fileName)
        {
            switch (Path.GetExtension(fileName).ToLowerInvariant())
            {
                case ".gp":
                case ".gp3":
                case ".gp4":
                case ".gp5":
                    return "gb2312";
                default:
                    return "utf-8";
            }
        }

        private void OnPlayPauseClick(object? sender, RoutedEventArgs e)
        {
            var api = AlphaTabView.Api;
            if (api == null)
            {
                return;
            }

            api.PlayPause();
        }

        private void OnStopClick(object? sender, RoutedEventArgs e)
        {
            var api = AlphaTabView.Api;
            if (api == null)
            {
                return;
            }

            api.Stop();
        }
    }
}
