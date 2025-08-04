using GraphQLite.Models;
using GraphQLite.Storage;
using System.Collections.Concurrent;

namespace GraphQLite.Engine;

/// <summary>
/// Moteur d'optimisation des algorithmes de graphes
/// Implémente des algorithmes avancés et des optimisations de performance
/// </summary>
public class GraphOptimizationEngine
{
    private readonly GraphStorage _storage;
    private readonly ConcurrentDictionary<string, object> _algorithmCache;
    private readonly ConcurrentDictionary<Guid, Dictionary<Guid, double>> _distanceCache;
    private readonly ConcurrentDictionary<Guid, Dictionary<Guid, List<Guid>>> _pathCache;
    private readonly object _cacheLock = new();

    // Métriques de performance
    public class PerformanceMetrics
    {
        public long TotalOperations { get; set; }
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public double AverageExecutionTime { get; set; }
        public double CacheHitRate { get; set; }
        public Dictionary<string, double> AlgorithmPerformance { get; set; } = new();
    }

    public PerformanceMetrics Metrics { get; private set; } = new();

    public GraphOptimizationEngine(GraphStorage storage)
    {
        _storage = storage;
        _algorithmCache = new ConcurrentDictionary<string, object>();
        _distanceCache = new ConcurrentDictionary<Guid, Dictionary<Guid, double>>();
        _pathCache = new ConcurrentDictionary<Guid, Dictionary<Guid, List<Guid>>>();
    }

    /// <summary>
    /// Algorithme Dijkstra optimisé pour trouver le chemin le plus court avec poids
    /// </summary>
    public List<Node> FindShortestPathDijkstra(Guid fromId, Guid toId, string? weightProperty = null)
    {
        var cacheKey = $"dijkstra_{fromId}_{toId}_{weightProperty ?? "default"}";
        
        if (_algorithmCache.TryGetValue(cacheKey, out var cachedResult))
        {
            Metrics.CacheHits++;
            return (List<Node>)cachedResult;
        }

        Metrics.CacheMisses++;
        var startTime = DateTime.UtcNow;

        var distances = new Dictionary<Guid, double>();
        var previous = new Dictionary<Guid, Guid?>();
        var unvisited = new HashSet<Guid>();
        var allNodes = _storage.GetAllNodes();

        // Initialisation
        foreach (var node in allNodes)
        {
            distances[node.Id] = double.MaxValue;
            unvisited.Add(node.Id);
        }
        distances[fromId] = 0;

        while (unvisited.Count > 0)
        {
            // Trouver le nœud non visité avec la distance minimale
            var currentId = unvisited.OrderBy(id => distances[id]).First();
            
            if (currentId == toId)
                break;

            unvisited.Remove(currentId);

            var edges = _storage.GetEdgesForNode(currentId);
            foreach (var edge in edges)
            {
                var neighborId = edge.GetOtherNode(currentId);
                if (!unvisited.Contains(neighborId))
                    continue;

                var weight = GetEdgeWeight(edge, weightProperty);
                var newDistance = distances[currentId] + weight;

                if (newDistance < distances[neighborId])
                {
                    distances[neighborId] = newDistance;
                    previous[neighborId] = currentId;
                }
            }
        }

        // Reconstruire le chemin
        var path = new List<Node>();
        var current = toId;
        
        while (current != Guid.Empty && previous.ContainsKey(current))
        {
            var node = _storage.GetNode(current);
            if (node != null)
                path.Insert(0, node);
            current = previous[current] ?? Guid.Empty;
        }

        // Ajouter le nœud de départ si nécessaire
        if (path.Count > 0 && path[0].Id != fromId)
        {
            var startNode = _storage.GetNode(fromId);
            if (startNode != null)
                path.Insert(0, startNode);
        }

        var dijkstraExecutionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        UpdateMetrics("Dijkstra", dijkstraExecutionTime);

        _algorithmCache.TryAdd(cacheKey, path);
        return path;
    }

