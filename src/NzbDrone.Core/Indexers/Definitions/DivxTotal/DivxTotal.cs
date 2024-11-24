using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NLog;
using NzbDrone.Common.Extensions;
using NzbDrone.Common.Http;
using NzbDrone.Core.Configuration;
using NzbDrone.Core.Http.CloudFlare;
using NzbDrone.Core.Indexers.Exceptions;
using NzbDrone.Core.IndexerSearch.Definitions;
using NzbDrone.Core.Messaging.Events;
using NzbDrone.Core.Parser.Model;

namespace NzbDrone.Core.Indexers.Definitions.DivxTotal
{
    public class DivxTotal : TorrentIndexerBase<DivxTotalSettings>
    {
        public override string Name => "DivxTotal";
        public override string[] IndexerUrls => new[] { "https://www5.divxtotal.mov/" };
        public override string Description => "DivxTotal is a SPANISH site for Movies, TV series and Software";
        public override string Language => "es-ES";
        public override Encoding Encoding => Encoding.UTF8;
        public override IndexerPrivacy Privacy => IndexerPrivacy.Public;
        public override IndexerCapabilities Capabilities => SetCapabilities();

        public override int PageSize => 15;

        public DivxTotal(IIndexerHttpClient httpClient,
            IEventAggregator eventAggregator,
            IIndexerStatusService indexerStatusService,
            IConfigService configService,
            Logger logger)
            : base(httpClient, eventAggregator, indexerStatusService, configService, logger)
        {
        }

        public override IIndexerRequestGenerator GetRequestGenerator()
        {
            return new DivxTotalRequestGenerator(Settings, Capabilities);
        }

        public override IParseIndexerResponse GetParser()
        {
            return new DivxTotalParser(Settings, Capabilities.Categories);
        }

        protected IParseIndexerResponse getSeriesParser()
        {
            return new DivxTotalSeriesParser(Settings, Capabilities.Categories);
        }

