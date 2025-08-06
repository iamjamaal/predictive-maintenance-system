using Microsoft.Extensions.Caching.Memory;
using System.Text.Json;
using System.Collections.Concurrent;

namespace FEENALOoFINALE.Services
{
    public interface ICacheService
    {
        Task<T?> GetAsync<T>(string key) where T : class;
        Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class;
        Task RemoveAsync(string key);
        Task RemoveByPatternAsync(string pattern);
        Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getItem, TimeSpan? expiration = null) where T : class;
        Task ClearAllAsync();
    }

    public class MemoryCacheService : ICacheService
    {
        private readonly IMemoryCache _cache;
        private readonly ILogger<MemoryCacheService> _logger;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> _locks = new();

        public MemoryCacheService(IMemoryCache cache, ILogger<MemoryCacheService> logger)
        {
            _cache = cache;
            _logger = logger;
        }

        public async Task<T?> GetAsync<T>(string key) where T : class
        {
            try
            {
                return await Task.FromResult(_cache.Get<T>(key));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving cache key: {Key}", key);
                return null;
            }
        }

        public async Task SetAsync<T>(string key, T value, TimeSpan? expiration = null) where T : class
        {
            try
            {
                var options = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = expiration ?? TimeSpan.FromMinutes(30),
                    SlidingExpiration = TimeSpan.FromMinutes(5),
                    Priority = CacheItemPriority.Normal
                };

                _cache.Set(key, value, options);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error setting cache key: {Key}", key);
            }
        }

        public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> getItem, TimeSpan? expiration = null) where T : class
        {
            var cachedValue = await GetAsync<T>(key);
            if (cachedValue != null)
                return cachedValue;

            var semaphore = _locks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
            await semaphore.WaitAsync();

            try
            {
                // Double-check after acquiring lock
                cachedValue = await GetAsync<T>(key);
                if (cachedValue != null)
                    return cachedValue;

                var value = await getItem();
                await SetAsync(key, value, expiration);
                return value;
            }
            finally
            {
                semaphore.Release();
            }
        }

        public async Task RemoveAsync(string key)
        {
            try
            {
                _cache.Remove(key);
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error removing cache key: {Key}", key);
            }
        }

        public async Task RemoveByPatternAsync(string pattern)
        {
            // Implementation for pattern-based cache removal
            await Task.CompletedTask;
        }

        public async Task ClearAllAsync()
        {
            if (_cache is MemoryCache memCache)
            {
                memCache.Compact(1.0);
            }
            await Task.CompletedTask;
        }
    }
}
