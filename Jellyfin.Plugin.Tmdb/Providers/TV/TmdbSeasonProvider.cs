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
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;

namespace Jellyfin.Plugin.TmdbAdult.Providers.TV
{
    /// <summary>
    /// Tmdb season provider.
    /// </summary>
    public class TmdbSeasonProvider : IRemoteMetadataProvider<Season, SeasonInfo>
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly TmdbClientManager _tmdbClientManager;

        /// <summary>
        /// Initializes a new instance of the <see cref="TmdbSeasonProvider"/> class.
        /// </summary>
        /// <param name="httpClientFactory">Instance of the <see cref="IHttpClientFactory"/> interface.</param>
        /// <param name="tmdbClientManager">Instance of the <see cref="TmdbClientManager"/>.</param>
        public TmdbSeasonProvider(IHttpClientFactory httpClientFactory, TmdbClientManager tmdbClientManager)
        {
            _httpClientFactory = httpClientFactory;
            _tmdbClientManager = tmdbClientManager;
        }

        /// <inheritdoc />
        public string Name => TmdbUtils.ProviderName;

        /// <inheritdoc />
        public async Task<MetadataResult<Season>> GetMetadata(SeasonInfo info, CancellationToken cancellationToken)
        {
            var result = new MetadataResult<Season>();

            info.SeriesProviderIds.TryGetValue(MetadataProvider.Tmdb.ToString(), out var seriesTmdbId);

            var seasonNumber = info.IndexNumber;

            if (string.IsNullOrWhiteSpace(seriesTmdbId) || !seasonNumber.HasValue)
            {
                return result;
            }

            var seasonResult = await _tmdbClientManager
                .GetSeasonAsync(Convert.ToInt32(seriesTmdbId, CultureInfo.InvariantCulture), seasonNumber.Value, info.MetadataLanguage, TmdbUtils.GetImageLanguagesParam(info.MetadataLanguage), cancellationToken)
                .ConfigureAwait(false);

            if (seasonResult == null)
            {
                return result;
            }

            result.HasMetadata = true;
            result.Item = new Season
            {
                IndexNumber = seasonNumber,
                Overview = seasonResult.Overview
            };

            if (!string.IsNullOrEmpty(seasonResult.ExternalIds?.TvdbId))
            {
                result.Item.SetProviderId(MetadataProvider.Tvdb, seasonResult.ExternalIds.TvdbId);
            }

            // TODO why was this disabled?
            var credits = seasonResult.Credits;
            if (credits?.Cast != null)
            {
                var cast = credits.Cast.OrderBy(c => c.Order).Take(TmdbUtils.MaxCastMembers).ToList();
                for (var i = 0; i < cast.Count; i++)
                {
                    result.AddPerson(new PersonInfo
                    {
                        Name = cast[i].Name.Trim(),
                        Role = cast[i].Character,
                        Type = PersonType.Actor,
                        SortOrder = cast[i].Order
                    });
                }
            }

            if (credits?.Crew != null)
            {
                foreach (var person in credits.Crew)
                {
                    // Normalize this
                    var type = TmdbUtils.MapCrewToPersonType(person);

                    if (!TmdbUtils.WantedCrewTypes.Contains(type, StringComparer.OrdinalIgnoreCase)
                        && !TmdbUtils.WantedCrewTypes.Contains(person.Job ?? string.Empty, StringComparer.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    result.AddPerson(new PersonInfo
                    {
                        Name = person.Name.Trim(),
                        Role = person.Job,
                        Type = type
                    });
                }
            }

            result.Item.PremiereDate = seasonResult.AirDate;
            result.Item.ProductionYear = seasonResult.AirDate?.Year;

            return result;
        }

        /// <inheritdoc />
        public Task<IEnumerable<RemoteSearchResult>> GetSearchResults(SeasonInfo searchInfo, CancellationToken cancellationToken)
        {
            return Task.FromResult(Enumerable.Empty<RemoteSearchResult>());
        }

        /// <inheritdoc />
        public Task<HttpResponseMessage> GetImageResponse(string url, CancellationToken cancellationToken)
        {
            return _httpClientFactory.CreateClient(NamedClient.Default).GetAsync(new Uri(url), cancellationToken);
        }
    }
}