        protected override async Task<IndexerPageableQueryResult> FetchReleases(Func<IIndexerRequestGenerator, IndexerPageableRequestChain> pageableRequestChainSelector, SearchCriteriaBase searchCriteria, bool isRecent = false)
        {
            var releases = new List<ReleaseInfo>();
            var result = new IndexerPageableQueryResult();
            var url = string.Empty;
            var minimumBackoff = TimeSpan.FromHours(1);

            try
            {
                var generator = GetRequestGenerator();
                var parser = GetParser();
                var seriesParser = getSeriesParser();
                parser.CookiesUpdater = (cookies, expiration) =>
                {
                    _indexerStatusService.UpdateCookies(Definition.Id, cookies, expiration);
                };

                var pageableRequestChain = pageableRequestChainSelector(generator);

                for (var i = 0; i < pageableRequestChain.Tiers; i++)
                {
                    var pageableRequests = pageableRequestChain.GetTier(i);

                    foreach (var pageableRequest in pageableRequests)
                    {
                        var pagedReleases = new List<ReleaseInfo>();

                        var pageSize = PageSize;

                        foreach (var request in pageableRequest)
                        {
                            url = request.Url.FullUri;

                            var page = await FetchPage(request, parser);

                            var filteredPage = new IndexerQueryResult();

                            // Sadly, releases are partial at this point if the result was a series, since DivxTotal does not have meaningful info at the search level
                            // They also aren´t filtered by category, so we need to filter them here and remove them.
                            for (var j = page.Releases.Count - 1; j >= 0; j--)
                            {
                                // return results only for requested categories
                                if (searchCriteria.Categories.Any() && !searchCriteria.Categories.Any(c => page.Releases[j].Categories.Any(cat => cat.Id == c)))
                                {
                                    page.Releases.RemoveAt(j);
                                    continue;
                                }

                                if (searchCriteria is BasicSearchCriteria or TvSearchCriteria || string.IsNullOrWhiteSpace(page.Releases[j].DownloadUrl))
                                {
                                    // Fetch the info page for the release
                                    var info = await FetchPage(new IndexerRequest(page.Releases[j].InfoUrl, HttpAccept.Html), getSeriesParser());
                                    pagedReleases.AddRange(info.Releases);
                                    page.Releases.RemoveAt(i);
                                }
                            }

                            pageSize = pageSize == 1 ? page.Releases.Count : pageSize;

                            result.Queries.Add(page);

                            pagedReleases.AddRange(page.Releases);

                            if (!IsFullPage(page.Releases, pageSize))
                            {
                                break;
                            }
                        }

                        releases.AddRange(pagedReleases.Where(r => IsValidRelease(r, searchCriteria.InteractiveSearch)));
                    }

                    if (releases.Any())
                    {
                        break;
                    }
                }

                _indexerStatusService.RecordSuccess(Definition.Id);
            }
            catch (WebException webException)
            {
                if (webException.Status is WebExceptionStatus.NameResolutionFailure or WebExceptionStatus.ConnectFailure)
                {
                    _indexerStatusService.RecordConnectionFailure(Definition.Id);
                }
                else
                {
                    _indexerStatusService.RecordFailure(Definition.Id);
                }

                if (webException.Message.Contains("502") || webException.Message.Contains("503") ||
                    webException.Message.Contains("504") || webException.Message.Contains("timed out"))
                {
                    _logger.Warn(webException, "{0} server is currently unavailable. {1} {2}", this, url, webException.Message);
                }
                else
                {
                    _logger.Warn(webException, "{0} {1} {2}", this, url, webException.Message);
                }
            }
            catch (TooManyRequestsException ex)
            {
                result.Queries.Add(new IndexerQueryResult { Response = ex.Response });

                var retryTime = ex.RetryAfter != TimeSpan.Zero ? ex.RetryAfter : minimumBackoff;

                _indexerStatusService.RecordFailure(Definition.Id, retryTime);
                _logger.Warn(ex, "Request Limit reached for {0}. Disabled for {1}", this, retryTime);
            }
            catch (HttpException ex)
            {
                result.Queries.Add(new IndexerQueryResult { Response = ex.Response });
                _indexerStatusService.RecordFailure(Definition.Id);

                if (ex.Response.HasHttpServerError)
                {
                    _logger.Warn(ex, "Unable to connect to {0} at [{1}]. Indexer's server is unavailable. Try again later. {2}", this, url, ex.Message);
                }
                else
                {
                    _logger.Warn(ex, "{0} {1}", this, ex.Message);
                }
            }
            catch (RequestLimitReachedException ex)
            {
                result.Queries.Add(new IndexerQueryResult { Response = ex.Response.HttpResponse });
                _indexerStatusService.RecordFailure(Definition.Id, minimumBackoff);
                _logger.Warn(ex, "Request Limit reached for {0}. Disabled for {1}", this, minimumBackoff);
            }
            catch (IndexerAuthException ex)
            {
                _indexerStatusService.RecordFailure(Definition.Id);
                _logger.Warn(ex, "Invalid Credentials for {0} {1}", this, url);
            }
            catch (CloudFlareProtectionException ex)
            {
                result.Queries.Add(new IndexerQueryResult { Response = ex.Response });
                _indexerStatusService.RecordFailure(Definition.Id);
                ex.WithData("FeedUrl", url);
                _logger.Error(ex, "Cloudflare protection detected for {0}, Flaresolverr may be required.", this);
            }
            catch (IndexerException ex)
            {
                result.Queries.Add(new IndexerQueryResult { Response = ex.Response.HttpResponse });
                _indexerStatusService.RecordFailure(Definition.Id);
                _logger.Warn(ex, "{0}", url);
            }
            catch (HttpRequestException ex)
            {
                _indexerStatusService.RecordFailure(Definition.Id);
                _logger.Warn(ex, "Unable to connect to indexer, please check your DNS settings and ensure IPv6 is working or disabled. {0}", url);
            }
            catch (TaskCanceledException ex)
            {
                _indexerStatusService.RecordFailure(Definition.Id);
                _logger.Warn(ex, "Unable to connect to indexer, possibly due to a timeout. {0}", url);
            }
            catch (Exception ex)
            {
                _indexerStatusService.RecordFailure(Definition.Id);
                ex.WithData("FeedUrl", url);
                _logger.Error(ex, "An error occurred while processing indexer feed. {0}", url);
            }

            result.Releases = CleanupReleases(releases, searchCriteria);

            return result;
        }

        private IndexerCapabilities SetCapabilities()
        {
            var caps = new IndexerCapabilities
            {
                TvSearchParams = new List<TvSearchParam>
                {
                    TvSearchParam.Q, TvSearchParam.Season, TvSearchParam.Ep
                },
                MovieSearchParams = new List<MovieSearchParam>
                {
                    MovieSearchParam.Q,
                },
            };

            caps.Categories.AddCategoryMapping(DivxTotalCategories.Peliculas, NewznabStandardCategory.MoviesSD, "Películas");
            caps.Categories.AddCategoryMapping(DivxTotalCategories.PeliculasHd, NewznabStandardCategory.MoviesHD, "Películas HD");
            caps.Categories.AddCategoryMapping(DivxTotalCategories.Peliculas3D, NewznabStandardCategory.Movies3D, "Películas 3D");
            caps.Categories.AddCategoryMapping(DivxTotalCategories.PeliculasDvdr, NewznabStandardCategory.MoviesDVD, "Películas DVD-r");
            caps.Categories.AddCategoryMapping(DivxTotalCategories.Series, NewznabStandardCategory.TVSD, "Series");
            caps.Categories.AddCategoryMapping(DivxTotalCategories.Programas, NewznabStandardCategory.PC, "Programas");
            caps.Categories.AddCategoryMapping(DivxTotalCategories.Otros, NewznabStandardCategory.OtherMisc, "Otros");

            return caps;
        }
    }
}
