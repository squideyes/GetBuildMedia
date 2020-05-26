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

using CodeHollow.FeedReader;
using CodeHollow.FeedReader.Feeds;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace GetBuildMedia
{
    public class RssClient
    {
        private const string URL = "https://s.ch9.ms/Events/Build/2020/RSS";

        public async Task<List<Episode>> GetEpisodesAsync()
        {
            var feed = await FeedReader.ReadAsync(URL);

            var episodes = new List<Episode>();

            foreach (var item in feed.Items)
            {
                var specific = item.SpecificItem as MediaRssFeedItem;

                var medias = new Dictionary<MediaKind, Media>();

                foreach (var m in specific.MediaGroups.First().Media)
                {
                    var fileName = Path.GetFileName(m.Url).ToLower();

                    var media = new Media()
                    {
                        FileSize = m.FileSize.Value,
                        Duration = m.Duration.Value,
                        Uri = new Uri(m.Url),
                        ContentType = m.Type
                    };

                    if (fileName.EndsWith(".mp3"))
                        media.Kind = MediaKind.Audio;
                    else if (fileName.EndsWith("_high.mp4"))
                        media.Kind = MediaKind.Video;
                    else
                        continue;

                    if (!medias.ContainsKey(media.Kind))
                        medias.Add(media.Kind, media);
                }

                var doc = new HtmlDocument();

                doc.LoadHtml(item.Description);

                var episode = new Episode()
                {
                    Code = item.Link.Split("/").Last(),
                    Title = item.Title.Trim(),
                    Link = new Uri(item.Link),
                    PubDate = item.PublishingDate.Value,
                    Duration = TimeSpan.FromSeconds(
                        medias.Select(x => x.Value.Duration).Average()),
                    Synopsis = doc.DocumentNode.InnerText.Trim(),
                    Medias = medias,
                    Talent = specific.DC.Creator.Split(',').ToList()
                };

                episodes.Add(episode);
            }

            return episodes;
        }
    }
}
