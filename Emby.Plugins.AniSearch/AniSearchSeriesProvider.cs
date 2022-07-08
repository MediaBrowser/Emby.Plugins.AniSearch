using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Logging;
using MediaBrowser.Model.Providers;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Model.Configuration;
using Emby.Anime;

namespace Emby.Plugins.AniSearch
{
    public class AniSearchSeriesProvider : IRemoteMetadataProvider<Series, SeriesInfo>, IHasOrder
    {
        private readonly IHttpClient _httpClient;
        private readonly IApplicationPaths _paths;
        private readonly ILogger _log;
        public int Order => 7;
        public string Name => "AniSearch";

        private Api _api;

        public AniSearchSeriesProvider(IApplicationPaths appPaths, IHttpClient httpClient, ILogManager logManager)
        {
            _log = logManager.GetLogger(Name);
            _httpClient = httpClient;
            _paths = appPaths;
            _api = new Api(_log, httpClient);
        }

        public async Task<MetadataResult<Series>> GetMetadata(SeriesInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Series>();

            var aid = info.GetProviderId(ProviderNames.AniSearch);
            if (string.IsNullOrEmpty(aid))
            {
                _log.Info("Start AniSearch... Searching(" + info.Name + ")");
                aid = await _api.FindSeries(info.Name, cancellationToken).ConfigureAwait(false);
            }

            if (!string.IsNullOrEmpty(aid))
            {
                _log.Info("AniSearch search by aid {0}", aid);
                string WebContent = await _api.WebRequestAPI(Api.AniSearch_anime_link + aid, cancellationToken).ConfigureAwait(false);
                result.Item = new Series();
                result.HasMetadata = true;

                result.Item.SetProviderId(ProviderNames.AniSearch, aid);
                result.Item.Overview = _api.Get_Overview(WebContent);
                try
                {
                    //AniSearch has a max rating of 5
                    result.Item.CommunityRating = (float.Parse(_api.Get_Rating(WebContent), System.Globalization.CultureInfo.InvariantCulture) * 2);
                }
                catch (Exception) { }
                foreach (var genre in _api.Get_Genre(WebContent))
                    result.Item.AddGenre(genre);
                GenreHelper.CleanupGenres(result.Item);
                StoreImageUrl(aid, _api.Get_ImageUrl(WebContent), "image");
            }
            return result;
        }

        public async Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeriesInfo searchInfo, CancellationToken cancellationToken)
        {
            var results = new Dictionary<string, RemoteSearchResult>();

            var aid = searchInfo.GetProviderId(ProviderNames.AniSearch);
            if (!string.IsNullOrEmpty(aid))
            {
                if (!results.ContainsKey(aid))
                    results.Add(aid, await _api.GetAnime(aid, searchInfo.MetadataLanguage, cancellationToken).ConfigureAwait(false));
            }

            if (!string.IsNullOrEmpty(searchInfo.Name))
            {
                List<string> ids = await _api.Search_GetSeries_list(searchInfo.Name, cancellationToken).ConfigureAwait(false);
                foreach (string a in ids)
                {
                    results.Add(a, await _api.GetAnime(a, searchInfo.MetadataLanguage, cancellationToken).ConfigureAwait(false));
                }
            }

            return results.Values;
        }

        private void StoreImageUrl(string series, string url, string type)
        {
            var path = Path.Combine(_paths.CachePath, "anisearch", type, series + ".txt");
            var directory = Path.GetDirectoryName(path);
            Directory.CreateDirectory(directory);

            File.WriteAllText(path, url);
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClient.GetResponse(new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = url
            });
        }
    }

    public class AniSearchSeriesImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClient _httpClient;
        private Api _api;

        public AniSearchSeriesImageProvider(IHttpClient httpClient, ILogManager logManager)
        {
            _httpClient = httpClient;
            _api = new Api(logManager.GetLogger(Name), httpClient);
        }

        public string Name => "AniSearch";

        public bool Supports(BaseItem item) => item is Series || item is Season;

        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            return new[] { ImageType.Primary };
        }

        public Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, LibraryOptions libraryOptions, CancellationToken cancellationToken)
        {
            var seriesId = item.GetProviderId(ProviderNames.AniSearch);
            return GetImages(seriesId, cancellationToken);
        }

        public async Task<IEnumerable<RemoteImageInfo>> GetImages(string aid, CancellationToken cancellationToken)
        {
            var list = new List<RemoteImageInfo>();

            if (!string.IsNullOrEmpty(aid))
            {
                var primary = _api.Get_ImageUrl(await _api.WebRequestAPI(Api.AniSearch_anime_link + aid, cancellationToken).ConfigureAwait(false));
                list.Add(new RemoteImageInfo
                {
                    ProviderName = Name,
                    Type = ImageType.Primary,
                    Url = primary
                });
            }
            return list;
        }

        public Task<HttpResponseInfo> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClient.GetResponse(new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = url
            });
        }
    }
}