    /// <summary>
    /// Algorithme A* pour recherche de chemin avec heuristique
    /// </summary>
    public List<Node> FindPathAStar(Guid fromId, Guid toId, string? weightProperty = null)
    {
        var cacheKey = $"astar_{fromId}_{toId}_{weightProperty ?? "default"}";
        
        if (_algorithmCache.TryGetValue(cacheKey, out var cachedResult))
        {
            Metrics.CacheHits++;
            return (List<Node>)cachedResult;
        }

        Metrics.CacheMisses++;
        var startTime = DateTime.UtcNow;

        var openSet = new PriorityQueue<(Guid nodeId, double fScore), double>();
        var cameFrom = new Dictionary<Guid, Guid?>();
        var gScore = new Dictionary<Guid, double>();
        var fScore = new Dictionary<Guid, double>();

        gScore[fromId] = 0;
        fScore[fromId] = HeuristicCost(fromId, toId);
        openSet.Enqueue((fromId, fScore[fromId]), fScore[fromId]);

        while (openSet.Count > 0)
        {
            var (currentId, _) = openSet.Dequeue();

            if (currentId == toId)
            {
                var path = ReconstructPath(cameFrom, currentId);
                var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
                UpdateMetrics("AStar", executionTime);
                _algorithmCache.TryAdd(cacheKey, path);
                return path;
            }

            var edges = _storage.GetEdgesForNode(currentId);
            foreach (var edge in edges)
            {
                var neighborId = edge.GetOtherNode(currentId);
                var tentativeGScore = gScore[currentId] + GetEdgeWeight(edge, weightProperty);

                if (!gScore.ContainsKey(neighborId) || tentativeGScore < gScore[neighborId])
                {
                    cameFrom[neighborId] = currentId;
                    gScore[neighborId] = tentativeGScore;
                    fScore[neighborId] = gScore[neighborId] + HeuristicCost(neighborId, toId);
                    openSet.Enqueue((neighborId, fScore[neighborId]), fScore[neighborId]);
                }
            }
        }

        var astarExecutionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        UpdateMetrics("AStar", astarExecutionTime);
        
        var emptyPath = new List<Node>();
        _algorithmCache.TryAdd(cacheKey, emptyPath);
        return emptyPath;
    }

    /// <summary>
    /// Algorithme Floyd-Warshall pour calculer toutes les distances entre nœuds
    /// </summary>
    public Dictionary<Guid, Dictionary<Guid, double>> ComputeAllPairsShortestPaths()
    {
        var cacheKey = "floyd_warshall_all_pairs";
        
        if (_algorithmCache.TryGetValue(cacheKey, out var cachedResult))
        {
            Metrics.CacheHits++;
            return (Dictionary<Guid, Dictionary<Guid, double>>)cachedResult;
        }

        Metrics.CacheMisses++;
        var startTime = DateTime.UtcNow;

        var allNodes = _storage.GetAllNodes();
        var distances = new Dictionary<Guid, Dictionary<Guid, double>>();

        // Initialisation
        foreach (var node in allNodes)
        {
            distances[node.Id] = new Dictionary<Guid, double>();
            foreach (var otherNode in allNodes)
            {
                distances[node.Id][otherNode.Id] = node.Id == otherNode.Id ? 0 : double.MaxValue;
            }
        }

        // Initialiser avec les arêtes existantes
        var allEdges = _storage.GetAllEdges();
        foreach (var edge in allEdges)
        {
            var weight = GetEdgeWeight(edge, null);
            distances[edge.FromNodeId][edge.ToNodeId] = weight;
            distances[edge.ToNodeId][edge.FromNodeId] = weight; // Graphe non dirigé
        }

        // Algorithme Floyd-Warshall
        foreach (var k in allNodes)
        {
            foreach (var i in allNodes)
            {
                foreach (var j in allNodes)
                {
                    if (distances[i.Id][k.Id] != double.MaxValue && 
                        distances[k.Id][j.Id] != double.MaxValue)
                    {
                        var newDistance = distances[i.Id][k.Id] + distances[k.Id][j.Id];
                        if (newDistance < distances[i.Id][j.Id])
                        {
                            distances[i.Id][j.Id] = newDistance;
                        }
                    }
                }
            }
        }

        var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        UpdateMetrics("FloydWarshall", executionTime);

        _algorithmCache.TryAdd(cacheKey, distances);
        return distances;
    }

