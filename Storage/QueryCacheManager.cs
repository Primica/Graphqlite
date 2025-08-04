using GraphQLite.Query;
using GraphQLite.Engine;
using System.Collections.Concurrent;

namespace GraphQLite.Storage;

/// <summary>
/// Gestionnaire de cache pour les résultats de requêtes fréquentes
/// </summary>
public class QueryCacheManager
{
    // Cache principal avec expiration
    private readonly ConcurrentDictionary<string, CacheEntry> _cache;
    
    // Statistiques d'utilisation du cache
    private readonly ConcurrentDictionary<string, int> _cacheHitStats;
    private readonly ConcurrentDictionary<string, int> _cacheMissStats;
    
    // Configuration du cache intelligent
    private readonly TimeSpan _defaultExpiration = TimeSpan.FromMinutes(10);
    private readonly int _maxCacheSize = 2000;
    private readonly object _cleanupLock = new();
    
    // Statistiques pour l'optimisation automatique
    private readonly ConcurrentDictionary<string, int> _queryFrequency = new();
    private readonly ConcurrentDictionary<string, DateTime> _lastQueryTime = new();

    public QueryCacheManager()
    {
        _cache = new ConcurrentDictionary<string, CacheEntry>();
        _cacheHitStats = new ConcurrentDictionary<string, int>();
        _cacheMissStats = new ConcurrentDictionary<string, int>();
        
        // Démarrer le nettoyage automatique
        StartCleanupTask();
    }

    /// <summary>
    /// Génère une clé de cache basée sur la requête parsée
    /// </summary>
    public string GenerateCacheKey(ParsedQuery query)
    {
        var keyParts = new List<string>
        {
            query.Type.ToString(),
            query.NodeLabel ?? "",
            query.EdgeType ?? "",
            query.FromNode ?? "",
            query.ToNode ?? ""
        };

        // Ajouter les propriétés triées
        if (query.Properties.Any())
        {
            var sortedProps = query.Properties.OrderBy(kvp => kvp.Key)
                .Select(kvp => $"{kvp.Key}:{kvp.Value}");
            keyParts.Add(string.Join("|", sortedProps));
        }

        // Ajouter les conditions triées
        if (query.Conditions.Any())
        {
            var sortedConditions = query.Conditions.OrderBy(kvp => kvp.Key)
                .Select(kvp => $"{kvp.Key}:{kvp.Value}");
            keyParts.Add(string.Join("|", sortedConditions));
        }

        // Ajouter les paramètres de pagination
        if (query.Limit.HasValue) keyParts.Add($"limit:{query.Limit}");
        if (query.Offset.HasValue) keyParts.Add($"offset:{query.Offset}");

        // Ajouter les paramètres de groupement et tri
        if (query.GroupByProperties.Any())
        {
            keyParts.Add($"groupby:{string.Join(",", query.GroupByProperties)}");
        }

        if (query.OrderByClauses.Any())
        {
            var orderByStr = string.Join(",", query.OrderByClauses.Select(o => $"{o.Property}:{o.Direction}"));
            keyParts.Add($"orderby:{orderByStr}");
        }

        return string.Join("::", keyParts);
    }

    /// <summary>
    /// Tente de récupérer un résultat du cache
    /// </summary>
    public bool TryGetCachedResult(string cacheKey, out QueryResult? result)
    {
        result = null;

        if (_cache.TryGetValue(cacheKey, out var entry))
        {
            // Vérifier si l'entrée n'est pas expirée
            if (DateTime.UtcNow < entry.ExpirationTime)
            {
                // Mettre à jour les statistiques de hit
                _cacheHitStats.AddOrUpdate(cacheKey, 1, (_, count) => count + 1);
                
                // Mettre à jour le temps d'accès
                entry.LastAccessed = DateTime.UtcNow;
                result = entry.Result;
                return true;
            }
            else
            {
                // Supprimer l'entrée expirée
                _cache.TryRemove(cacheKey, out _);
            }
        }

        // Mettre à jour les statistiques de miss
        _cacheMissStats.AddOrUpdate(cacheKey, 1, (_, count) => count + 1);
        return false;
    }

