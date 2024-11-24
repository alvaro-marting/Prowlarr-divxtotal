using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.Definitions.DivxTotal
{
    public class DivxTotalSeriesParser : IParseIndexerResponse
    {
        private const string DownloadLink = "/download_tt.php";
        private readonly DivxTotalSettings _settings;
        private readonly IndexerCapabilitiesCategories _categories;

        public Action<IDictionary<string, string>, DateTime?> CookiesUpdater { get; set; }

        public DivxTotalSeriesParser(DivxTotalSettings settings, IndexerCapabilitiesCategories categories)
        {
            _settings = settings;
            _categories = categories;
        }

        public IList<ReleaseInfo> ParseResponse(IndexerResponse indexerResponse)
        {
            if (indexerResponse.HttpResponse.StatusCode != HttpStatusCode.OK)
            {
                throw new IndexerException(indexerResponse, $"Anidex search returned unexpected result. Expected 200 OK but got {indexerResponse.HttpResponse.StatusCode}.");
            }

            var releaseInfos = new List<ReleaseInfo>();

            var parser = new HtmlParser();
            using var dom = parser.ParseDocument(indexerResponse.Content);
            var detailsStr = indexerResponse.HttpRequest.Url.ToString();
            var cat = detailsStr.Split("/")[3];

            var infoDiv = dom.QuerySelector(".panel-body>.row>.col-lg-7");
            var publishDateContainer = infoDiv.QuerySelectorAll(".info-item")[1];
            var publishDateStr = publishDateContainer.QuerySelectorAll("p")[1].TextContent.Trim();
            var publishDate = TryToParseDate(publishDateStr, DateTime.Now);

            var tables = dom.QuerySelectorAll("table.rwd-table");
            foreach (var table in tables)
            {
                var rows = table.QuerySelectorAll("tbody > tr");
                foreach (var row in rows)
                {
                    var anchor = row.QuerySelector("a");
                    var episodeTitle = anchor.TextContent.Trim();

                    // Convert the title to Scene format
                    episodeTitle = ParseDivxTotalSeriesTitle(episodeTitle);

                    var downloadLink = GetDownloadLink(row);

                    releaseInfos.Add(GenerateRelease(episodeTitle, detailsStr, downloadLink, cat, publishDate, DivxTotalFizeSizes.Series));
                }
            }

            return releaseInfos.ToArray();
        }

        private ReleaseInfo GenerateRelease(string title, string detailsStr, string downloadLink, string cat, DateTime publishDate, long size)
        {
            var release = new ReleaseInfo
            {
                Title = title,
                InfoUrl = detailsStr,
                DownloadUrl = downloadLink,
                Guid = downloadLink,
                Categories = _categories.MapTrackerCatToNewznab(cat),
                PublishDate = publishDate,
                Size = size,
                Files = 1,
            };
            return release;
        }

        private static string ParseDivxTotalSeriesTitle(string episodeTitle)
        {
            // episodeTitle = American Horror Story6x04
            var newTitle = episodeTitle;
            try
            {
                var r = new Regex("(([0-9]+)x([0-9]+)[^0-9]*)$", RegexOptions.IgnoreCase);
                var m = r.Match(newTitle);
                if (m.Success)
                {
                    var season = "S" + m.Groups[2].Value.PadLeft(2, '0');
                    var episode = "E" + m.Groups[3].Value.PadLeft(2, '0');
                    newTitle = newTitle.Replace(m.Groups[1].Value, " " + season + episode);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            newTitle += " SPANISH SDTV XviD";

            // return American Horror Story S06E04 SPANISH SDTV XviD
            return newTitle;
        }

        private static DateTime TryToParseDate(string dateToParse, DateTime dateDefault)
        {
            try
            {
                return DateTime.ParseExact(dateToParse, "dd-MM-yyyy", CultureInfo.InvariantCulture);
            }
            catch
            {
                return dateDefault;
            }
        }

        private static string GetDownloadLink(IElement dom) =>
            dom.QuerySelector($"a[href*=\"{DownloadLink}\"]")?.GetAttribute("href");
    }
}