    /// <summary>
    /// Recherche de composantes connexes optimisée
    /// </summary>
    public List<List<Node>> FindConnectedComponents()
    {
        var cacheKey = "connected_components";
        
        if (_algorithmCache.TryGetValue(cacheKey, out var cachedResult))
        {
            Metrics.CacheHits++;
            return (List<List<Node>>)cachedResult;
        }

        Metrics.CacheMisses++;
        var startTime = DateTime.UtcNow;

        var allNodes = _storage.GetAllNodes();
        var visited = new HashSet<Guid>();
        var components = new List<List<Node>>();

        foreach (var node in allNodes)
        {
            if (!visited.Contains(node.Id))
            {
                var component = new List<Node>();
                DFS(node.Id, visited, component);
                components.Add(component);
            }
        }

        var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        UpdateMetrics("ConnectedComponents", executionTime);

        _algorithmCache.TryAdd(cacheKey, components);
        return components;
    }

    /// <summary>
    /// Détection de cycles dans le graphe
    /// </summary>
    public List<List<Node>> DetectCycles()
    {
        var cacheKey = "cycle_detection";
        
        if (_algorithmCache.TryGetValue(cacheKey, out var cachedResult))
        {
            Metrics.CacheHits++;
            return (List<List<Node>>)cachedResult;
        }

        Metrics.CacheMisses++;
        var startTime = DateTime.UtcNow;

        var allNodes = _storage.GetAllNodes();
        var visited = new HashSet<Guid>();
        var recStack = new HashSet<Guid>();
        var cycles = new List<List<Node>>();

        foreach (var node in allNodes)
        {
            if (!visited.Contains(node.Id))
            {
                var cycle = new List<Node>();
                if (DetectCycleDFS(node.Id, visited, recStack, cycle))
                {
                    cycles.Add(cycle);
                }
            }
        }

        var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        UpdateMetrics("CycleDetection", executionTime);

        _algorithmCache.TryAdd(cacheKey, cycles);
        return cycles;
    }

    /// <summary>
    /// Calcul du diamètre du graphe (distance maximale entre deux nœuds)
    /// </summary>
    public double CalculateGraphDiameter()
    {
        var cacheKey = "graph_diameter";
        
        if (_algorithmCache.TryGetValue(cacheKey, out var cachedResult))
        {
            Metrics.CacheHits++;
            return (double)cachedResult;
        }

        Metrics.CacheMisses++;
        var startTime = DateTime.UtcNow;

        var allPairs = ComputeAllPairsShortestPaths();
        var maxDistance = 0.0;

        foreach (var source in allPairs)
        {
            foreach (var target in source.Value)
            {
                if (target.Value != double.MaxValue && target.Value > maxDistance)
                {
                    maxDistance = target.Value;
                }
            }
        }

        var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        UpdateMetrics("GraphDiameter", executionTime);

        _algorithmCache.TryAdd(cacheKey, maxDistance);
        return maxDistance;
    }

    /// <summary>
    /// Calcul du rayon du graphe (distance minimale maximale depuis un nœud)
    /// </summary>
    public double CalculateGraphRadius()
    {
        var cacheKey = "graph_radius";
        
        if (_algorithmCache.TryGetValue(cacheKey, out var cachedResult))
        {
            Metrics.CacheHits++;
            return (double)cachedResult;
        }

        Metrics.CacheMisses++;
        var startTime = DateTime.UtcNow;

        var allPairs = ComputeAllPairsShortestPaths();
        var minMaxDistance = double.MaxValue;

        foreach (var source in allPairs)
        {
            var maxDistanceFromSource = source.Value.Values.Where(d => d != double.MaxValue).Max();
            if (maxDistanceFromSource < minMaxDistance)
            {
                minMaxDistance = maxDistanceFromSource;
            }
        }

        var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        UpdateMetrics("GraphRadius", executionTime);

        _algorithmCache.TryAdd(cacheKey, minMaxDistance);
        return minMaxDistance;
    }

    /// <summary>
    /// Recherche de nœuds centraux (centralité de proximité)
    /// </summary>
    public List<(Node node, double centrality)> CalculateClosenessCentrality()
    {
        var cacheKey = "closeness_centrality";
        
        if (_algorithmCache.TryGetValue(cacheKey, out var cachedResult))
        {
            Metrics.CacheHits++;
            return (List<(Node, double)>)cachedResult;
        }

        Metrics.CacheMisses++;
        var startTime = DateTime.UtcNow;

        var allPairs = ComputeAllPairsShortestPaths();
        var centrality = new List<(Node node, double centrality)>();

        foreach (var source in allPairs)
        {
            var sourceNode = _storage.GetNode(source.Key);
            if (sourceNode == null) continue;

            var totalDistance = source.Value.Values.Where(d => d != double.MaxValue).Sum();
            var reachableNodes = source.Value.Values.Count(d => d != double.MaxValue);

            if (reachableNodes > 1)
            {
                var closeness = (reachableNodes - 1) / totalDistance;
                centrality.Add((sourceNode, closeness));
            }
            else
            {
                centrality.Add((sourceNode, 0));
            }
        }

        var sortedCentrality = centrality.OrderByDescending(x => x.centrality).ToList();

        var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        UpdateMetrics("ClosenessCentrality", executionTime);

        _algorithmCache.TryAdd(cacheKey, sortedCentrality);
        return sortedCentrality;
    }