    /// <summary>
    /// Stocke un résultat dans le cache avec expiration intelligente
    /// </summary>
    public void CacheResult(string cacheKey, QueryResult result, TimeSpan? expiration = null)
    {
        // Calculer l'expiration intelligente basée sur la fréquence d'utilisation
        var intelligentExpiration = CalculateIntelligentExpiration(cacheKey);
        var expirationTime = DateTime.UtcNow.Add(expiration ?? intelligentExpiration);
        
        var entry = new CacheEntry
        {
            Result = result,
            CreatedTime = DateTime.UtcNow,
            LastAccessed = DateTime.UtcNow,
            ExpirationTime = expirationTime,
            AccessCount = 1
        };

        // Mettre à jour les statistiques de fréquence
        _queryFrequency.AddOrUpdate(cacheKey, 1, (_, count) => count + 1);
        _lastQueryTime.AddOrUpdate(cacheKey, DateTime.UtcNow, (_, _) => DateTime.UtcNow);

        // Gérer la taille maximale du cache de manière intelligente
        if (_cache.Count >= _maxCacheSize)
        {
            CleanupIntelligentEntries();
        }

        _cache.AddOrUpdate(cacheKey, entry, (_, existing) =>
        {
            existing.Result = result;
            existing.CreatedTime = DateTime.UtcNow;
            existing.LastAccessed = DateTime.UtcNow;
            existing.ExpirationTime = expirationTime;
            existing.AccessCount++;
            return existing;
        });
    }

    /// <summary>
    /// Invalide le cache pour les opérations de modification
    /// </summary>
    public void InvalidateCacheForModification(QueryType queryType, string? nodeLabel = null)
    {
        var keysToRemove = new List<string>();

        foreach (var kvp in _cache)
        {
            var key = kvp.Key;
            var entry = kvp.Value;

            // Invalider selon le type d'opération
            bool shouldInvalidate = queryType switch
            {
                QueryType.CreateNode => ShouldInvalidateForNodeCreation(key, nodeLabel),
                QueryType.UpdateNode => ShouldInvalidateForNodeUpdate(key, nodeLabel),
                QueryType.DeleteNode => ShouldInvalidateForNodeDeletion(key, nodeLabel),
                QueryType.CreateEdge => ShouldInvalidateForEdgeCreation(key),
                QueryType.DeleteEdge => ShouldInvalidateForEdgeDeletion(key),
                _ => false
            };

            if (shouldInvalidate)
            {
                keysToRemove.Add(key);
            }
        }

        // Supprimer les clés invalidées
        foreach (var key in keysToRemove)
        {
            _cache.TryRemove(key, out _);
        }
    }

    /// <summary>
    /// Vide complètement le cache
    /// </summary>
    public void ClearCache()
    {
        _cache.Clear();
    }

    /// <summary>
    /// Obtient les statistiques du cache
    /// </summary>
    public CacheStats GetCacheStats()
    {
        var stats = new CacheStats
        {
            TotalEntries = _cache.Count,
            HitStats = new Dictionary<string, int>(_cacheHitStats),
            MissStats = new Dictionary<string, int>(_cacheMissStats),
            TotalHits = _cacheHitStats.Values.Sum(),
            TotalMisses = _cacheMissStats.Values.Sum()
        };

        if (stats.TotalHits + stats.TotalMisses > 0)
        {
            stats.HitRate = (double)stats.TotalHits / (stats.TotalHits + stats.TotalMisses);
        }

        return stats;
    }

    /// <summary>
    /// Obtient les entrées du cache les plus utilisées
    /// </summary>
    public List<CacheEntryInfo> GetTopCacheEntries(int count = 10)
    {
        return _cache
            .Where(kvp => DateTime.UtcNow < kvp.Value.ExpirationTime)
            .OrderByDescending(kvp => kvp.Value.AccessCount)
            .Take(count)
            .Select(kvp => new CacheEntryInfo
            {
                CacheKey = kvp.Key,
                AccessCount = kvp.Value.AccessCount,
                CreatedTime = kvp.Value.CreatedTime,
                LastAccessed = kvp.Value.LastAccessed,
                ExpirationTime = kvp.Value.ExpirationTime
            })
            .ToList();
    }

    private bool ShouldInvalidateForNodeCreation(string cacheKey, string? nodeLabel)
    {
        // Invalider les requêtes qui pourraient être affectées par la création d'un nœud
        return string.IsNullOrEmpty(nodeLabel) || 
               cacheKey.Contains($"::{nodeLabel}::") ||
               cacheKey.Contains("find all") ||
               cacheKey.Contains("count");
    }

    private bool ShouldInvalidateForNodeUpdate(string cacheKey, string? nodeLabel)
    {
        // Invalider les requêtes qui pourraient être affectées par la mise à jour d'un nœud
        return string.IsNullOrEmpty(nodeLabel) || 
               cacheKey.Contains($"::{nodeLabel}::") ||
               cacheKey.Contains("find all") ||
               cacheKey.Contains("count") ||
               cacheKey.Contains("aggregate");
    }

    private bool ShouldInvalidateForNodeDeletion(string cacheKey, string? nodeLabel)
    {
        // Invalider les requêtes qui pourraient être affectées par la suppression d'un nœud
        return string.IsNullOrEmpty(nodeLabel) || 
               cacheKey.Contains($"::{nodeLabel}::") ||
               cacheKey.Contains("find all") ||
               cacheKey.Contains("count") ||
               cacheKey.Contains("aggregate");
    }

