using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Text.RegularExpressions;
using NzbDrone.Common.Http;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Parser;

namespace NzbDrone.Core.Indexers.Definitions.DivxTotal
{
    public class DivxTotalRequestGenerator : IIndexerRequestGenerator
    {
        private readonly DivxTotalSettings _settings;
        private readonly IndexerCapabilities _capabilities;

        public DivxTotalRequestGenerator(DivxTotalSettings settings, IndexerCapabilities capabilities)
        {
            _settings = settings;
            _capabilities = capabilities;
        }

        public IndexerPageableRequestChain GetSearchRequests(MovieSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();

            pageableRequests.Add(GetPagedRequests($"{searchCriteria.SanitizedSearchTerm}"));

            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(MusicSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        public IndexerPageableRequestChain GetSearchRequests(TvSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();

            pageableRequests.Add(GetPagedRequests($"{searchCriteria.SanitizedTvSearchString}", "/series-6"));

            return pageableRequests;
        }

        public IndexerPageableRequestChain GetSearchRequests(BookSearchCriteria searchCriteria)
        {
            return new IndexerPageableRequestChain();
        }

        public IndexerPageableRequestChain GetSearchRequests(BasicSearchCriteria searchCriteria)
        {
            var pageableRequests = new IndexerPageableRequestChain();

            pageableRequests.Add(GetPagedRequests($"{searchCriteria.SanitizedSearchTerm}"));

            return pageableRequests;
        }

        private IEnumerable<IndexerRequest> GetPagedRequests(string term, string sub = "")
        {
            var parameters = new NameValueCollection
            {
                { "s", Sanitize(term) ?? string.Empty }
            };

            var i = 0;
            do
            {
                i++;
                var searchUrl = $"{_settings.BaseUrl}{sub}/page/{i}?{parameters.GetQueryString()}";

                var request = new IndexerRequest(searchUrl, HttpAccept.Html);

                yield return request;
            }
            while (true);
        }

        public Func<IDictionary<string, string>> GetCookies { get; set; }
        public Action<IDictionary<string, string>, DateTime?> CookiesUpdater { get; set; }

        private static string Sanitize(string term)
        {
            // replace punctuation symbols with spaces
            // searchTerm = Marco Polo 2014
            term = Regex.Replace(term, @"[-._\(\)@/\\\[\]\+\%]", " ");
            term = Regex.Replace(term, @"\s+", " ");
            term = term.Trim();

            // we parse the year and remove it from search
            // searchTerm = Marco Polo
            // query.Year = 2014
            var r = new Regex("([ ]+([0-9]{4}))$", RegexOptions.IgnoreCase);
            var m = r.Match(term);
            if (m.Success)
            {
                term = term.Replace(m.Groups[1].Value, "");
            }

            term = Regex.Replace(term, @"\b(espa[ñn]ol|spanish|castellano|spa)\b", "", RegexOptions.IgnoreCase);

            return term;
        }
    }
}
