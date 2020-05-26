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

using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace GetBuildMedia
{
    internal class Job
    {
        private const int BUFFER_SIZE = 1024 * 1024 * 4;

        private static readonly HttpClient client = new HttpClient();

        public Episode Episode { get; set; }
        public string Folder { get; set; }
        public MediaKind MediaKind { get; set; }

        public event EventHandler<ProgressArgs> OnProgress;

        public async Task<bool> FetchAndSaveAsync(CancellationToken cancellationToken)
        {
            var startedOn = DateTime.UtcNow;

            if (!Directory.Exists(Folder))
                Directory.CreateDirectory(Folder);

            var fullPath = Episode.GetFullPath(Folder, MediaKind);

            var buffer = new byte[BUFFER_SIZE];

            var response = await client.GetAsync(Episode.Medias[MediaKind].Uri,
                HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (!response.IsSuccessStatusCode)
                return false;

            if (cancellationToken.IsCancellationRequested)
                return false;

            var target = new MemoryStream();

            // TODO: Check for RSS vs. HttpResponse ContentLength missmatch
            var fileSize = response.Content.Headers.ContentLength.Value;

            using (var source = await response.Content.ReadAsStreamAsync())
            {
                int bytesRead;

                do
                {
                    if (cancellationToken.IsCancellationRequested)
                        return false;

                    bytesRead = await source.ReadAsync(buffer, 0, buffer.Length);

                    if (bytesRead > 0)
                    {
                        target.Write(buffer, 0, bytesRead);

                        OnProgress?.Invoke(this, new ProgressArgs(bytesRead));
                    }
                }
                while (bytesRead != 0);
            }

            if (cancellationToken.IsCancellationRequested)
                return false;

            target.Position = 0;

            using var saveTo = File.Open(fullPath, FileMode.Create);

            await target.CopyToAsync(saveTo, cancellationToken);

            if (cancellationToken.IsCancellationRequested)
                return false;

            if (MediaKind == MediaKind.Video)
                Episode.HasVideo = true;
            else if (MediaKind == MediaKind.Audio)
                Episode.HasAudio = true;

            return true;
        }
    }
}