    private bool ShouldInvalidateForEdgeCreation(string cacheKey)
    {
        // Invalider les requêtes d'arêtes et de chemins
        return cacheKey.Contains("edge") ||
               cacheKey.Contains("path") ||
               cacheKey.Contains("find edges");
    }

    private bool ShouldInvalidateForEdgeDeletion(string cacheKey)
    {
        // Invalider les requêtes d'arêtes et de chemins
        return cacheKey.Contains("edge") ||
               cacheKey.Contains("path") ||
               cacheKey.Contains("find edges");
    }

    /// <summary>
    /// Calcule une expiration intelligente basée sur la fréquence d'utilisation
    /// </summary>
    private TimeSpan CalculateIntelligentExpiration(string cacheKey)
    {
        if (_queryFrequency.TryGetValue(cacheKey, out var frequency))
        {
            // Plus la requête est fréquente, plus elle reste en cache longtemps
            if (frequency > 10)
                return TimeSpan.FromMinutes(30); // Très fréquente
            else if (frequency > 5)
                return TimeSpan.FromMinutes(20); // Fréquente
            else if (frequency > 2)
                return TimeSpan.FromMinutes(15); // Moyennement fréquente
            else
                return TimeSpan.FromMinutes(10); // Peu fréquente
        }
        
        return _defaultExpiration;
    }

    /// <summary>
    /// Nettoyage intelligent des entrées du cache
    /// </summary>
    private void CleanupIntelligentEntries()
    {
        lock (_cleanupLock)
        {
            // Supprimer les entrées expirées
            var expiredKeys = _cache
                .Where(kvp => DateTime.UtcNow >= kvp.Value.ExpirationTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var key in expiredKeys)
            {
                _cache.TryRemove(key, out _);
            }

            // Si le cache est encore trop plein, supprimer intelligemment
            if (_cache.Count >= _maxCacheSize)
            {
                // Calculer un score pour chaque entrée basé sur l'utilisation et la récence
                var entriesWithScore = _cache.Select(kvp => new
                {
                    Key = kvp.Key,
                    Entry = kvp.Value,
                    Score = CalculateEntryScore(kvp.Key, kvp.Value)
                }).OrderBy(x => x.Score).ToList();

                // Supprimer les 25% les moins performantes
                var keysToRemove = entriesWithScore
                    .Take(_cache.Count / 4)
                    .Select(x => x.Key)
                    .ToList();

                foreach (var key in keysToRemove)
                {
                    _cache.TryRemove(key, out _);
                }
            }
        }
    }

    /// <summary>
    /// Calcule un score pour une entrée du cache
    /// </summary>
    private double CalculateEntryScore(string cacheKey, CacheEntry entry)
    {
        var frequency = _queryFrequency.GetValueOrDefault(cacheKey, 1);
        var timeSinceLastAccess = DateTime.UtcNow - entry.LastAccessed;
        var timeSinceCreation = DateTime.UtcNow - entry.CreatedTime;
        
        // Score basé sur la fréquence d'accès, la récence et l'âge
        var frequencyScore = Math.Log(frequency + 1) * 10;
        var recencyScore = Math.Max(0, 100 - timeSinceLastAccess.TotalMinutes);
        var agePenalty = Math.Min(50, timeSinceCreation.TotalMinutes);
        
        return frequencyScore + recencyScore - agePenalty;
    }

    private void CleanupOldEntries()
    {
        CleanupIntelligentEntries();
    }

    private void StartCleanupTask()
    {
        Task.Run(async () =>
        {
            while (true)
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMinutes(1));
                    CleanupOldEntries();
                }
                catch (Exception ex)
                {
                    // Log l'erreur mais continue le nettoyage
                    Console.WriteLine($"Erreur lors du nettoyage du cache : {ex.Message}");
                }
            }
        });
    }
}

/// <summary>
/// Entrée du cache avec métadonnées
/// </summary>
public class CacheEntry
{
    public QueryResult Result { get; set; } = new();
    public DateTime CreatedTime { get; set; }
    public DateTime LastAccessed { get; set; }
    public DateTime ExpirationTime { get; set; }
    public int AccessCount { get; set; }
}

/// <summary>
/// Statistiques du cache
/// </summary>
public class CacheStats
{
    public int TotalEntries { get; set; }
    public Dictionary<string, int> HitStats { get; set; } = new();
    public Dictionary<string, int> MissStats { get; set; } = new();
    public int TotalHits { get; set; }
    public int TotalMisses { get; set; }
    public double HitRate { get; set; }
}

/// <summary>
/// Informations sur une entrée du cache
/// </summary>
public class CacheEntryInfo
{
    public string CacheKey { get; set; } = "";
    public int AccessCount { get; set; }
    public DateTime CreatedTime { get; set; }
    public DateTime LastAccessed { get; set; }
    public DateTime ExpirationTime { get; set; }
} 