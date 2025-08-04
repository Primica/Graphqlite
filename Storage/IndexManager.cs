using GraphQLite.Models;
using System.Collections.Concurrent;

namespace GraphQLite.Storage;

/// <summary>
/// Gestionnaire d'indexation pour accélérer les requêtes sur les propriétés fréquemment utilisées
/// </summary>
public class IndexManager
{
    // Index par label et propriété
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<object, HashSet<Guid>>>> _indexes;
    
    // Statistiques d'utilisation des propriétés
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, int>> _propertyUsageStats;
    
    // Configuration des propriétés à indexer automatiquement
    private readonly HashSet<string> _autoIndexProperties = new()
    {
        "name", "department", "role", "salary", "age", "industry", "status", "location", "city"
    };

    public IndexManager()
    {
        _indexes = new ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<object, HashSet<Guid>>>>();
        _propertyUsageStats = new ConcurrentDictionary<string, ConcurrentDictionary<string, int>>();
    }

    /// <summary>
    /// Indexe un nœud avec toutes ses propriétés
    /// </summary>
    public void IndexNode(Node node)
    {
        foreach (var property in node.Properties)
        {
            // Indexer automatiquement les propriétés fréquemment utilisées
            if (_autoIndexProperties.Contains(property.Key))
            {
                AddToIndex(node.Label, property.Key, property.Value, node.Id);
            }
            
            // Mettre à jour les statistiques d'utilisation
            UpdatePropertyUsageStats(node.Label, property.Key);
        }
    }

    /// <summary>
    /// Supprime un nœud de tous les index
    /// </summary>
    public void RemoveNodeFromIndexes(Node node)
    {
        foreach (var property in node.Properties)
        {
            if (_autoIndexProperties.Contains(property.Key))
            {
                RemoveFromIndex(node.Label, property.Key, property.Value, node.Id);
            }
        }
    }

    /// <summary>
    /// Met à jour l'index pour un nœud modifié
    /// </summary>
    public void UpdateNodeIndex(Node node, Dictionary<string, object> oldProperties)
    {
        // Supprimer les anciennes valeurs des index
        foreach (var oldProperty in oldProperties)
        {
            if (_autoIndexProperties.Contains(oldProperty.Key))
            {
                RemoveFromIndex(node.Label, oldProperty.Key, oldProperty.Value, node.Id);
            }
        }

        // Ajouter les nouvelles valeurs aux index
        foreach (var newProperty in node.Properties)
        {
            if (_autoIndexProperties.Contains(newProperty.Key))
            {
                AddToIndex(node.Label, newProperty.Key, newProperty.Value, node.Id);
            }
        }
    }

    /// <summary>
    /// Recherche des nœuds par propriété indexée
    /// </summary>
    public HashSet<Guid> FindNodesByProperty(string label, string propertyName, object value)
    {
        if (_indexes.TryGetValue(label, out var labelIndex) &&
            labelIndex.TryGetValue(propertyName, out var propertyIndex) &&
            propertyIndex.TryGetValue(value, out var nodeIds))
        {
            return new HashSet<Guid>(nodeIds);
        }
        
        return new HashSet<Guid>();
    }

    /// <summary>
    /// Recherche des nœuds par propriété avec opérateurs de comparaison
    /// </summary>
    public HashSet<Guid> FindNodesByPropertyRange(string label, string propertyName, object minValue, object maxValue)
    {
        var result = new HashSet<Guid>();
        
        if (_indexes.TryGetValue(label, out var labelIndex) &&
            labelIndex.TryGetValue(propertyName, out var propertyIndex))
        {
            foreach (var kvp in propertyIndex)
            {
                var value = kvp.Key;
                if (IsInRange(value, minValue, maxValue))
                {
                    result.UnionWith(kvp.Value);
                }
            }
        }
        
        return result;
    }

    /// <summary>
    /// Obtient les statistiques d'utilisation des propriétés
    /// </summary>
    public Dictionary<string, Dictionary<string, int>> GetPropertyUsageStats()
    {
        var result = new Dictionary<string, Dictionary<string, int>>();
        
        foreach (var labelStats in _propertyUsageStats)
        {
            result[labelStats.Key] = new Dictionary<string, int>(labelStats.Value);
        }
        
        return result;
    }

