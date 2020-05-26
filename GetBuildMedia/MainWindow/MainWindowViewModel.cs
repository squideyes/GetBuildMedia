#region Copyright & License
// Copyright 2020 by SquidEyes, LLC
// 
// Permission is hereby granted, free of charge, to any person 
// obtaining a copy of this software and associated documentation 
// files (the "Software"), to deal in the Software without 
// restriction, including without limitation the rights to use,
// copy, modify, merge, publish, distribute, sublicense, and/or 
// sell copies of the Software, and to permit persons to whom 
// the Software is furnished to do so, subject to the following 
// conditions:
// 
// The above copyright notice and this permission notice shall be 
// included in all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
// OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND 
// NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT 
// HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, 
// WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING 
// FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR 
// OTHER DEALINGS IN THE SOFTWARE.
#endregion

using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using System.Windows;
using System.Windows.Data;

namespace GetBuildMedia
{
    public class MainWindowViewModel : ViewModelBase
    {
        private readonly string folder = Path.Combine(Environment.GetFolderPath(
            Environment.SpecialFolder.MyDocuments), nameof(GetBuildMedia));

        private CancellationTokenSource cts;
        private int progress;
        private List<Episode> episodes;
        private string filterText;
        private bool allSelected;
        private bool fetchVideos;
        private bool fetchAudios;
        private string statusPrompt;

        public MainWindowViewModel(List<Episode> episodes)
        {
            if (!Directory.Exists(folder))
                Directory.CreateDirectory(folder);

            var fileNames = new HashSet<string>(Directory.GetFiles(folder));

            foreach (var episode in episodes)
            {
                episode.OnSelectionChanged += (s, e) =>
                {
                    if (allSelected && !e.Value)
                    {
                        allSelected = false;

                        RaisePropertyChanged(() => AllSelected);
                    }
                    else if (!allSelected && Episodes.All(c => c.Selected))
                    {
                        allSelected = true;

                        RaisePropertyChanged(() => AllSelected);
                    }

                    UpdateUI();
                };

                if (fileNames.Contains(episode.GetFullPath(folder, MediaKind.Audio)))
                    episode.HasAudio = true;

                if (fileNames.Contains(episode.GetFullPath(folder, MediaKind.Video)))
                    episode.HasVideo = true;
            }

            Episodes = episodes;

            EpisodeFilterView = (CollectionView)CollectionViewSource.GetDefaultView(Episodes);
            EpisodeFilterView.Filter = OnFilterTriggered;

            UpdateUI();

            FetchVideos = true;
            FetchAudios = false;

            cts = new CancellationTokenSource();

            ShowStandardStatusPrompt();
        }

        private void ShowStandardStatusPrompt() =>
            StatusPrompt = "Select one or more episodes then click the \"Fetch\" button to download media";

        public CollectionView EpisodeFilterView { get; set; }

        public string StatusPrompt
        {
            get => statusPrompt;
            set => Set(ref statusPrompt, value);
        }

        public List<Episode> Episodes
        {
            get => episodes;
            set => Set(ref episodes, value);
        }

        public bool FetchAudios
        {
            get => fetchAudios;
            set => Set(() => FetchAudios, ref fetchAudios, value);
        }

        public bool FetchVideos
        {
            get => fetchVideos;
            set => Set(() => FetchVideos, ref fetchVideos, value);
        }

        public bool Fetching { get; private set; }

        public bool AllSelected
        {
            get
            {
                return allSelected;
            }
            set
            {
                Set(() => AllSelected, ref allSelected, value);

                foreach (var episode in Episodes)
                    episode.Selected = value;

                UpdateUI();
            }
        }

        public string FilterText
        {
            get => filterText;
            set
            {
                Set(ref filterText, value);

                ApplyFilter();
            }
        }

        public int Progress
        {
            get => progress;
            set => Set(() => Progress, ref progress, value);
        }

        public bool OnFilterTriggered(object item)
        {
            if (item is Episode episode)
            {
                return FilterText == null
                    || episode.Title.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
                    || episode.Code.Contains(FilterText, StringComparison.OrdinalIgnoreCase)
                    || episode.TalentString.Contains(FilterText, StringComparison.OrdinalIgnoreCase);
            }

            return true;
        }

