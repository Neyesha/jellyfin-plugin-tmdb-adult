using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Common.Net;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Providers;
using MediaBrowser.Model.Dto;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Extensions;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.TmdbAdult.Providers.TV
{
    /// <summary>
    /// Tmdb season image provider.
    /// </summary>
    public class TmdbSeasonImageProvider : IRemoteImageProvider
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TmdbClientManager _tmdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TmdbSeasonImageProvider"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        /// <param name="tmdbClientManager">Instance of the <see cref="TmdbClientManager"/>.</param>
        public TmdbSeasonImageProvider(IHttpClientFactory httpClientFactory, TmdbClientManager tmdbClientManager)
        {
            _httpClientFactory = httpClientFactory;
            _tmdbClientManager = tmdbClientManager;
        }

        /// <inheritdoc />
        public string Name => TmdbUtils.ProviderName;

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
        }

        /// <inheritdoc />
        public async Task<IEnumerable<RemoteImageInfo>> GetImages(BaseItem item, CancellationToken cancellationToken)
        {
            var season = (Season)item;
            var series = season?.Series;

            var seriesTmdbId = Convert.ToInt32(series?.GetProviderId(MetadataProvider.Tmdb), CultureInfo.InvariantCulture);

            if (seriesTmdbId <= 0 || season?.IndexNumber == null)
            {
                return Enumerable.Empty<RemoteImageInfo>();
            }

            var language = item.GetPreferredMetadataLanguage();

            var seasonResult = await _tmdbClientManager
                .GetSeasonAsync(seriesTmdbId, season.IndexNumber.Value, language, TmdbUtils.GetImageLanguagesParam(language), cancellationToken)
                .ConfigureAwait(false);

            var posters = seasonResult?.Images?.Posters;
            if (posters == null)
            {
                return Enumerable.Empty<RemoteImageInfo>();
            }

            var remoteImages = new RemoteImageInfo[posters.Count];
            for (var i = 0; i < posters.Count; i++)
            {
                var image = posters[i];
                remoteImages[i] = new RemoteImageInfo
                {
                    Url = _tmdbClientManager.GetPosterUrl(image.FilePath),
                    CommunityRating = image.VoteAverage,
                    VoteCount = image.VoteCount,
                    Width = image.Width,
                    Height = image.Height,
                    Language = TmdbUtils.AdjustImageLanguage(image.Iso_639_1, language),
                    ProviderName = Name,
                    Type = ImageType.Primary,
                    RatingType = RatingType.Score
                };
            }

            return remoteImages.OrderByLanguageDescending(language);
        }

        /// <inheritdoc />
        public IEnumerable<ImageType> GetSupportedImages(BaseItem item)
        {
            yield return ImageType.Primary;
        }

        /// <inheritdoc />
        public bool Supports(BaseItem item)
        {
            return item is Season;
        }
    }
}
