// Copyright © Christopher Dorst. All rights reserved.
// Licensed under the GNU General Public License, Version 3.0. See the LICENSE document in the repository root for license information.

using DevOps.Code.DataAccess.Options.CacheExpiration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ProtoBuf;
using System;
using System.IO;
using System.Threading.Tasks;

namespace DevOps.Code.DataAccess.Services.CacheService
{
    /// <summary>Generic data-access caching service for EntityFrameworkCore entities</summary>
    public class CacheService<TEntity> where TEntity : class
    {
        /// <summary>Reference to a distributed cache (Redis)</summary>
        private readonly IDistributedCache _cache;

        /// <summary>Options for the distributed cache (expiry, etc.)</summary>
        private readonly DistributedCacheEntryOptions _expiration;

        /// <summary>Logger</summary>
        private readonly ILogger<CacheService<TEntity>> _logger;

        /// <summary>Constructs an instance of the cache service</summary>
        public CacheService(IDistributedCache cache, ILogger<CacheService<TEntity>> logger, IOptions<CacheSlidingExpiration> options)
        {
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            var expiration = options?.Value ?? new CacheSlidingExpiration();
            _expiration = new DistributedCacheEntryOptions { SlidingExpiration = new TimeSpan(expiration.Days ?? 0, expiration.Hours ?? 0, expiration.Minutes ?? 0, expiration.Seconds ?? 0) };
        }

        /// <summary>Returns the entity given a key value</summary>
        public async Task<TEntity> FindAsync(string key)
        {
            _logger.LogInformation($"Find entry: {key}");
            var cacheEntry = await _cache.GetAsync(key);
            if (cacheEntry != null) return Deserialize(cacheEntry);
            _logger.LogInformation("Cache miss");
            return null;
        }

        /// <summary>Removes the entity at the given a key value from the cache</summary>
        public async Task RemoveAsync(string key)
        {
            _logger.LogInformation($"Removing entry: {key}");
            await _cache.RemoveAsync(key);
        }

        /// <summary>Saves the entity to the cache</summary>
        public async Task SaveAsync(string key, TEntity entity)
        {
            _logger.LogInformation($"Setting entry: {key}");
            var value = Serialize(entity);
            await _cache.SetAsync(key, value, _expiration);
        }

        /// <summary>De-serializes the cached entity</summary>
        private TEntity Deserialize(byte[] cacheEntry)
        {
            _logger.LogInformation("Cache hit");
            return Serializer.Deserialize<TEntity>(new MemoryStream(cacheEntry));
        }

        /// <summary>Serializes the entity</summary>
        private byte[] Serialize(TEntity entity)
        {
            _logger.LogInformation("ProtoBuf serializing record");
            using (var stream = new MemoryStream()){    Serializer.Serialize(stream, entity);
            return stream.ToArray();}};
    }
}
