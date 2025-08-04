using System.Text.Json;
using GraphQLite.Models;
using GraphQLite.Query;

namespace GraphQLite.Storage;

/// <summary>
/// Curseur pour la pagination intelligente
/// </summary>
public class PaginationCursor
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "node" ou "edge"
    public Dictionary<string, object> SortValues { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int PageSize { get; set; }
    public string? FilterHash { get; set; } // Pour identifier les filtres appliqués
    
    /// <summary>
    /// Encode le curseur en base64 pour l'URL
    /// </summary>
    public string Encode()
    {
        var json = JsonSerializer.Serialize(this);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        return Convert.ToBase64String(bytes);
    }
    
    /// <summary>
    /// Décode un curseur depuis une chaîne base64
    /// </summary>
    public static PaginationCursor? Decode(string encodedCursor)
    {
        try
        {
            var bytes = Convert.FromBase64String(encodedCursor);
            var json = System.Text.Encoding.UTF8.GetString(bytes);
            return JsonSerializer.Deserialize<PaginationCursor>(json);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>
/// Résultat de pagination avec curseurs
/// </summary>
public class PaginationResult<T>
{
    public List<T> Items { get; set; } = new();
    public string? NextCursor { get; set; }
    public string? PreviousCursor { get; set; }
    public bool HasNext { get; set; }
    public bool HasPrevious { get; set; }
    public int TotalCount { get; set; }
    public int PageSize { get; set; }
    public int CurrentPage { get; set; }
    public int TotalPages { get; set; }
    
    /// <summary>
    /// Crée un résultat de pagination vide
    /// </summary>
    public static PaginationResult<T> Empty(int pageSize = 10)
    {
        return new PaginationResult<T>
        {
            PageSize = pageSize,
            TotalPages = 0,
            CurrentPage = 1
        };
    }
}

/// <summary>
/// Gestionnaire de pagination intelligente avec curseurs
/// </summary>
public class IntelligentPagination
{
    private readonly GraphStorage _storage;
    private readonly QueryCacheManager _cacheManager;
    
    public IntelligentPagination(GraphStorage storage, QueryCacheManager cacheManager)
    {
        _storage = storage;
        _cacheManager = cacheManager;
    }
    
    /// <summary>
    /// Pagine des nœuds avec curseurs intelligents
    /// </summary>
    public async Task<PaginationResult<Node>> PaginateNodesAsync(
        string? nodeLabel = null,
        Dictionary<string, object>? conditions = null,
        List<OrderByClause>? orderBy = null,
        int pageSize = 10,
        string? cursor = null,
        bool forward = true)
    {
        try
        {
            // Récupérer tous les nœuds
            var normalizedLabel = nodeLabel?.ToLowerInvariant();
            var allNodes = normalizedLabel != null 
                ? _storage.GetNodesByLabel(normalizedLabel)
                : _storage.GetAllNodes();
            
            Console.WriteLine($"DEBUG: PaginateNodesAsync - Found {allNodes.Count} nodes with label '{nodeLabel}'");
            
            // Appliquer les filtres
            if (conditions?.Any() == true)
            {
                allNodes = await FilterNodesByConditionsAsync(allNodes, conditions);
                Console.WriteLine($"DEBUG: After filtering: {allNodes.Count} nodes");
            }
            
            // Appliquer le tri
            if (orderBy?.Any() == true)
            {
                allNodes = SortNodes(allNodes, orderBy);
            }
            
            // Déterminer la position de départ
            var startIndex = 0;
            if (!string.IsNullOrEmpty(cursor))
            {
                var decodedCursor = PaginationCursor.Decode(cursor);
                if (decodedCursor != null)
                {
                    startIndex = FindNodeIndexByCursor(allNodes, decodedCursor, forward);
                }
            }
            
            // Paginer les résultats
            var items = allNodes
                .Skip(startIndex)
                .Take(pageSize + 1) // +1 pour déterminer s'il y a une page suivante
                .ToList();
            
            var hasNext = items.Count > pageSize;
            if (hasNext)
            {
                items.RemoveAt(items.Count - 1); // Retirer l'élément supplémentaire
            }
            
            Console.WriteLine($"DEBUG: Pagination result: {items.Count} items, hasNext: {hasNext}");
            
            // Créer les curseurs
            var nextCursor = hasNext && items.Any() ? CreateCursorForNode(items.Last(), "node", pageSize, conditions) : null;
            var previousCursor = startIndex > 0 && allNodes.Count > startIndex - 1 ? CreateCursorForNode(allNodes[startIndex - 1], "node", pageSize, conditions) : null;
            
            return new PaginationResult<Node>
            {
                Items = items,
                NextCursor = nextCursor?.Encode(),
                PreviousCursor = previousCursor?.Encode(),
                HasNext = hasNext,
                HasPrevious = startIndex > 0,
                TotalCount = allNodes.Count,
                PageSize = pageSize,
                CurrentPage = (startIndex / pageSize) + 1,
                TotalPages = (int)Math.Ceiling((double)allNodes.Count / pageSize)
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"DEBUG: Error in PaginateNodesAsync: {ex.Message}");
            // En cas d'erreur, retourner un résultat vide
            return PaginationResult<Node>.Empty(pageSize);
        }
    }
    
    /// <summary>
    /// Pagine des arêtes avec curseurs intelligents
    /// </summary>
    public async Task<PaginationResult<Edge>> PaginateEdgesAsync(
        string? edgeType = null,
        Dictionary<string, object>? conditions = null,
        List<OrderByClause>? orderBy = null,
        int pageSize = 10,
        string? cursor = null,
        bool forward = true)
    {
        try
        {
            // Récupérer toutes les arêtes
            var allEdges = _storage.GetAllEdges();
            
            // Filtrer par type si spécifié
            if (!string.IsNullOrEmpty(edgeType))
            {
                allEdges = allEdges.Where(e => e.RelationType == edgeType).ToList();
            }
            
            // Appliquer les filtres
            if (conditions?.Any() == true)
            {
                allEdges = FilterEdgesByConditions(allEdges, conditions);
            }
            
            // Appliquer le tri
            if (orderBy?.Any() == true)
            {
                allEdges = SortEdges(allEdges, orderBy);
            }
            
            // Déterminer la position de départ
            var startIndex = 0;
            if (!string.IsNullOrEmpty(cursor))
            {
                var decodedCursor = PaginationCursor.Decode(cursor);
                if (decodedCursor != null)
                {
                    startIndex = FindEdgeIndexByCursor(allEdges, decodedCursor, forward);
                }
            }
            
            // Paginer les résultats
            var items = allEdges
                .Skip(startIndex)
                .Take(pageSize + 1) // +1 pour déterminer s'il y a une page suivante
                .ToList();
            
            var hasNext = items.Count > pageSize;
            if (hasNext)
            {
                items.RemoveAt(items.Count - 1); // Retirer l'élément supplémentaire
            }
            
            // Créer les curseurs
            var nextCursor = hasNext ? CreateCursorForEdge(items.Last(), pageSize, conditions) : null;
            var previousCursor = startIndex > 0 ? CreateCursorForEdge(allEdges[startIndex - 1], pageSize, conditions) : null;
            
            return new PaginationResult<Edge>
            {
                Items = items,
                NextCursor = nextCursor?.Encode(),
                PreviousCursor = previousCursor?.Encode(),
                HasNext = hasNext,
                HasPrevious = startIndex > 0,
                TotalCount = allEdges.Count,
                PageSize = pageSize,
                CurrentPage = (startIndex / pageSize) + 1,
                TotalPages = (int)Math.Ceiling((double)allEdges.Count / pageSize)
            };
        }
        catch (Exception ex)
        {
            // En cas d'erreur, retourner un résultat vide
            return PaginationResult<Edge>.Empty(pageSize);
        }
    }
    
    /// <summary>
    /// Filtre les nœuds selon les conditions
    /// </summary>
    private async Task<List<Node>> FilterNodesByConditionsAsync(List<Node> nodes, Dictionary<string, object> conditions)
    {
        var filteredNodes = new List<Node>();
        
        foreach (var node in nodes)
        {
            var matches = true;
            
            foreach (var condition in conditions)
            {
                if (!node.Properties.TryGetValue(condition.Key, out var value))
                {
                    matches = false;
                    break;
                }
                
                // Gérer les opérateurs de comparaison dans la clé
                var conditionKey = condition.Key;
                var operator_ = "=";
                
                if (conditionKey.Contains(">"))
                {
                    operator_ = ">";
                    conditionKey = conditionKey.Replace(">", "");
                }
                else if (conditionKey.Contains("<"))
                {
                    operator_ = "<";
                    conditionKey = conditionKey.Replace("<", "");
                }
                else if (conditionKey.Contains(">="))
                {
                    operator_ = ">=";
                    conditionKey = conditionKey.Replace(">=", "");
                }
                else if (conditionKey.Contains("<="))
                {
                    operator_ = "<=";
                    conditionKey = conditionKey.Replace("<=", "");
                }
                
                if (!node.Properties.TryGetValue(conditionKey, out var actualValue))
                {
                    matches = false;
                    break;
                }
                
                if (!EvaluateConditionWithOperator(actualValue, condition.Value, operator_))
                {
                    matches = false;
                    break;
                }
            }
            
            if (matches)
            {
                filteredNodes.Add(node);
            }
        }
        
        return filteredNodes;
    }
    
    /// <summary>
    /// Filtre les arêtes selon les conditions
    /// </summary>
    private List<Edge> FilterEdgesByConditions(List<Edge> edges, Dictionary<string, object> conditions)
    {
        var filteredEdges = new List<Edge>();
        
        foreach (var edge in edges)
        {
            var matches = true;
            
            foreach (var condition in conditions)
            {
                if (!edge.Properties.TryGetValue(condition.Key, out var value))
                {
                    matches = false;
                    break;
                }
                
                if (!EvaluateCondition(value, condition.Value))
                {
                    matches = false;
                    break;
                }
            }
            
            if (matches)
            {
                filteredEdges.Add(edge);
            }
        }
        
        return filteredEdges;
    }
    
    /// <summary>
    /// Évalue une condition de comparaison
    /// </summary>
    private bool EvaluateCondition(object actual, object expected)
    {
        if (actual == null || expected == null)
            return actual == expected;
        
        if (actual.GetType() == expected.GetType())
            return actual.Equals(expected);
        
        // Tentative de conversion pour la comparaison numérique
        if (actual is IConvertible && expected is IConvertible)
        {
            try
            {
                var actualConverted = Convert.ToDouble(actual);
                var expectedConverted = Convert.ToDouble(expected);
                return actualConverted == expectedConverted;
            }
            catch
            {
                return false;
            }
        }
        
        // Comparaison de chaînes
        var actualString = actual.ToString()?.ToLowerInvariant();
        var expectedString = expected.ToString()?.ToLowerInvariant();
        
        return actualString == expectedString;
    }
    
    /// <summary>
    /// Évalue une condition avec un opérateur de comparaison
    /// </summary>
    private bool EvaluateConditionWithOperator(object actual, object expected, string operator_)
    {
        if (actual == null || expected == null)
            return false;
        
        // Tentative de conversion numérique pour les comparaisons
        if (actual is IConvertible && expected is IConvertible)
        {
            try
            {
                var actualConverted = Convert.ToDouble(actual);
                var expectedConverted = Convert.ToDouble(expected);
                
                return operator_ switch
                {
                    "=" => actualConverted == expectedConverted,
                    ">" => actualConverted > expectedConverted,
                    "<" => actualConverted < expectedConverted,
                    ">=" => actualConverted >= expectedConverted,
                    "<=" => actualConverted <= expectedConverted,
                    _ => actualConverted == expectedConverted
                };
            }
            catch
            {
                // Si la conversion échoue, utiliser la comparaison de chaînes
            }
        }
        
        // Comparaison de chaînes
        var actualString = actual.ToString()?.ToLowerInvariant();
        var expectedString = expected.ToString()?.ToLowerInvariant();
        
        return operator_ switch
        {
            "=" => actualString == expectedString,
            ">" => string.Compare(actualString, expectedString, StringComparison.OrdinalIgnoreCase) > 0,
            "<" => string.Compare(actualString, expectedString, StringComparison.OrdinalIgnoreCase) < 0,
            ">=" => string.Compare(actualString, expectedString, StringComparison.OrdinalIgnoreCase) >= 0,
            "<=" => string.Compare(actualString, expectedString, StringComparison.OrdinalIgnoreCase) <= 0,
            _ => actualString == expectedString
        };
    }
    
    /// <summary>
    /// Trie les nœuds selon les clauses de tri
    /// </summary>
    private List<Node> SortNodes(List<Node> nodes, List<OrderByClause> orderBy)
    {
        var sorted = nodes.AsEnumerable();
        
        foreach (var clause in orderBy)
        {
            sorted = clause.Direction == OrderDirection.Ascending
                ? sorted.OrderBy(n => GetNodePropertyValue(n, clause.Property))
                : sorted.OrderByDescending(n => GetNodePropertyValue(n, clause.Property));
        }
        
        return sorted.ToList();
    }
    
    /// <summary>
    /// Trie les arêtes selon les clauses de tri
    /// </summary>
    private List<Edge> SortEdges(List<Edge> edges, List<OrderByClause> orderBy)
    {
        var sorted = edges.AsEnumerable();
        
        foreach (var clause in orderBy)
        {
            sorted = clause.Direction == OrderDirection.Ascending
                ? sorted.OrderBy(e => GetEdgePropertyValue(e, clause.Property))
                : sorted.OrderByDescending(e => GetEdgePropertyValue(e, clause.Property));
        }
        
        return sorted.ToList();
    }
    
    /// <summary>
    /// Obtient la valeur d'une propriété de nœud pour le tri
    /// </summary>
    private object GetNodePropertyValue(Node node, string property)
    {
        return node.Properties.TryGetValue(property, out var value) ? value : string.Empty;
    }
    
    /// <summary>
    /// Obtient la valeur d'une propriété d'arête pour le tri
    /// </summary>
    private object GetEdgePropertyValue(Edge edge, string property)
    {
        return edge.Properties.TryGetValue(property, out var value) ? value : string.Empty;
    }
    
    /// <summary>
    /// Trouve l'index d'un nœud selon le curseur
    /// </summary>
    private int FindNodeIndexByCursor(List<Node> nodes, PaginationCursor cursor, bool forward)
    {
        for (int i = 0; i < nodes.Count; i++)
        {
            if (nodes[i].Id.ToString() == cursor.Id)
            {
                return forward ? i + 1 : i - 1;
            }
        }
        return 0;
    }
    
    /// <summary>
    /// Trouve l'index d'une arête selon le curseur
    /// </summary>
    private int FindEdgeIndexByCursor(List<Edge> edges, PaginationCursor cursor, bool forward)
    {
        for (int i = 0; i < edges.Count; i++)
        {
            if (edges[i].Id.ToString() == cursor.Id)
            {
                return forward ? i + 1 : i - 1;
            }
        }
        return 0;
    }
    
    /// <summary>
    /// Crée un curseur pour un nœud
    /// </summary>
    private PaginationCursor CreateCursorForNode(Node node, string type, int pageSize, Dictionary<string, object>? conditions)
    {
        return new PaginationCursor
        {
            Id = node.Id.ToString(),
            Type = type,
            PageSize = pageSize,
            FilterHash = conditions != null ? CreateFilterHash(conditions) : null,
            SortValues = node.Properties
        };
    }
    
    /// <summary>
    /// Crée un curseur pour une arête
    /// </summary>
    private PaginationCursor CreateCursorForEdge(Edge edge, int pageSize, Dictionary<string, object>? conditions)
    {
        return new PaginationCursor
        {
            Id = edge.Id.ToString(),
            Type = "edge",
            PageSize = pageSize,
            FilterHash = conditions != null ? CreateFilterHash(conditions) : null,
            SortValues = edge.Properties
        };
    }
    
    /// <summary>
    /// Crée un hash pour les filtres
    /// </summary>
    private string CreateFilterHash(Dictionary<string, object> conditions)
    {
        var json = JsonSerializer.Serialize(conditions.OrderBy(kvp => kvp.Key));
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json));
    }
}

// Utilise les classes existantes de GraphQLite.Query 