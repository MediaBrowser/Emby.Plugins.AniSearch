using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Emby.Anime;
using MediaBrowser.Common.Net;
using System.IO;
using MediaBrowser.Model.Net;
using MediaBrowser.Model.Logging;

namespace Emby.Plugins.AniSearch
{
    /// <summary>
    /// API for http://anisearch.de a german anime database
    /// 🛈 Anisearch does not have an API interface to work with
    /// </summary>
    internal class Api
    {
        public static List<string> anime_search_names = new List<string>();
        public static List<string> anime_search_ids = new List<string>();
        public static string SearchLink = "https://www.anisearch.de/anime/index/?char=all&page=1&text={0}&smode=2&sort=title&order=asc&view=2&title=de,en,fr,it,pl,ru,es,tr&titlex=1,2&hentai=yes";
        public static string AniSearch_anime_link = "https://www.anisearch.de/anime/";

        private IHttpClient _httpClient;
        private ILogger _logger;

        /// <summary>
        /// WebContent API call to get a anime with id
        /// </summary>
        /// <param name="id"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Api(ILogger logger, IHttpClient httpClient)
        {
            _logger = logger;
            _httpClient = httpClient;
        }

        /// <summary>
        /// API call to get the anime with the id
        /// </summary>
        /// <param name="id"></param>
        /// <returns></returns>
        public async Task<RemoteSearchResult> GetAnime(string id, string preferredLanguage, CancellationToken cancellationToken)
        {
            string WebContent = await WebRequestAPI(AniSearch_anime_link + id, cancellationToken).ConfigureAwait(false);
            var result = new RemoteSearchResult
            {
                Name = SelectName(WebContent, preferredLanguage)
            };

            result.SearchProviderName = One_line_regex(new Regex("\"" + "Japanisch" + "\"" + @"> <strong>(.*?)<\/"), WebContent);
            result.ImageUrl = Get_ImageUrl(WebContent);
            result.SetProviderId(ProviderNames.AniSearch, id);
            result.Overview = Get_Overview(WebContent);

            return result;
        }

        /// <summary>
        /// API call to select the lang
        /// </summary>
        /// <param name="WebContent"></param>
        /// <param name="preference"></param>
        /// <param name="language"></param>
        /// <returns></returns>
        private string SelectName(string WebContent, string preferredLanguage)
        {
            if (string.IsNullOrEmpty(preferredLanguage) || preferredLanguage.StartsWith("en", StringComparison.OrdinalIgnoreCase))
            {
                var title = Get_title("en", WebContent);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    return title;
                }
            }
            if (string.Equals(preferredLanguage, "de", StringComparison.OrdinalIgnoreCase))
            {
                var title = Get_title("de", WebContent);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    return title;
                }
            }
            if (string.Equals(preferredLanguage, "ja", StringComparison.OrdinalIgnoreCase))
            {
                var title = Get_title("jap", WebContent);
                if (!string.IsNullOrWhiteSpace(title))
                {
                    return title;
                }
            }

