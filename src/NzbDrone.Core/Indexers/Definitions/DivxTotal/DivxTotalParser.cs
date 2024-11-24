using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using AngleSharp.Html.Parser;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.Parser;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.Definitions.DivxTotal
{
    public class DivxTotalParser : IParseIndexerResponse
    {
        private readonly DivxTotalSettings _settings;
        private readonly IndexerCapabilitiesCategories _categories;

        public DivxTotalParser(DivxTotalSettings settings, IndexerCapabilitiesCategories categories)
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

            var rows = dom.QuerySelectorAll("table.table > tbody > tr");
            foreach (var row in rows)
            {
                var anchor = row.QuerySelector("a");
                var title = anchor.TextContent.Trim();

                var detailsStr = anchor.GetAttribute("href");
                var cat = detailsStr.Split("/")[3];

                var publishStr = row.QuerySelectorAll("td")[2].TextContent.Trim();
                var publishDate = TryToParseDate(publishStr, DateTime.Now);
                var sizeStr = row.QuerySelectorAll("td")[3].TextContent.Trim();

                var release = new TorrentInfo
                {
                    Guid = detailsStr,
                    InfoUrl = detailsStr,
                    DownloadUrl = $"{(cat != DivxTotalCategories.Series ? detailsStr : "")}",
                    Title = title,
                    Categories = _categories.MapTrackerCatToNewznab(cat),
                    PublishDate = publishDate,
                    Files = 1,
                    Seeders = 1,
                    Peers = 2,
                    Size = TryToParseSize(sizeStr, DivxTotalFizeSizes.Otros),
                    DownloadVolumeFactor = 0,
                    UploadVolumeFactor = 1
                };

                releaseInfos.Add(release);
            }

            return releaseInfos.ToArray();
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

        private static long TryToParseSize(string sizeToParse, long sizeDefault)
        {
            try
            {
                var parsedSize = ParseUtil.GetBytes(sizeToParse);
                return parsedSize > 0 ? parsedSize : sizeDefault;
            }
            catch
            {
                return sizeDefault;
            }
        }

        public Action<IDictionary<string, string>, DateTime?> CookiesUpdater { get; set; }
    }
}