        public void ApplyFilter() => CollectionViewSource.GetDefaultView(Episodes).Refresh();

        private void UpdateUI()
        {
            RaisePropertyChanged(() => Fetching);
            RaisePropertyChanged(() => FetchCommand);
            RaisePropertyChanged(() => CancelCommand);
        }

        public RelayCommand FetchCommand => new RelayCommand(async () =>
        {
            List<Job> GetJobs(List<Episode> episodes, MediaKind mediaKind)
            {
                var jobs = new List<Job>();

                foreach (var episode in episodes)
                {
                    jobs.Add(new Job()
                    {
                        Episode = episode,
                        Folder = folder,
                        MediaKind = mediaKind
                    });
                }

                return jobs;
            }

            Fetching = true;

            UpdateUI();

            var episodes = Episodes.Where(
                e => e.Selected && e.Progress != 100).ToList();

            if (episodes.Count > 0)
            {
                var jobs = new List<Job>();

                if (FetchVideos)
                    jobs.AddRange(GetJobs(episodes, MediaKind.Video));

                if (FetchAudios)
                    jobs.AddRange(GetJobs(episodes, MediaKind.Audio));

                if (jobs.Count > 0)
                    await DownloadFilesAsync(jobs);
            }
        },
        () => !Fetching && Episodes.Any(c => c.Selected && c.Progress != 100));

        private void ExecuteMediaFile(string fullPath)
        {
            var p = new Process
            {
                StartInfo = new ProcessStartInfo(fullPath)
                {
                    UseShellExecute = true
                }
            };

            p.Start();
        }

        public RelayCommand<Episode> PlayAudioCommand => new RelayCommand<Episode>(
            episode => ExecuteMediaFile(episode.GetFullPath(folder, MediaKind.Audio)));

        public RelayCommand<Episode> PlayVideoCommand => new RelayCommand<Episode>(
            episode => ExecuteMediaFile(episode.GetFullPath(folder, MediaKind.Video)));

        public RelayCommand CancelCommand => new RelayCommand(() =>
        {
            Fetching = false;

            UpdateUI();

            cts.Cancel();
        },
        () => Fetching);


        public RelayCommand AboutCommand => new RelayCommand(() =>
        {
            MessageBox.Show($"A simple media downloader for video and audio files, sourced from Microsoft's Channel 9.  For further info plus source code, please visit http://github.com/squideyes/GetBuildMedia.",
                "About", MessageBoxButton.OK, MessageBoxImage.Question);
        });

        private async Task DownloadFilesAsync(List<Job> jobs)
        {
            var progressLock = new object();

            long expectedBytes = 0;
            long totalBytesRead = 0;

            foreach (var job in jobs)
            {
                if (job.Episode.Medias.ContainsKey(job.MediaKind))
                    expectedBytes += job.Episode.Medias[job.MediaKind].FileSize;
            }

            StatusPrompt = $"Downloading {jobs.Count} media file(s); click the \"Cancel\" button to cancel";

            var fetcher = new ActionBlock<Job>(
                async job =>
                {
                    job.OnProgress += (s, e) =>
                    {
                        lock (progressLock)
                        {
                            totalBytesRead += e.BytesRead;

                            Progress = (int)(totalBytesRead / (double)expectedBytes * 100.0);
                        }
                    };

                    await job.FetchAndSaveAsync(cts.Token);
                },
                new ExecutionDataflowBlockOptions()
                {
                    MaxDegreeOfParallelism = Environment.ProcessorCount,
                    CancellationToken = cts.Token
                });

            jobs.ForEach(job => fetcher.Post(job));

            fetcher.Complete();

            try
            {
                await fetcher.Completion;

                MessageBox.Show($"{jobs.Count} media files were downloaded.  Click any one of the Play buttons to play the media in your default player.", 
                    "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception error)
            {
                UpdateUI();

                MessageBox.Show(error.Message,
                    "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
            finally
            {
                Fetching = false;

                UpdateUI();

                Progress = 0;

                cts = new CancellationTokenSource();

                ShowStandardStatusPrompt();
            }
        }
    }
}