            return Get_title("jap_r", WebContent);
        }

        /// <summary>
        /// API call to get the title with the right lang
        /// </summary>
        /// <param name="lang"></param>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public string Get_title(string lang, string WebContent)
        {
            switch (lang)
            {
                case "en":
                    return One_line_regex(new Regex("\"" + "Englisch" + "\"" + @"> <strong>(.*?)<\/"), WebContent);

                case "de":
                    return One_line_regex(new Regex("\"" + "Deutsch" + "\"" + @"> <strong>(.*?)<\/"), WebContent);

                case "jap":
                    return One_line_regex(new Regex("<div class=\"grey\">" + @"(.*?)<\/"), One_line_regex(new Regex("\"" + "Englisch" + "\"" + @"> <strong>(.*?)<\/div"), WebContent));

                //Default is jap_r
                default:
                    return One_line_regex(new Regex("\"" + "Japanisch" + "\"" + @"> <strong>(.*?)<\/"), WebContent);
            }
        }

        /// <summary>
        /// API call to get the genre of the anime
        /// </summary>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public List<string> Get_Genre(string WebContent)
        {
            List<string> result = new List<string>();
            string Genres = One_line_regex(new Regex("<ul class=\"cloud\">" + @"(.*?)<\/ul>"), WebContent);
            int x = 0;
            string AniSearch_Genre = null;
            while (AniSearch_Genre != "" && x < 100)
            {
                AniSearch_Genre = One_line_regex(new Regex(@"<li>(.*?)<\/li>"), Genres, 0, x);
                AniSearch_Genre = One_line_regex(new Regex("\">" + @"(.*?)<\/a>"), AniSearch_Genre);
                if (!string.IsNullOrEmpty(AniSearch_Genre))
                {
                    result.Add(AniSearch_Genre);
                }
                x++;
            }
            return result;
        }

        /// <summary>
        /// API call to get the img url
        /// </summary>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public string Get_ImageUrl(string WebContent)
        {
            return One_line_regex(new Regex("<img itemprop=\"image\" src=\"" + @"(.*?)" + "\""), WebContent);
        }

        /// <summary>
        /// API call too get the rating
        /// </summary>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public string Get_Rating(string WebContent)
        {
            return One_line_regex(new Regex("<span itemprop=\"ratingValue\">" + @"(.*?)" + @"<\/span>"), WebContent);
        }

        /// <summary>
        /// API call to get the description
        /// </summary>
        /// <param name="WebContent"></param>
        /// <returns></returns>
        public string Get_Overview(string WebContent)
        {
            return Regex.Replace(One_line_regex(new Regex("<span itemprop=\"description\" lang=\"de\" id=\"desc-de\" class=\"desc-zz textblock\">" + @"(.*?)<\/span>"), WebContent), "<.*?>", String.Empty);
        }

        /// <summary>
        /// API call to search a title and return the right one back
        /// </summary>
        /// <param name="title"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<string> Search_GetSeries(string title, CancellationToken cancellationToken)
        {
            _logger.Debug("Search_GetSeries: {0}", title);

            anime_search_names.Clear();
            anime_search_ids.Clear();
            string result = null;
            string result_text = null;
            string WebContent = await WebRequestAPI(string.Format(SearchLink, title), cancellationToken).ConfigureAwait(false);
            int x = 0;
            while (result_text != "" && x < 100)
            {
                result_text = One_line_regex(new Regex("<th scope=\"row\" class=\"showpop\" data-width=\"200\"" + @".*?>(.*)<\/th>"), WebContent, 1, x);

                if (!string.IsNullOrEmpty(result_text))
                {
                    //get id
                    int _x = 0;
                    string a_name = null;
                    while (a_name != "" && _x < 100)
                    {
                        string id = One_line_regex(new Regex(@"anime\/(.*?),"), result_text);
                        a_name = Regex.Replace(One_line_regex(new Regex(@"((<a|<d).*?>)(.*?)(<\/a>|<\/div>)"), result_text, 3, _x), "<.*?>", String.Empty);
                        if (a_name != "")
                        {
                            if (Equals_check.Compare_strings(a_name, title))
                            {
                                return id;
                            }
                            if (Int32.TryParse(id, out int n))
                            {
                                anime_search_names.Add(a_name);
                                anime_search_ids.Add(id);
                            }
                        }

                        _x++;
                    }
                }
                x++;
            }

            return result;
        }

        /// <summary>
        /// API call to search a title and return a list back
        /// </summary>
        /// <param name="title"></param>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public async Task<List<string>> Search_GetSeries_list(string title, CancellationToken cancellationToken)
        {
            List<string> result = new List<string>();
            string result_text = null;
            string WebContent = await WebRequestAPI(string.Format(SearchLink, title), cancellationToken).ConfigureAwait(false);
            int x = 0;
            while (result_text != "" && x < 100)
            {
                result_text = One_line_regex(new Regex("<th scope=\"row\" class=\"showpop\" data-width=\"200\"" + @".*?>(.*)<\/th>"), WebContent, 1, x);
                if (result_text != "")
                {
                    //get id
                    int _x = 0;
                    string a_name = null;
                    while (a_name != "" && _x < 100)
                    {
                        string id = One_line_regex(new Regex(@"anime\/(.*?),"), result_text);
                        a_name = Regex.Replace(One_line_regex(new Regex(@"((<a|<d).*?>)(.*?)(<\/a>|<\/div>)"), result_text, 3, _x), "<.*?>", String.Empty);
                        if (a_name != "")
                        {
                            if (Equals_check.Compare_strings(a_name, title))
                            {
                                result.Add(id);
                                return result;
                            }
                            if (Int32.TryParse(id, out int n))
                            {
                                result.Add(id);
                            }
                        }
                        _x++;
                    }
                }
                x++;
            }
            return result;
        }

        /// <summary>
        /// SEARCH Title
        /// </summary>
        public async Task<string> FindSeries(string title, CancellationToken cancellationToken)
        {
            string aid = await Search_GetSeries(title, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(aid))
            {
                return aid;
            }
            else
            {
                int x = 0;

                foreach (string a_name in anime_search_names)
                {
                    if (Equals_check.Compare_strings(a_name, title))
                    {
                        return anime_search_ids[x];
                    }
                    x++;
                }
            }

            aid = await Search_GetSeries(Equals_check.Clear_name(title), cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(aid))
            {
                return aid;
            }
            return null;
        }

        /// <summary>
        /// Simple regex
        /// </summary>
        public string One_line_regex(Regex regex, string match, int group = 1, int match_int = 0)
        {
            int x = 0;
            MatchCollection matches = regex.Matches(match);

            foreach (Match _match in matches)
            {
                if (x == match_int)
                {
                    return _match.Groups[group].Value.ToString();
                }
                x++;
            }
            return "";
        }

        public async Task<string> WebRequestAPI(string link, CancellationToken cancellationToken)
        {
            var options = new HttpRequestOptions
            {
                CancellationToken = cancellationToken,
                Url = link
            };

            using (var stream = await _httpClient.Get(options).ConfigureAwait(false))
            {
                using (var reader = new StreamReader(stream))
                {
                    return await reader.ReadToEndAsync().ConfigureAwait(false);
                }
            }
        }
    }
}