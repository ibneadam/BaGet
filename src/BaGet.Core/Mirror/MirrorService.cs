﻿using System;
using System.Threading;
using System.Threading.Tasks;
using BaGet.Core.Services;
using Microsoft.Extensions.Logging;
using NuGet.Versioning;

namespace BaGet.Core.Mirror
{
    public class MirrorService : IMirrorService
    {
        private readonly Uri _packageBaseAddress;
        private readonly IPackageService _localPackages;
        private readonly IPackageDownloader _downloader;
        private readonly IIndexingService _indexer;
        private readonly ILogger<MirrorService> _logger;

        public MirrorService(
            Uri packageBaseAddress,
            IPackageService localPackages,
            IPackageDownloader downloader,
            IIndexingService indexer,
            ILogger<MirrorService> logger)
        {
            _packageBaseAddress = packageBaseAddress ?? throw new ArgumentNullException(nameof(packageBaseAddress));
            _localPackages = localPackages ?? throw new ArgumentNullException(nameof(localPackages));
            _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
            _indexer = indexer ?? throw new ArgumentNullException(nameof(indexer));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task MirrorAsync(string id, NuGetVersion version)
        {
            if (await _localPackages.ExistsAsync(id, version))
            {
                return;
            }

            await TryIndexFromSourceAsync(id, version);
        }

        private async Task<bool> TryIndexFromSourceAsync(string id, NuGetVersion version)
        {
            var idString = id.ToLowerInvariant();
            var versionString = version.ToNormalizedString().ToLowerInvariant();

            _logger.LogInformation(
                "Attempting to index package {Id} {Version} from upstream source...",
                idString,
                versionString);

            try
            {
                // See https://github.com/NuGet/NuGet.Client/blob/4eed67e7e159796ae486d2cca406b283e23b6ac8/src/NuGet.Core/NuGet.Protocol/Resources/DownloadResourceV3.cs#L82
                var packageUri = new Uri(_packageBaseAddress, $"{idString}/{versionString}/{idString}.{versionString}.nupkg");

                // TODO: DownloadAsync throws when the package doesn't exist. This could be cleaner.
                using (var stream = await _downloader.DownloadAsync(packageUri, CancellationToken.None))
                {
                    _logger.LogInformation(
                        "Downloaded package {Id} {Version}, indexing...",
                        idString,
                        versionString);

                    var indexingResult = await _indexer.IndexAsync(stream);

                    switch (indexingResult)
                    {
                        case IndexingResult.InvalidPackage:
                            _logger.LogWarning(
                                "Could not index {Id} {Version} as it is an invalid package",
                                idString,
                                versionString);

                            return false;

                        case IndexingResult.Success:
                        case IndexingResult.PackageAlreadyExists:
                            _logger.LogInformation(
                                "Successfully indexed {Id} {Version}",
                                idString,
                                versionString);

                            return true;

                        default:
                            _logger.LogError(
                                "Unknown indexing result for {Id} {Version}: {IndexingResult}",
                                idString,
                                versionString,
                                indexingResult);

                            throw new InvalidOperationException($"Unknown indexing result: {indexingResult}");
                    }
                }
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Failed to index package {Id} {Version}", idString, versionString);

                return false;
            }
        }
    }
}