    /// <summary>
    /// Recherche de ponts (arêtes critiques)
    /// </summary>
    public List<Edge> FindBridges()
    {
        var cacheKey = "bridges";
        
        if (_algorithmCache.TryGetValue(cacheKey, out var cachedResult))
        {
            Metrics.CacheHits++;
            return (List<Edge>)cachedResult;
        }

        Metrics.CacheMisses++;
        var startTime = DateTime.UtcNow;

        var allEdges = _storage.GetAllEdges();
        var bridges = new List<Edge>();

        foreach (var edge in allEdges)
        {
            // Temporairement supprimer l'arête
            var tempEdge = edge;
            _storage.RemoveEdge(edge.Id);

            // Vérifier si le graphe reste connexe
            var components = FindConnectedComponents();
            if (components.Count > 1)
            {
                bridges.Add(edge);
            }

            // Restaurer l'arête
            _storage.AddEdge(tempEdge);
        }

        var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        UpdateMetrics("Bridges", executionTime);

        _algorithmCache.TryAdd(cacheKey, bridges);
        return bridges;
    }

    /// <summary>
    /// Recherche de points d'articulation (nœuds critiques)
    /// </summary>
    public List<Node> FindArticulationPoints()
    {
        var cacheKey = "articulation_points";
        
        if (_algorithmCache.TryGetValue(cacheKey, out var cachedResult))
        {
            Metrics.CacheHits++;
            return (List<Node>)cachedResult;
        }

        Metrics.CacheMisses++;
        var startTime = DateTime.UtcNow;

        var allNodes = _storage.GetAllNodes();
        var articulationPoints = new List<Node>();

        foreach (var node in allNodes)
        {
            // Temporairement supprimer le nœud
            var tempNode = node;
            var connectedEdges = _storage.GetEdgesForNode(node.Id);
            _storage.RemoveNode(node.Id);

            // Vérifier si le graphe reste connexe
            var components = FindConnectedComponents();
            if (components.Count > 1)
            {
                articulationPoints.Add(node);
            }

            // Restaurer le nœud et ses arêtes
            _storage.AddNode(tempNode);
            foreach (var edge in connectedEdges)
            {
                _storage.AddEdge(edge);
            }
        }

        var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        UpdateMetrics("ArticulationPoints", executionTime);

        _algorithmCache.TryAdd(cacheKey, articulationPoints);
        return articulationPoints;
    }

    /// <summary>
    /// Optimisation de la recherche de chemins avec cache intelligent
    /// </summary>
    public List<Node> FindOptimizedPath(Guid fromId, Guid toId, string? algorithm = null, string? weightProperty = null)
    {
        algorithm ??= "auto";

        return algorithm.ToLower() switch
        {
            "dijkstra" => FindShortestPathDijkstra(fromId, toId, weightProperty),
            "astar" => FindPathAStar(fromId, toId, weightProperty),
            "auto" => AutoSelectAlgorithm(fromId, toId, weightProperty),
            _ => FindShortestPathDijkstra(fromId, toId, weightProperty)
        };
    }

    /// <summary>
    /// Sélection automatique de l'algorithme optimal basée sur les caractéristiques du graphe
    /// </summary>
    private List<Node> AutoSelectAlgorithm(Guid fromId, Guid toId, string? weightProperty)
    {
        var allNodes = _storage.GetAllNodes();
        var allEdges = _storage.GetAllEdges();
        
        // Heuristique simple : A* pour graphes denses, Dijkstra pour graphes clairsemés
        var density = (double)allEdges.Count / (allNodes.Count * (allNodes.Count - 1));
        
        return density > 0.3 ? FindPathAStar(fromId, toId, weightProperty) : FindShortestPathDijkstra(fromId, toId, weightProperty);
    }