    /// <summary>
    /// Ajoute une propriété à l'index automatique
    /// </summary>
    public void AddAutoIndexProperty(string propertyName)
    {
        _autoIndexProperties.Add(propertyName);
    }

    /// <summary>
    /// Supprime une propriété de l'index automatique
    /// </summary>
    public void RemoveAutoIndexProperty(string propertyName)
    {
        _autoIndexProperties.Remove(propertyName);
    }

    /// <summary>
    /// Obtient la liste des propriétés indexées automatiquement
    /// </summary>
    public HashSet<string> GetAutoIndexProperties()
    {
        return new HashSet<string>(_autoIndexProperties);
    }

    /// <summary>
    /// Reconstruit tous les index (utile après chargement de données)
    /// </summary>
    public void RebuildIndexes(IEnumerable<Node> nodes)
    {
        // Vider tous les index existants
        _indexes.Clear();
        
        // Reconstruire les index
        foreach (var node in nodes)
        {
            IndexNode(node);
        }
    }

    /// <summary>
    /// Obtient des statistiques sur les index
    /// </summary>
    public IndexStats GetIndexStats()
    {
        var stats = new IndexStats
        {
            TotalIndexes = _indexes.Count,
            AutoIndexProperties = _autoIndexProperties.Count,
            PropertyUsageStats = GetPropertyUsageStats()
        };

        foreach (var labelIndex in _indexes)
        {
            stats.LabelStats[labelIndex.Key] = new LabelIndexStats
            {
                IndexedProperties = labelIndex.Value.Count,
                TotalIndexedNodes = labelIndex.Value.Values.Sum(propIndex => propIndex.Values.Sum(nodeIds => nodeIds.Count))
            };
        }

        return stats;
    }

    private void AddToIndex(string label, string propertyName, object value, Guid nodeId)
    {
        var labelIndex = _indexes.GetOrAdd(label, _ => new ConcurrentDictionary<string, ConcurrentDictionary<object, HashSet<Guid>>>());
        var propertyIndex = labelIndex.GetOrAdd(propertyName, _ => new ConcurrentDictionary<object, HashSet<Guid>>());
        var nodeIds = propertyIndex.GetOrAdd(value, _ => new HashSet<Guid>());
        
        lock (nodeIds)
        {
            nodeIds.Add(nodeId);
        }
    }

    private void RemoveFromIndex(string label, string propertyName, object value, Guid nodeId)
    {
        if (_indexes.TryGetValue(label, out var labelIndex) &&
            labelIndex.TryGetValue(propertyName, out var propertyIndex) &&
            propertyIndex.TryGetValue(value, out var nodeIds))
        {
            lock (nodeIds)
            {
                nodeIds.Remove(nodeId);
                
                // Supprimer l'entrée si elle est vide
                if (nodeIds.Count == 0)
                {
                    propertyIndex.TryRemove(value, out _);
                }
            }
        }
    }

    private void UpdatePropertyUsageStats(string label, string propertyName)
    {
        var labelStats = _propertyUsageStats.GetOrAdd(label, _ => new ConcurrentDictionary<string, int>());
        labelStats.AddOrUpdate(propertyName, 1, (_, count) => count + 1);
    }

    private bool IsInRange(object value, object minValue, object maxValue)
    {
        if (value is IComparable comparable)
        {
            var minComparable = minValue as IComparable;
            var maxComparable = maxValue as IComparable;
            
            if (minComparable != null && maxComparable != null)
            {
                return comparable.CompareTo(minValue) >= 0 && comparable.CompareTo(maxValue) <= 0;
            }
        }
        
        return false;
    }
}

/// <summary>
/// Statistiques des index
/// </summary>
public class IndexStats
{
    public int TotalIndexes { get; set; }
    public int AutoIndexProperties { get; set; }
    public Dictionary<string, LabelIndexStats> LabelStats { get; set; } = new();
    public Dictionary<string, Dictionary<string, int>> PropertyUsageStats { get; set; } = new();
}

/// <summary>
/// Statistiques d'index par label
/// </summary>
public class LabelIndexStats
{
    public int IndexedProperties { get; set; }
    public int TotalIndexedNodes { get; set; }
} 