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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace GetBuildMedia
{
    public class Episode : ObservableObject
    {
        private int progress = 0;
        private bool selected = false;
        private bool hasAudio = false;
        private bool hasVideo = false;

        public event EventHandler<SelectionChangedArgs> OnSelectionChanged;

        public bool Selected
        {
            get
            {
                return selected;
            }
            set
            {
                Set(() => Selected, ref selected, value);

                OnSelectionChanged.Invoke(
                    this, new SelectionChangedArgs(value));
            }
        }

        public string Code { get; set; }
        public DateTime PubDate { get; set; }
        public Uri Link { get; set; }
        public TimeSpan Duration { get; set; }
        public string Title { get; set; }
        public string Synopsis { get; set; }
        public List<string> Talent { get; set; }
        public Dictionary<MediaKind, Media> Medias { get; set; }

        public bool HasAudio
        { 
            get => hasAudio;
            set => Set(() => HasAudio, ref hasAudio, value); 
        }

        public bool HasVideo
        {
            get => hasVideo;
            set => Set(() => HasVideo, ref hasVideo, value);
        }

        public int Progress
        {
            get => progress;
            set => Set(() => Progress, ref progress, value);
        }

        public bool CanFetchAudio =>
            !HasAudio && Medias.ContainsKey(MediaKind.Audio);

        public bool CanFetchVideo =>
            !HasAudio && Medias.ContainsKey(MediaKind.Audio);

        public string TalentString => string.Join(",", Talent);

        public string GetFullPath(string folder, MediaKind mediaKind)
        {
            var sb = new StringBuilder();

            sb.Append(Path.GetInvalidFileNameChars().Aggregate(
                Title, (current, c) => current.Replace(c.ToString(), " ")));
            sb.Append('.');
            sb.Append(mediaKind == MediaKind.Video ? "mp4" : "mp3");

            return Path.Combine(folder, sb.ToString());
        }
    }
}