    /// <summary>
    /// Nettoyage du cache avec politique LRU
    /// </summary>
    public void ClearCache()
    {
        lock (_cacheLock)
        {
            _algorithmCache.Clear();
            _distanceCache.Clear();
            _pathCache.Clear();
        }
    }

    /// <summary>
    /// Obtenir les statistiques de performance
    /// </summary>
    public PerformanceMetrics GetPerformanceMetrics()
    {
        var cacheHitRate = Metrics.TotalOperations > 0 ? (double)Metrics.CacheHits / Metrics.TotalOperations : 0;
        Metrics.CacheHitRate = cacheHitRate;
        return Metrics;
    }

    // Méthodes utilitaires privées

    private double GetEdgeWeight(Edge edge, string? weightProperty)
    {
        if (string.IsNullOrEmpty(weightProperty))
            return 1.0;

        if (edge.Properties.TryGetValue(weightProperty, out var weightValue))
        {
            return Convert.ToDouble(weightValue);
        }

        return 1.0;
    }

    private double HeuristicCost(Guid fromId, Guid toId)
    {
        // Heuristique simple basée sur les propriétés des nœuds
        var fromNode = _storage.GetNode(fromId);
        var toNode = _storage.GetNode(toId);
        
        if (fromNode == null || toNode == null)
            return 0;

        // Heuristique basée sur la différence des propriétés numériques
        var fromProps = fromNode.Properties.Values.Where(v => IsNumeric(v)).ToList();
        var toProps = toNode.Properties.Values.Where(v => IsNumeric(v)).ToList();
        
        if (fromProps.Count > 0 && toProps.Count > 0)
        {
            var fromAvg = fromProps.Average(v => Convert.ToDouble(v));
            var toAvg = toProps.Average(v => Convert.ToDouble(v));
            return Math.Abs(fromAvg - toAvg);
        }

        return 1.0;
    }

    private bool IsNumeric(object value)
    {
        return value is int or long or float or double or decimal;
    }

    private List<Node> ReconstructPath(Dictionary<Guid, Guid?> cameFrom, Guid current)
    {
        var path = new List<Node>();
        while (current != Guid.Empty)
        {
            var node = _storage.GetNode(current);
            if (node != null)
                path.Insert(0, node);
            current = cameFrom.GetValueOrDefault(current, Guid.Empty) ?? Guid.Empty;
        }
        return path;
    }

    private void DFS(Guid nodeId, HashSet<Guid> visited, List<Node> component)
    {
        visited.Add(nodeId);
        var node = _storage.GetNode(nodeId);
        if (node != null)
            component.Add(node);

        var edges = _storage.GetEdgesForNode(nodeId);
        foreach (var edge in edges)
        {
            var neighborId = edge.GetOtherNode(nodeId);
            if (!visited.Contains(neighborId))
            {
                DFS(neighborId, visited, component);
            }
        }
    }

    private bool DetectCycleDFS(Guid nodeId, HashSet<Guid> visited, HashSet<Guid> recStack, List<Node> cycle)
    {
        visited.Add(nodeId);
        recStack.Add(nodeId);

        var node = _storage.GetNode(nodeId);
        if (node != null)
            cycle.Add(node);

        var edges = _storage.GetEdgesForNode(nodeId);
        foreach (var edge in edges)
        {
            var neighborId = edge.GetOtherNode(nodeId);
            
            if (!visited.Contains(neighborId))
            {
                if (DetectCycleDFS(neighborId, visited, recStack, cycle))
                    return true;
            }
            else if (recStack.Contains(neighborId))
            {
                return true;
            }
        }

        recStack.Remove(nodeId);
        cycle.RemoveAt(cycle.Count - 1);
        return false;
    }

    private void UpdateMetrics(string algorithm, double executionTime)
    {
        lock (_cacheLock)
        {
            Metrics.TotalOperations++;
            Metrics.AverageExecutionTime = (Metrics.AverageExecutionTime * (Metrics.TotalOperations - 1) + executionTime) / Metrics.TotalOperations;
            
            if (!Metrics.AlgorithmPerformance.ContainsKey(algorithm))
                Metrics.AlgorithmPerformance[algorithm] = 0;
            
            Metrics.AlgorithmPerformance[algorithm] = (Metrics.AlgorithmPerformance[algorithm] + executionTime) / 2;
        }
    }
} 