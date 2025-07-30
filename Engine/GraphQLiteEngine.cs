using GraphQLite.Models;
using GraphQLite.Storage;
using GraphQLite.Query;
using System.Text.RegularExpressions;

namespace GraphQLite.Engine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

/// <summary>
/// Moteur principal de la base de données GraphQLite
/// Orchestre les opérations de requête et de stockage
/// </summary>
public class GraphQLiteEngine : IDisposable
{
    private readonly GraphStorage _storage;
    private readonly NaturalLanguageParser _parser;
    private readonly VariableManager _variableManager;

    public GraphQLiteEngine(string databasePath)
    {
        _storage = new GraphStorage(databasePath);
        _parser = new NaturalLanguageParser();
        _variableManager = new VariableManager();
    }

    /// <summary>
    /// Initialise la base de données
    /// </summary>
    public async Task<GraphStorage.LoadResult> InitializeAsync()
    {
        return await _storage.LoadAsync();
    }

    /// <summary>
    /// Exécute une requête en langage naturel
    /// </summary>
    public async Task<QueryResult> ExecuteQueryAsync(string query)
    {
        try
        {
            var parsedQuery = _parser.Parse(query);
            
            // Traiter les variables si c'est une définition de variable
            if (parsedQuery.Type == QueryType.DefineVariable)
            {
                return await DefineVariableAsync(parsedQuery);
            }
            
            // Remplacer les variables dans la requête parsée
            ReplaceVariablesInParsedQuery(parsedQuery);
            
            var result = await ExecuteParsedQueryAsync(parsedQuery);
            
            // Sauvegarder après les opérations de modification
            if (IsModifyingOperation(parsedQuery.Type))
            {
                await _storage.SaveAsync();
            }

            return result;
        }
        catch (Exception ex)
        {
            return new QueryResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    private Task<QueryResult> ExecuteParsedQueryAsync(ParsedQuery query)
    {
        return query.Type switch
        {
            QueryType.CreateNode => CreateNodeAsync(query),
            QueryType.CreateEdge => CreateEdgeAsync(query),
            QueryType.FindNodes => FindNodesAsync(query),
            QueryType.FindEdges => FindEdgesAsync(query),
            QueryType.FindPath => FindPathAsync(query),
            QueryType.FindWithinSteps => FindWithinStepsAsync(query),
            QueryType.UpdateNode => UpdateNodeAsync(query),
            QueryType.UpdateEdge => UpdateEdgeAsync(query),
            QueryType.DeleteNode => DeleteNodeAsync(query),
            QueryType.DeleteEdge => DeleteEdgeAsync(query),
            QueryType.Count => CountNodesAsync(query),
            QueryType.Aggregate => ExecuteAggregateAsync(query),
            QueryType.DefineVariable => DefineVariableAsync(query),
            QueryType.BatchOperation => ExecuteBatchOperationAsync(query),
            QueryType.ShowSchema => ShowSchemaAsync(),
            _ => throw new NotSupportedException($"Type de requête non supporté : {query.Type}")
        };
    }

    private Task<QueryResult> CreateNodeAsync(ParsedQuery query)
    {
        try
        {
            // Les variables ont déjà été remplacées dans ReplaceVariablesInParsedQuery
            var node = new Node(query.NodeLabel ?? "node", query.Properties);
            
            // S'assurer que toutes les propriétés sont correctement assignées
            foreach (var property in query.Properties)
            {
                node.SetProperty(property.Key, property.Value);
            }
            
            _storage.AddNode(node);
            
            return Task.FromResult(new QueryResult
            {
                Success = true,
                Message = $"Nœud créé avec l'ID : {node.Id}",
                Data = node
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new QueryResult
            {
                Success = false,
                Error = $"Erreur lors de la création du nœud : {ex.Message}"
            });
        }
    }

    private Task<QueryResult> CreateEdgeAsync(ParsedQuery query)
    {
        try
        {
            // Parser les noms avec labels et guillemets
            var fromNodeInfo = ParseNodeReference(query.FromNode!);
            var toNodeInfo = ParseNodeReference(query.ToNode!);
            
            // Chercher les nœuds par nom avec gestion des labels
            var fromNodes = _storage.GetAllNodes()
                .Where(n => MatchesNodeReference(n, fromNodeInfo))
                .ToList();

            var toNodes = _storage.GetAllNodes()
                .Where(n => MatchesNodeReference(n, toNodeInfo))
                .ToList();

            if (!fromNodes.Any())
                return Task.FromResult(new QueryResult { Success = false, Error = $"Nœud source '{query.FromNode}' introuvable" });

            if (!toNodes.Any())
                return Task.FromResult(new QueryResult { Success = false, Error = $"Nœud destination '{query.ToNode}' introuvable" });

            var edge = new Edge(fromNodes.First().Id, toNodes.First().Id, query.EdgeType!, query.Properties);
            _storage.AddEdge(edge);

            return Task.FromResult(new QueryResult
            {
                Success = true,
                Message = $"Arête créée avec l'ID : {edge.Id}",
                Data = new { EdgeId = edge.Id, Edge = edge }
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new QueryResult
            {
                Success = false,
                Error = $"Erreur lors de la création de l'arête : {ex.Message}"
            });
        }
    }

    /// <summary>
    /// Parse une référence de nœud (label "nom" ou juste nom)
    /// </summary>
    private (string? Label, string Name) ParseNodeReference(string nodeReference)
    {
        // Pattern pour "label "nom"" - gère les espaces dans les noms
        var match = Regex.Match(nodeReference, @"^(\w+)\s+""([^""]+)""$");
        if (match.Success)
        {
            return (match.Groups[1].Value, match.Groups[2].Value);
        }
        
        // Pattern alternatif pour "label nom" (sans guillemets)
        var matchWithoutQuotes = Regex.Match(nodeReference, @"^(\w+)\s+(.+)$");
        if (matchWithoutQuotes.Success)
        {
            return (matchWithoutQuotes.Groups[1].Value, matchWithoutQuotes.Groups[2].Value);
        }
        
        // Pattern pour "label nom" avec espaces dans le nom (sans guillemets)
        var matchWithSpaces = Regex.Match(nodeReference, @"^(\w+)\s+(.+)$");
        if (matchWithSpaces.Success)
        {
            return (matchWithSpaces.Groups[1].Value, matchWithSpaces.Groups[2].Value);
        }
        
        // Sinon, c'est juste un nom
        return (null, nodeReference);
    }

    /// <summary>
    /// Vérifie si un nœud correspond à une référence
    /// </summary>
    private bool MatchesNodeReference(Node node, (string? Label, string Name) reference)
    {
        // Vérifier le nom d'abord
        var nodeName = node.GetProperty<string>("name");
        if (nodeName == null || !nodeName.Equals(reference.Name, StringComparison.OrdinalIgnoreCase))
            return false;
        
        // Si un label est spécifié, vérifier qu'il correspond
        if (reference.Label != null)
        {
            return node.Label.Equals(reference.Label, StringComparison.OrdinalIgnoreCase);
        }
        
        return true;
    }

    private async Task<QueryResult> FindNodesAsync(ParsedQuery query)
    {
        var nodes = _storage.GetNodesByLabel(query.NodeLabel!);

        // Appliquer les conditions
        if (query.Conditions.Any())
        {
            nodes = await FilterNodesByConditionsAsync(nodes, query.Conditions);
        }

        // Appliquer la pagination (OFFSET puis LIMIT)
        if (query.Offset.HasValue)
        {
            nodes = nodes.Skip(query.Offset.Value).ToList();
        }

        if (query.Limit.HasValue)
        {
            nodes = nodes.Take(query.Limit.Value).ToList();
        }

        return new QueryResult
        {
            Success = true,
            Message = $"{nodes.Count} nœud(s) trouvé(s)",
            Data = nodes
        };
    }

    private Task<QueryResult> FindEdgesAsync(ParsedQuery query)
    {
        try
        {
            var allEdges = _storage.GetAllEdges();
            var filteredEdges = new List<Edge>();

            // Si des nœuds source et destination sont spécifiés
            if (!string.IsNullOrEmpty(query.FromNode) && !string.IsNullOrEmpty(query.ToNode))
            {
                var fromNodeInfo = ParseNodeReference(query.FromNode);
                var toNodeInfo = ParseNodeReference(query.ToNode);
                
                var fromNodes = _storage.GetAllNodes()
                    .Where(n => MatchesNodeReference(n, fromNodeInfo))
                    .ToList();

                var toNodes = _storage.GetAllNodes()
                    .Where(n => MatchesNodeReference(n, toNodeInfo))
                    .ToList();

                if (!fromNodes.Any())
                {
                    return Task.FromResult(new QueryResult
                    {
                        Success = false,
                        Error = $"Nœud source '{query.FromNode}' introuvable"
                    });
                }

                if (!toNodes.Any())
                {
                    return Task.FromResult(new QueryResult
                    {
                        Success = false,
                        Error = $"Nœud destination '{query.ToNode}' introuvable"
                    });
                }

                var fromNodeId = fromNodes.First().Id;
                var toNodeId = toNodes.First().Id;

                filteredEdges = allEdges.Where(e => 
                    (e.FromNodeId == fromNodeId && e.ToNodeId == toNodeId) ||
                    (e.FromNodeId == toNodeId && e.ToNodeId == fromNodeId) // Support bidirectionnel
                ).ToList();
            }
            // Si seulement le nœud source est spécifié
            else if (!string.IsNullOrEmpty(query.FromNode))
            {
                var fromNodeInfo = ParseNodeReference(query.FromNode);
                var fromNodes = _storage.GetAllNodes()
                    .Where(n => MatchesNodeReference(n, fromNodeInfo))
                    .ToList();

                if (!fromNodes.Any())
                {
                    return Task.FromResult(new QueryResult
                    {
                        Success = false,
                        Error = $"Nœud source '{query.FromNode}' introuvable"
                    });
                }

                var fromNodeId = fromNodes.First().Id;
                filteredEdges = allEdges.Where(e => 
                    e.FromNodeId == fromNodeId || e.ToNodeId == fromNodeId
                ).ToList();
            }
            // Si seulement le nœud destination est spécifié
            else if (!string.IsNullOrEmpty(query.ToNode))
            {
                var toNodeInfo = ParseNodeReference(query.ToNode);
                var toNodes = _storage.GetAllNodes()
                    .Where(n => MatchesNodeReference(n, toNodeInfo))
                    .ToList();

                if (!toNodes.Any())
                {
                    return Task.FromResult(new QueryResult
                    {
                        Success = false,
                        Error = $"Nœud destination '{query.ToNode}' introuvable"
                    });
                }

                var toNodeId = toNodes.First().Id;
                filteredEdges = allEdges.Where(e => 
                    e.FromNodeId == toNodeId || e.ToNodeId == toNodeId
                ).ToList();
            }
            // Si aucun nœud spécifique, retourner toutes les arêtes
            else
            {
                filteredEdges = allEdges;
            }

            // Appliquer les conditions si présentes
            if (query.Conditions.Any())
            {
                filteredEdges = FilterEdgesByConditions(filteredEdges, query.Conditions);
            }

            // Appliquer la pagination
            if (query.Offset.HasValue)
            {
                filteredEdges = filteredEdges.Skip(query.Offset.Value).ToList();
            }

            if (query.Limit.HasValue)
            {
                filteredEdges = filteredEdges.Take(query.Limit.Value).ToList();
            }

            return Task.FromResult(new QueryResult
            {
                Success = true,
                Message = $"{filteredEdges.Count} arête(s) trouvée(s)",
                Data = filteredEdges
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new QueryResult
            {
                Success = false,
                Error = $"Erreur lors de la recherche d'arêtes : {ex.Message}"
            });
        }
    }

    private Task<QueryResult> FindPathAsync(ParsedQuery query)
    {
        try
        {
            // Les variables ont déjà été remplacées dans ReplaceVariablesInParsedQuery
            var fromNodeName = query.FromNode;
            var toNodeName = query.ToNode;
            
            if (string.IsNullOrEmpty(fromNodeName) || string.IsNullOrEmpty(toNodeName))
            {
                return Task.FromResult(new QueryResult
                {
                    Success = false,
                    Error = "Les nœuds source et destination doivent être spécifiés"
                });
            }
            
            // Parser les références de nœuds avec labels et guillemets
            var fromNodeInfo = ParseNodeReference(fromNodeName);
            var toNodeInfo = ParseNodeReference(toNodeName);
            
            // Recherche de chemin simple (BFS)
            var fromNodes = _storage.GetAllNodes()
                .Where(n => MatchesNodeReference(n, fromNodeInfo))
                .ToList();

            var toNodes = _storage.GetAllNodes()
                .Where(n => MatchesNodeReference(n, toNodeInfo))
                .ToList();

            if (!fromNodes.Any())
            {
                return Task.FromResult(new QueryResult
                {
                    Success = false,
                    Error = $"Nœud source '{fromNodeName}' introuvable"
                });
            }

            if (!toNodes.Any())
            {
                return Task.FromResult(new QueryResult
                {
                    Success = false,
                    Error = $"Nœud destination '{toNodeName}' introuvable"
                });
            }

            var fromNodeId = fromNodes.First().Id;
            var toNodeId = toNodes.First().Id;

            // Vérifier si c'est un chemin bidirectionnel
            var isBidirectional = query.Properties.ContainsKey("bidirectional") && 
                                 (bool)query.Properties["bidirectional"];

            // Vérifier s'il y a un type d'arête à éviter
            var avoidEdgeType = query.Properties.ContainsKey("avoid_edge_type") ? 
                               query.Properties["avoid_edge_type"].ToString() : null;

            // Vérifier s'il y a un type d'arête spécifique à utiliser
            var viaEdgeType = !string.IsNullOrEmpty(query.EdgeType) ? query.EdgeType : null;

            // Vérifier le nombre maximum d'étapes
            var maxSteps = query.MaxSteps ?? 100; // Limite par défaut

            var path = FindAdvancedPath(fromNodeId, toNodeId, viaEdgeType, avoidEdgeType, maxSteps, isBidirectional);

            if (!path.Any())
            {
                return Task.FromResult(new QueryResult
                {
                    Success = true,
                    Message = $"Aucun chemin trouvé entre '{fromNodeName}' et '{toNodeName}'",
                    Data = new { Path = new List<object>(), FromNode = fromNodeName, ToNode = toNodeName }
                });
            }

            return Task.FromResult(new QueryResult
            {
                Success = true,
                Message = $"Chemin trouvé entre '{fromNodeName}' et '{toNodeName}' ({path.Count} nœuds)",
                Data = new { Path = path, FromNode = fromNodeName, ToNode = toNodeName, PathLength = path.Count }
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new QueryResult
            {
                Success = false,
                Error = $"Erreur lors de la recherche de chemin : {ex.Message}"
            });
        }
    }

    private Task<QueryResult> FindWithinStepsAsync(ParsedQuery query)
    {
        try
        {
            // Les variables ont déjà été remplacées dans ReplaceVariablesInParsedQuery
            var fromNodeName = query.FromNode;
            var targetLabel = query.NodeLabel;
            var maxSteps = query.MaxSteps ?? 3; // Valeur par défaut
            
            if (string.IsNullOrEmpty(fromNodeName) || string.IsNullOrEmpty(targetLabel))
            {
                return Task.FromResult(new QueryResult
                {
                    Success = false,
                    Error = "Le nœud source et le label cible doivent être spécifiés"
                });
            }
            
            // Parser la référence de nœud avec labels et guillemets
            var fromNodeInfo = ParseNodeReference(fromNodeName);
            
            var fromNodes = _storage.GetAllNodes()
                .Where(n => MatchesNodeReference(n, fromNodeInfo))
                .ToList();

            if (!fromNodes.Any())
            {
                return Task.FromResult(new QueryResult
                {
                    Success = false,
                    Error = $"Nœud source '{fromNodeName}' introuvable"
                });
            }

            var fromNodeId = fromNodes.First().Id;

            // Vérifier s'il y a un type d'arête spécifique à utiliser
            var viaEdgeType = !string.IsNullOrEmpty(query.EdgeType) ? query.EdgeType : null;

            // Vérifier s'il y a un type d'arête à éviter
            var avoidEdgeType = query.Properties.ContainsKey("avoid_edge_type") ? 
                               query.Properties["avoid_edge_type"].ToString() : null;

            var foundNodes = FindNodesWithinStepsAdvanced(fromNodeId, targetLabel, maxSteps, viaEdgeType, avoidEdgeType);

            // Appliquer les conditions si présentes
            if (query.Conditions.Any())
            {
                foundNodes = FilterNodesByConditionsSync(foundNodes, query.Conditions);
            }

            return Task.FromResult(new QueryResult
            {
                Success = true,
                Message = $"Trouvé {foundNodes.Count} nœuds de type '{targetLabel}' dans un rayon de {maxSteps} étapes depuis '{fromNodeName}'",
                Data = new { Nodes = foundNodes, FromNode = fromNodeName, TargetLabel = targetLabel, MaxSteps = maxSteps, Count = foundNodes.Count }
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new QueryResult
            {
                Success = false,
                Error = $"Erreur lors de la recherche dans les étapes : {ex.Message}"
            });
        }
    }

    private Task<QueryResult> UpdateNodeAsync(ParsedQuery query)
    {
        var nodes = _storage.GetNodesByLabel(query.NodeLabel!);

        if (query.Conditions.Any())
        {
            nodes = FilterNodesByConditions(nodes, query.Conditions);
        }

        int updatedCount = 0;
        foreach (var node in nodes)
        {
            foreach (var property in query.Properties)
            {
                node.SetProperty(property.Key, property.Value);
            }
            updatedCount++;
        }

        return Task.FromResult(new QueryResult
        {
            Success = true,
            Message = $"{updatedCount} nœud(s) mis à jour",
            Data = nodes
        });
    }

    private Task<QueryResult> DeleteNodeAsync(ParsedQuery query)
    {
        var nodes = _storage.GetNodesByLabel(query.NodeLabel!);

        if (query.Conditions.Any())
        {
            nodes = FilterNodesByConditions(nodes, query.Conditions);
        }

        int deletedCount = 0;
        foreach (var node in nodes.ToList())
        {
            if (_storage.RemoveNode(node.Id))
                deletedCount++;
        }

        return Task.FromResult(new QueryResult
        {
            Success = true,
            Message = $"{deletedCount} nœud(s) supprimé(s)"
        });
    }

    private Task<QueryResult> DeleteEdgeAsync(ParsedQuery query)
    {
        // Chercher les nœuds source et destination par nom
        var fromNodes = _storage.GetAllNodes()
            .Where(n => n.GetProperty<string>("name")?.Equals(query.FromNode, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        var toNodes = _storage.GetAllNodes()
            .Where(n => n.GetProperty<string>("name")?.Equals(query.ToNode, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (!fromNodes.Any())
        {
            return Task.FromResult(new QueryResult 
            { 
                Success = false, 
                Error = $"Nœud source '{query.FromNode}' introuvable" 
            });
        }

        if (!toNodes.Any())
        {
            return Task.FromResult(new QueryResult 
            { 
                Success = false, 
                Error = $"Nœud destination '{query.ToNode}' introuvable" 
            });
        }

        var fromNodeId = fromNodes.First().Id;
        var toNodeId = toNodes.First().Id;

        // Trouver toutes les arêtes entre ces nœuds
        var allEdges = _storage.GetAllEdges();
        var edgesToDelete = allEdges.Where(e => 
            (e.FromNodeId == fromNodeId && e.ToNodeId == toNodeId) ||
            (e.FromNodeId == toNodeId && e.ToNodeId == fromNodeId) // Arêtes bidirectionnelles
        ).ToList();

        // Appliquer les conditions si présentes (par exemple pour filtrer par type de relation)
        if (query.Conditions.Any())
        {
            edgesToDelete = FilterEdgesByConditions(edgesToDelete, query.Conditions);
        }

        int deletedCount = 0;
        foreach (var edge in edgesToDelete)
        {
            if (_storage.RemoveEdge(edge.Id))
                deletedCount++;
        }

        return Task.FromResult(new QueryResult
        {
            Success = true,
            Message = $"{deletedCount} arête(s) supprimée(s) entre '{query.FromNode}' et '{query.ToNode}'"
        });
    }

    private Task<QueryResult> CountNodesAsync(ParsedQuery query)
    {
        // Vérifier si c'est un comptage d'arêtes
        if (query.Properties.ContainsKey("count_edges"))
        {
            return CountEdgesAsync(query);
        }
        
        // Vérifier si c'est un comptage d'arêtes par type
        if (query.NodeLabel == "edges" || query.NodeLabel == "edge")
        {
            return CountEdgesAsync(query);
        }
        
        var nodes = _storage.GetNodesByLabel(query.NodeLabel!);

        if (query.Conditions.Any())
        {
            nodes = FilterNodesByConditions(nodes, query.Conditions);
        }

        // Appliquer la pagination avant le comptage (utile pour compter une page spécifique)
        if (query.Offset.HasValue)
        {
            nodes = nodes.Skip(query.Offset.Value).ToList();
        }

        if (query.Limit.HasValue)
        {
            nodes = nodes.Take(query.Limit.Value).ToList();
        }

        return Task.FromResult(new QueryResult
        {
            Success = true,
            Message = $"Nombre de nœuds : {nodes.Count}",
            Data = nodes.Count
        });
    }

    private Task<QueryResult> CountEdgesAsync(ParsedQuery query)
    {
        var edges = _storage.GetAllEdges();
        
        if (query.Conditions.Any())
        {
            edges = FilterEdgesByConditions(edges, query.Conditions);
        }

        return Task.FromResult(new QueryResult
        {
            Success = true,
            Message = $"Nombre d'arêtes : {edges.Count}",
            Data = edges.Count
        });
    }

    private Task<QueryResult> ShowSchemaAsync()
    {
        var schema = GenerateDatabaseSchema();

        return Task.FromResult(new QueryResult
        {
            Success = true,
            Message = $"Schéma de la base de données ({schema.TotalNodes} nœuds, {schema.TotalEdges} arêtes)",
            Data = schema
        });
    }

    /// <summary>
    /// Génère le schéma complet de la base de données
    /// </summary>
    private DatabaseSchema GenerateDatabaseSchema()
    {
        var schema = new DatabaseSchema
        {
            GeneratedAt = DateTime.UtcNow
        };

        var allNodes = _storage.GetAllNodes();
        var allEdges = _storage.GetAllEdges();

        schema.TotalNodes = allNodes.Count;
        schema.TotalEdges = allEdges.Count;

        // Analyser les nœuds
        foreach (var nodeGroup in allNodes.GroupBy(n => n.Label))
        {
            var nodeSchema = new SchemaInfo
            {
                Label = nodeGroup.Key,
                Count = nodeGroup.Count(),
                FirstSeen = nodeGroup.Min(n => n.CreatedAt),
                LastUpdated = nodeGroup.Max(n => n.UpdatedAt)
            };

            // Analyser les propriétés des nœuds
            foreach (var node in nodeGroup)
            {
                foreach (var property in node.Properties)
                {
                    var propertyName = property.Key;
                    var propertyValue = property.Value;

                    if (!nodeSchema.Properties.ContainsKey(propertyName))
                    {
                        nodeSchema.Properties[propertyName] = new TypeInfo
                        {
                            Type = propertyValue?.GetType().Name ?? "null",
                            Count = 0,
                            SampleValue = propertyValue,
                            UniqueValues = new HashSet<object>()
                        };
                    }

                    var typeInfo = nodeSchema.Properties[propertyName];
                    typeInfo.Count++;
                    
                    if (propertyValue != null && typeInfo.UniqueValues.Count < 10) // Limite pour éviter trop de valeurs
                    {
                        typeInfo.UniqueValues.Add(propertyValue);
                    }
                }
            }

            schema.NodeSchemas[nodeGroup.Key] = nodeSchema;
        }

        // Analyser les arêtes
        foreach (var edgeGroup in allEdges.GroupBy(e => e.RelationType))
        {
            var edgeSchema = new SchemaInfo
            {
                Label = edgeGroup.Key,
                Count = edgeGroup.Count(),
                FirstSeen = edgeGroup.Min(e => e.CreatedAt),
                LastUpdated = edgeGroup.Max(e => e.UpdatedAt)
            };

            // Analyser les propriétés des arêtes
            foreach (var edge in edgeGroup)
            {
                foreach (var property in edge.Properties)
                {
                    var propertyName = property.Key;
                    var propertyValue = property.Value;

                    if (!edgeSchema.Properties.ContainsKey(propertyName))
                    {
                        edgeSchema.Properties[propertyName] = new TypeInfo
                        {
                            Type = propertyValue?.GetType().Name ?? "null",
                            Count = 0,
                            SampleValue = propertyValue,
                            UniqueValues = new HashSet<object>()
                        };
                    }

                    var typeInfo = edgeSchema.Properties[propertyName];
                    typeInfo.Count++;
                    
                    if (propertyValue != null && typeInfo.UniqueValues.Count < 10)
                    {
                        typeInfo.UniqueValues.Add(propertyValue);
                    }
                }
            }

            schema.EdgeSchemas[edgeGroup.Key] = edgeSchema;
        }

        return schema;
    }

    private async Task<List<Node>> FilterNodesByConditionsAsync(List<Node> nodes, Dictionary<string, object> conditions)
    {
        if (!conditions.Any()) return nodes;

        // Remplacer les variables dans les conditions avant l'évaluation
        var processedConditions = new Dictionary<string, object>();
        foreach (var condition in conditions)
        {
            var key = condition.Key;
            var value = condition.Value;
            
            // Remplacer les variables dans la clé si nécessaire
            if (key.Contains("$"))
            {
                // Ne pas remplacer les variables dans les clés de propriétés
                // Les clés comme "role" ne doivent pas être remplacées
                if (!key.StartsWith("$"))
                {
                    key = _variableManager.ReplaceVariables(key);
                }
            }
            
            // Remplacer les variables dans la valeur si c'est une chaîne
            if (value is string strValue && strValue.Contains("$"))
            {
                // Essayer plusieurs fois le remplacement pour gérer les cas complexes
                var originalValue = strValue;
                var replacedValue = _variableManager.ReplaceVariables(strValue);
                
                // Si la valeur n'a pas changé, essayer avec une approche plus agressive
                if (replacedValue == originalValue && strValue.Contains("$"))
                {
                    // Essayer de remplacer chaque variable individuellement
                    var finalValue = strValue;
                    var variablePattern = @"\$([a-zA-Z_][a-zA-Z0-9_]*)";
                    var matches = Regex.Matches(strValue, variablePattern);
                    
                    foreach (Match match in matches)
                    {
                        var varName = match.Value; // $variableName
                        var varNameWithoutDollar = match.Groups[1].Value; // variableName
                        
                        // Essayer de trouver la variable avec différentes variations de casse
                        var foundVariable = _variableManager.GetAllVariables()
                            .FirstOrDefault(kvp => string.Equals(kvp.Key, varName, StringComparison.OrdinalIgnoreCase) || 
                                                   string.Equals(kvp.Key, varNameWithoutDollar, StringComparison.OrdinalIgnoreCase));
                        
                        if (foundVariable.Key != null)
                        {
                            finalValue = finalValue.Replace(varName, foundVariable.Value?.ToString() ?? "");
                        }
                    }
                    
                    value = finalValue;
                }
                else
                {
                    value = replacedValue;
                }
            }
            
            processedConditions[key] = value;
        }

        var filteredNodes = new List<Node>();
        
        foreach (var node in nodes)
        {
            // Séparer les conditions AND et OR
            var andConditions = new List<KeyValuePair<string, object>>();
            var orConditions = new List<KeyValuePair<string, object>>();

            foreach (var condition in processedConditions)
            {
                if (condition.Key.StartsWith("Or_"))
                {
                    orConditions.Add(condition);
                }
                else
                {
                    andConditions.Add(condition);
                }
            }

            // Évaluer les conditions AND
            bool andResult = true;
            if (andConditions.Any())
            {
                var andResults = await Task.WhenAll(andConditions.Select(async condition =>
                {
                    var result = await EvaluateConditionAsync(node, condition.Key, condition.Value);
                    return result;
                }));
                andResult = andResults.All(x => x);
            }

            // Évaluer les conditions OR
            bool orResult = false;
            if (orConditions.Any())
            {
                var orResults = await Task.WhenAll(orConditions.Select(async condition =>
                {
                    var result = await EvaluateConditionAsync(node, condition.Key, condition.Value);
                    return result;
                }));
                orResult = orResults.Any(x => x);
            }

            // Logique finale
            bool finalResult;
            if (andConditions.Any() && orConditions.Any())
            {
                // Cas mixte : AND ET OR
                finalResult = andResult && orResult;
            }
            else if (andConditions.Any())
            {
                // Que des conditions AND
                finalResult = andResult;
            }
            else if (orConditions.Any())
            {
                // Que des conditions OR
                finalResult = orResult;
            }
            else
            {
                // Aucune condition
                finalResult = true;
            }

            if (finalResult)
            {
                filteredNodes.Add(node);
            }
        }
        
        return filteredNodes;
    }

    /// <summary>
    /// Évalue une condition individuelle sur un nœud
    /// </summary>
    private async Task<bool> EvaluateConditionAsync(Node node, string conditionKey, object expectedValue)
    {
        try
        {
            // Gestion des conditions complexes avec relations
            if (conditionKey == "connected_to_via")
            {
                return await EvaluateConnectedToViaConditionAsync(node, expectedValue);
            }
            else if (conditionKey == "has_edge")
            {
                return await EvaluateHasEdgeConditionAsync(node, expectedValue);
            }
            else if (conditionKey == "connected_via")
            {
                return await EvaluateConnectedViaConditionAsync(node, expectedValue);
            }
            
            // Gestion des conditions normales
            var actualValue = GetNodeValueForCondition(node, conditionKey);
            
            if (actualValue == null && expectedValue == null)
                return true;
            
            if (actualValue == null || expectedValue == null)
                return false;
            
            // Si la valeur attendue est une sous-requête
            if (expectedValue is ParsedQuery subQuery)
            {
                return await EvaluateSubQueryConditionAsync(node, conditionKey, subQuery);
            }
            
            // Comparaison directe
            return CompareForEquality(actualValue, expectedValue);
        }
        catch (Exception ex)
        {
            // En cas d'erreur, retourner false pour éviter les faux positifs
            return false;
        }
    }
    
    /// <summary>
    /// Évalue une condition "connected to via"
    /// </summary>
    private async Task<bool> EvaluateConnectedToViaConditionAsync(Node node, object expectedValue)
    {
        try
        {
            if (expectedValue is string edgeTypeStr)
            {
                // Format simple : "connected to via [edge_type]"
                var edges = _storage.GetEdgesForNode(node.Id);
                return edges.Any(e => e.RelationType.Equals(edgeTypeStr, StringComparison.OrdinalIgnoreCase));
            }
            else if (expectedValue is Dictionary<string, object> dictValue)
            {
                // Format avec label et edge type
                var targetLabel = dictValue.GetValueOrDefault("Label")?.ToString();
                var edgeType = dictValue.GetValueOrDefault("EdgeType")?.ToString();
                
                if (!string.IsNullOrEmpty(targetLabel) && !string.IsNullOrEmpty(edgeType))
                {
                    var edges = _storage.GetEdgesForNode(node.Id);
                    var connectedNodes = new List<Node>();
                    
                    foreach (var edge in edges)
                    {
                        if (edge.RelationType.Equals(edgeType, StringComparison.OrdinalIgnoreCase))
                        {
                            var otherNodeId = edge.GetOtherNode(node.Id);
                            var otherNode = _storage.GetNode(otherNodeId);
                            if (otherNode != null && otherNode.Label.Equals(targetLabel, StringComparison.OrdinalIgnoreCase))
                            {
                                connectedNodes.Add(otherNode);
                            }
                        }
                    }
                    
                    return connectedNodes.Any();
                }
            }
            else if (expectedValue is ParsedQuery subQuery)
            {
                // Format avec sous-requête
                var edges = _storage.GetEdgesForNode(node.Id);
                var connectedNodes = new List<Node>();
                
                foreach (var edge in edges)
                {
                    if (edge.RelationType.Equals(subQuery.EdgeType, StringComparison.OrdinalIgnoreCase))
                    {
                        var otherNodeId = edge.GetOtherNode(node.Id);
                        var otherNode = _storage.GetNode(otherNodeId);
                        if (otherNode != null)
                        {
                            // Appliquer les conditions de la sous-requête
                            if (subQuery.Conditions.Any())
                            {
                                var filteredNodes = await FilterNodesByConditionsAsync(new List<Node> { otherNode }, subQuery.Conditions);
                                if (filteredNodes.Any())
                                {
                                    connectedNodes.Add(otherNode);
                                }
                            }
                            else
                            {
                                connectedNodes.Add(otherNode);
                            }
                        }
                    }
                }
                
                return connectedNodes.Any();
            }
            
            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }
    
    /// <summary>
    /// Évalue une condition "has edge"
    /// </summary>
    private async Task<bool> EvaluateHasEdgeConditionAsync(Node node, object expectedValue)
    {
        try
        {
            if (expectedValue is Dictionary<string, object> dictValue)
            {
                var edgeType = dictValue.GetValueOrDefault("EdgeType")?.ToString();
                var targetLabel = dictValue.GetValueOrDefault("TargetLabel")?.ToString();
                
                if (!string.IsNullOrEmpty(edgeType) && !string.IsNullOrEmpty(targetLabel))
                {
                    var edges = _storage.GetEdgesForNode(node.Id);
                    
                    foreach (var edge in edges)
                    {
                        if (edge.RelationType.Equals(edgeType, StringComparison.OrdinalIgnoreCase))
                        {
                            var otherNodeId = edge.GetOtherNode(node.Id);
                            var otherNode = _storage.GetNode(otherNodeId);
                            if (otherNode != null && otherNode.Label.Equals(targetLabel, StringComparison.OrdinalIgnoreCase))
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            else if (expectedValue is ParsedQuery subQuery)
            {
                // Format avec sous-requête
                var edges = _storage.GetEdgesForNode(node.Id);
                
                foreach (var edge in edges)
                {
                    if (edge.RelationType.Equals(subQuery.EdgeType, StringComparison.OrdinalIgnoreCase))
                    {
                        var otherNodeId = edge.GetOtherNode(node.Id);
                        var otherNode = _storage.GetNode(otherNodeId);
                        if (otherNode != null)
                        {
                            // Appliquer les conditions de la sous-requête
                            if (subQuery.Conditions.Any())
                            {
                                var filteredNodes = await FilterNodesByConditionsAsync(new List<Node> { otherNode }, subQuery.Conditions);
                                if (filteredNodes.Any())
                                {
                                    return true;
                                }
                            }
                            else
                            {
                                return true;
                            }
                        }
                    }
                }
            }
            
            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }
    
    /// <summary>
    /// Évalue une condition "connected via"
    /// </summary>
    private async Task<bool> EvaluateConnectedViaConditionAsync(Node node, object expectedValue)
    {
        try
        {
            if (expectedValue is string edgeType)
            {
                // Format simple : "connected via [edge_type]"
                var edges = _storage.GetEdgesForNode(node.Id);
                return edges.Any(e => e.RelationType.Equals(edgeType, StringComparison.OrdinalIgnoreCase));
            }
            else if (expectedValue is ParsedQuery subQuery)
            {
                // Format avec sous-requête
                var edges = _storage.GetEdgesForNode(node.Id);
                var connectedNodes = new List<Node>();
                
                foreach (var edge in edges)
                {
                    if (edge.RelationType.Equals(subQuery.EdgeType, StringComparison.OrdinalIgnoreCase))
                    {
                        var otherNodeId = edge.GetOtherNode(node.Id);
                        var otherNode = _storage.GetNode(otherNodeId);
                        if (otherNode != null)
                        {
                            // Appliquer les conditions de la sous-requête
                            if (subQuery.Conditions.Any())
                            {
                                var filteredNodes = await FilterNodesByConditionsAsync(new List<Node> { otherNode }, subQuery.Conditions);
                                if (filteredNodes.Any())
                                {
                                    connectedNodes.Add(otherNode);
                                }
                            }
                            else
                            {
                                connectedNodes.Add(otherNode);
                            }
                        }
                    }
                }
                
                return connectedNodes.Any();
            }
            
            return false;
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    /// <summary>
    /// Évalue une condition individuelle sur une arête avec gestion des nulls
    /// </summary>
    private bool EvaluateEdgeCondition(Edge edge, string conditionKey, object expectedValue)
    {
        // Parser la clé de condition
        var keyParts = conditionKey.Split('_');
        
        string property;
        string @operator;

        if (keyParts.Length == 2)
        {
            property = keyParts[0];
            @operator = keyParts[1];
        }
        else if (keyParts.Length == 3)
        {
            property = keyParts[1];
            @operator = keyParts[2];
        }
        else
        {
            property = conditionKey;
            @operator = "eq";
        }

        // Vérifier les propriétés spéciales des arêtes
        object? actualValue = property.ToLower() switch
        {
            "type" or "relationtype" => edge.RelationType,
            _ => edge.Properties.TryGetValue(property, out var propValue) ? propValue : null
        };

        if (actualValue == null)
        {
            return @operator.ToLower() switch
            {
                "eq" => expectedValue == null,
                "ne" => expectedValue != null,
                _ => false
            };
        }

        // Évaluer selon l'opérateur
        return @operator.ToLower() switch
        {
            "eq" => CompareForEquality(actualValue, expectedValue),
            "ne" => !CompareForEquality(actualValue, expectedValue),
            "gt" => CompareValues(actualValue, expectedValue) > 0,
            "lt" => CompareValues(actualValue, expectedValue) < 0,
            "ge" => CompareValues(actualValue, expectedValue) >= 0,
            "le" => CompareValues(actualValue, expectedValue) <= 0,
            _ => false
        };
    }

    private Task<QueryResult> DefineVariableAsync(ParsedQuery query)
    {
        if (string.IsNullOrWhiteSpace(query.VariableName))
        {
            return Task.FromResult(new QueryResult
            {
                Success = false,
                Error = "Nom de variable manquant"
            });
        }

        _variableManager.DefineVariable(query.VariableName, query.VariableValue!);

        return Task.FromResult(new QueryResult
        {
            Success = true,
            Message = $"Variable '{query.VariableName}' définie avec la valeur : {query.VariableValue}",
            Data = new { VariableName = query.VariableName, Value = query.VariableValue }
        });
    }

    /// <summary>
    /// Remplace les variables dans une requête parsée avec gestion complète et robuste
    /// </summary>
    private void ReplaceVariablesInParsedQuery(ParsedQuery query)
    {
        try
        {
            // Remplacer les variables dans les propriétés de base
            query.Properties = _variableManager.ReplaceVariablesInProperties(query.Properties);
            
            // Remplacer les variables dans les nœuds source et destination
            if (!string.IsNullOrEmpty(query.FromNode))
            {
                query.FromNode = _variableManager.ReplaceVariables(query.FromNode);
            }
            
            if (!string.IsNullOrEmpty(query.ToNode))
            {
                query.ToNode = _variableManager.ReplaceVariables(query.ToNode);
            }
            
            // Remplacer les variables dans le type d'arête
            if (!string.IsNullOrEmpty(query.EdgeType))
            {
                query.EdgeType = _variableManager.ReplaceVariables(query.EdgeType);
            }
            
            // Remplacer les variables dans le label de nœud
            if (!string.IsNullOrEmpty(query.NodeLabel))
            {
                query.NodeLabel = _variableManager.ReplaceVariables(query.NodeLabel);
            }
            
            // Remplacer les variables dans les conditions avec gestion améliorée
            var updatedConditions = new Dictionary<string, object>();
            foreach (var kvp in query.Conditions)
            {
                var key = kvp.Key;
                var value = kvp.Value;
                
                // Remplacer les variables dans la clé si nécessaire
                if (key.Contains("$"))
                {
                    // Ne pas remplacer les variables dans les clés de propriétés
                    // Les clés comme "role" ne doivent pas être remplacées
                    if (!key.StartsWith("$"))
                    {
                        key = _variableManager.ReplaceVariables(key);
                    }
                }
                
                // Remplacer les variables dans la valeur
                if (value is string stringValue && stringValue.Contains("$"))
                {
                    value = _variableManager.ReplaceVariables(stringValue);
                }
                else if (value is Dictionary<string, object> dictValue)
                {
                    // Récursivement remplacer les variables dans les dictionnaires imbriqués
                    value = _variableManager.ReplaceVariablesInProperties(dictValue);
                }
                
                updatedConditions[key] = value;
            }
            query.Conditions = updatedConditions;
            
            // Remplacer les variables dans les propriétés d'agrégation
            if (!string.IsNullOrEmpty(query.AggregateProperty))
            {
                query.AggregateProperty = _variableManager.ReplaceVariables(query.AggregateProperty);
            }
            
            // Remplacer les variables dans les valeurs numériques
            if (query.BatchSize.HasValue && query.BatchSize.Value <= 0)
            {
                // Si la taille du batch n'est pas définie, essayer de l'extraire des variables
                var batchSizeVar = query.Properties.GetValueOrDefault("batchSize")?.ToString();
                if (!string.IsNullOrEmpty(batchSizeVar) && int.TryParse(batchSizeVar, out var size))
                {
                    query.BatchSize = size;
                }
            }
            
            // Gérer les sous-requêtes dans les conditions
            foreach (var kvp in query.Conditions.ToList())
            {
                var conditionKey = kvp.Key;
                var conditionValue = kvp.Value;
                
                // Vérifier si la condition contient une sous-requête
                if (conditionValue is string stringCondition && 
                    (stringCondition.Contains("find") || stringCondition.Contains("count") || stringCondition.Contains("aggregate")))
                {
                    // Traiter comme une sous-requête potentielle
                    var subQuery = ExtractSubQueryFromCondition(conditionKey, conditionValue);
                    if (subQuery != null)
                    {
                        // Remplacer les variables dans la sous-requête
                        ReplaceVariablesInParsedQuery(subQuery);
                        query.Conditions[conditionKey] = subQuery;
                    }
                }
            }
            
            // Gérer les variables dans les opérations en lots
            if (query.Type == QueryType.BatchOperation)
            {
                // Remplacer les variables dans les opérations batch
                foreach (var batchOp in query.BatchOperations)
                {
                    ReplaceVariablesInParsedQuery(batchOp);
                }
            }
            
            // Gérer les variables dans les sous-requêtes
            foreach (var subQuery in query.SubQueries)
            {
                ReplaceVariablesInParsedQuery(subQuery);
            }
        }
        catch (Exception ex)
        {
            // En cas d'erreur, continuer sans remplacer les variables
            // Cela évite de casser les requêtes si une variable est mal formée
        }
    }

    /// <summary>
    /// Récupère toutes les variables définies
    /// </summary>
    public Dictionary<string, object> GetVariables()
    {
        return _variableManager.GetAllVariables();
    }

    public void ClearVariables()
    {
        _variableManager.ClearVariables();
    }

    private bool IsModifyingOperation(QueryType type)
    {
        return type is QueryType.CreateNode or QueryType.CreateEdge or 
               QueryType.UpdateNode or QueryType.UpdateEdge or 
               QueryType.DeleteNode or QueryType.DeleteEdge or
               QueryType.BatchOperation;
    }

    private List<Node> FindShortestPath(Guid fromId, Guid toId)
    {
        if (fromId == toId)
        {
            var singleNode = _storage.GetNode(fromId);
            return singleNode != null ? new List<Node> { singleNode } : new List<Node>();
        }

        var visited = new HashSet<Guid>();
        var queue = new Queue<(Guid nodeId, List<Guid> path)>();
        queue.Enqueue((fromId, new List<Guid> { fromId }));

        while (queue.Count > 0)
        {
            var (currentId, path) = queue.Dequeue();

            if (visited.Contains(currentId))
                continue;

            visited.Add(currentId);

            if (currentId == toId)
            {
                return path.Select(id => _storage.GetNode(id))
                          .Where(n => n != null)
                          .ToList()!;
            }

            var edges = _storage.GetEdgesForNode(currentId);
            foreach (var edge in edges)
            {
                var nextId = edge.GetOtherNode(currentId);
                if (!visited.Contains(nextId))
                {
                    var newPath = new List<Guid>(path) { nextId };
                    queue.Enqueue((nextId, newPath));
                }
            }
        }

        return new List<Node>();
    }

    /// <summary>
    /// Trouve un chemin avancé avec support des types d'arêtes, évitement et bidirectionnalité
    /// </summary>
    private List<Node> FindAdvancedPath(Guid fromId, Guid toId, string? viaEdgeType, string? avoidEdgeType, int maxSteps, bool isBidirectional)
    {
        if (fromId == toId)
        {
            var singleNode = _storage.GetNode(fromId);
            return singleNode != null ? new List<Node> { singleNode } : new List<Node>();
        }

        var visited = new HashSet<Guid>();
        var queue = new Queue<(Guid nodeId, List<Guid> path, int steps)>();
        queue.Enqueue((fromId, new List<Guid> { fromId }, 0));

        while (queue.Count > 0)
        {
            var (currentId, path, steps) = queue.Dequeue();

            if (visited.Contains(currentId) || steps >= maxSteps)
                continue;

            visited.Add(currentId);

            if (currentId == toId)
            {
                return path.Select(id => _storage.GetNode(id))
                          .Where(n => n != null)
                          .ToList()!;
            }

            var edges = _storage.GetEdgesForNode(currentId);
            foreach (var edge in edges)
            {
                // Vérifier le type d'arête à éviter
                if (!string.IsNullOrEmpty(avoidEdgeType) && 
                    edge.RelationType.Equals(avoidEdgeType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                // Vérifier le type d'arête spécifique à utiliser
                if (!string.IsNullOrEmpty(viaEdgeType) && 
                    !edge.RelationType.Equals(viaEdgeType, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var nextId = edge.GetOtherNode(currentId);
                if (!visited.Contains(nextId))
                {
                    var newPath = new List<Guid>(path) { nextId };
                    queue.Enqueue((nextId, newPath, steps + 1));
                }
            }
        }

        // Si bidirectionnel et pas de chemin trouvé, essayer dans l'autre sens
        if (isBidirectional)
        {
            return FindAdvancedPath(toId, fromId, viaEdgeType, avoidEdgeType, maxSteps, false);
        }

        return new List<Node>();
    }

    /// <summary>
    /// Trouve tous les nœuds d'un label donné dans un nombre limité d'étapes depuis un nœud source
    /// </summary>
    private List<Node> FindNodesWithinSteps(Guid fromId, string targetLabel, int maxSteps)
    {
        var result = new List<Node>();
        var visited = new HashSet<Guid>();
        var queue = new Queue<(Guid nodeId, int currentStep)>();
        
        queue.Enqueue((fromId, 0));

        while (queue.Count > 0)
        {
            var (currentId, currentStep) = queue.Dequeue();

            if (visited.Contains(currentId) || currentStep > maxSteps)
                continue;

            visited.Add(currentId);

            var currentNode = _storage.GetNode(currentId);
            if (currentNode != null && currentStep > 0 && // Exclure le nœud de départ
                currentNode.Label.Equals(targetLabel, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(currentNode);
            }

            // Continuer la recherche si nous n'avons pas atteint la limite d'étapes
            if (currentStep < maxSteps)
            {
                var edges = _storage.GetEdgesForNode(currentId);
                foreach (var edge in edges)
                {
                    var nextId = edge.GetOtherNode(currentId);
                    if (!visited.Contains(nextId))
                    {
                        queue.Enqueue((nextId, currentStep + 1));
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Trouve tous les nœuds d'un label donné dans un nombre limité d'étapes avec support des types d'arêtes
    /// </summary>
    private List<Node> FindNodesWithinStepsAdvanced(Guid fromId, string targetLabel, int maxSteps, string? viaEdgeType, string? avoidEdgeType)
    {
        var result = new List<Node>();
        var visited = new HashSet<Guid>();
        var queue = new Queue<(Guid nodeId, int currentStep)>();
        
        queue.Enqueue((fromId, 0));

        while (queue.Count > 0)
        {
            var (currentId, currentStep) = queue.Dequeue();

            if (visited.Contains(currentId) || currentStep > maxSteps)
                continue;

            visited.Add(currentId);

            var currentNode = _storage.GetNode(currentId);
            if (currentNode != null && currentStep > 0 && // Exclure le nœud de départ
                currentNode.Label.Equals(targetLabel, StringComparison.OrdinalIgnoreCase))
            {
                result.Add(currentNode);
            }

            // Continuer la recherche si nous n'avons pas atteint la limite d'étapes
            if (currentStep < maxSteps)
            {
                var edges = _storage.GetEdgesForNode(currentId);
                foreach (var edge in edges)
                {
                    // Vérifier le type d'arête à éviter
                    if (!string.IsNullOrEmpty(avoidEdgeType) && 
                        edge.RelationType.Equals(avoidEdgeType, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    // Vérifier le type d'arête spécifique à utiliser
                    if (!string.IsNullOrEmpty(viaEdgeType) && 
                        !edge.RelationType.Equals(viaEdgeType, StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    var nextId = edge.GetOtherNode(currentId);
                    if (!visited.Contains(nextId))
                    {
                        queue.Enqueue((nextId, currentStep + 1));
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Calcule la distance entre deux nœuds (nombre d'étapes minimum)
    /// </summary>
    private int GetDistanceBetweenNodes(Guid fromId, Guid toId, int maxSteps)
    {
        if (fromId == toId) return 0;

        var visited = new HashSet<Guid>();
        var queue = new Queue<(Guid nodeId, int distance)>();
        
        queue.Enqueue((fromId, 0));

        while (queue.Count > 0)
        {
            var (currentId, distance) = queue.Dequeue();

            if (visited.Contains(currentId) || distance >= maxSteps)
                continue;

            visited.Add(currentId);

            if (currentId == toId)
                return distance;

            var edges = _storage.GetEdgesForNode(currentId);
            foreach (var edge in edges)
            {
                var nextId = edge.GetOtherNode(currentId);
                if (!visited.Contains(nextId))
                {
                    queue.Enqueue((nextId, distance + 1));
                }
            }
        }

        return -1; // Pas de chemin trouvé dans la limite
    }

    /// <summary>
    /// Compare deux valeurs pour l'égalité avec gestion sécurisée des nulls
    /// </summary>
    private bool CompareForEquality(object? actual, object expected)
    {
        if (actual == null && expected == null) return true;
        if (actual == null || expected == null) return false;

        // Comparaison spéciale pour les chaînes (insensible à la casse)
        if (actual is string actualStr && expected is string expectedStr)
        {
            return actualStr.Equals(expectedStr, StringComparison.OrdinalIgnoreCase);
        }

        // Comparaison spéciale pour les dates
        if (actual is DateTime actualDate && expected is DateTime expectedDate)
        {
            return actualDate.Date == expectedDate.Date; // Comparaison par date seulement
        }

        // Comparaison standard pour les autres types
        return Equals(actual, expected);
    }

    private int CompareValues(object? actual, object expected)
    {
        if (actual == null && expected == null) return 0;
        if (actual == null) return -1;
        if (expected == null) return 1;

        // Comparaison spéciale pour les dates
        if (actual is DateTime actualDate && expected is DateTime expectedDate)
        {
            return actualDate.CompareTo(expectedDate);
        }

        // Conversion en types numériques si possible
        if (actual is IComparable comparableActual && expected is IComparable comparableExpected)
        {
            if (actual.GetType() == expected.GetType())
            {
                return comparableActual.CompareTo(comparableExpected);
            }
            
            // Conversion pour les types numériques mixtes
            if (IsNumericType(actual) && IsNumericType(expected))
            {
                var actualDouble = Convert.ToDouble(actual);
                var expectedDouble = Convert.ToDouble(expected);
                return actualDouble.CompareTo(expectedDouble);
            }
        }
        
        // Comparaison par chaîne en dernier recours
        return string.Compare(actual.ToString(), expected.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private bool IsNumericType(object value) => value is int or long or float or double or decimal;

    /// <summary>
    /// Exécute une sous-requête de manière asynchrone
    /// </summary>
    private async Task<QueryResult> ExecuteSubQueryAsync(ParsedQuery subQuery)
    {
        try
        {
            // Exécuter la sous-requête selon son type
            QueryResult result;
            
            if (subQuery.Type == QueryType.Aggregate)
            {
                // Pour les agrégations, utiliser la méthode d'agrégation
                result = await ExecuteAggregateAsync(subQuery);
            }
            else
            {
                // Pour les autres types, utiliser la méthode générique
                result = await ExecuteParsedQueryAsync(subQuery);
            }
            
            return result;
        }
        catch (Exception ex)
        {
            return new QueryResult
            {
                Success = false,
                Error = ex.Message
            };
        }
    }

    /// <summary>
    /// Extrait les valeurs d'une sous-requête
    /// </summary>
    private List<object> ExtractSubQueryValues(QueryResult subQueryResult)
    {
        var values = new List<object>();
        
        // Vérifier d'abord si c'est une agrégation (données numériques)
        if (subQueryResult.Data is double numericValue)
        {
            values.Add(numericValue);
            return values;
        }
        else if (subQueryResult.Data is int intValue)
        {
            values.Add(intValue);
            return values;
        }
        else if (subQueryResult.Data is decimal decimalValue)
        {
            values.Add(decimalValue);
            return values;
        }
        else if (subQueryResult.Data is long longValue)
        {
            values.Add(longValue);
            return values;
        }
        else if (subQueryResult.Data is string stringValue)
        {
            values.Add(stringValue);
            return values;
        }
        
        // Si c'est une liste de nœuds
        if (subQueryResult.Data is List<Node> nodes)
        {
            // Pour les sous-requêtes de type FIND, extraire seulement la propriété spécifiée
            // ou toutes les propriétés si aucune n'est spécifiée
            foreach (var node in nodes)
            {
                // Vérifier si la sous-requête a une propriété spécifique
                if (subQueryResult.Message.Contains("property"))
                {
                    // Extraire seulement la propriété spécifiée
                    var propertyMatch = Regex.Match(subQueryResult.Message, @"property\s+(\w+)");
                    if (propertyMatch.Success)
                    {
                        var propertyName = propertyMatch.Groups[1].Value;
                        if (node.Properties.TryGetValue(propertyName, out var propertyValue))
                        {
                            values.Add(propertyValue);
                        }
                    }
                    else
                    {
                        // Si aucune propriété spécifique, extraire toutes les propriétés
                        foreach (var property in node.Properties)
                        {
                            values.Add(property.Value);
                        }
                    }
                }
                else
                {
                    // Pour les sous-requêtes COUNT ou autres, extraire toutes les propriétés
                    foreach (var property in node.Properties)
                    {
                        values.Add(property.Value);
                    }
                }
            }
        }
        
        // Si c'est une liste de valeurs (par exemple, des propriétés extraites par FindNodesAsync)
        else if (subQueryResult.Data is List<object> listResult)
        {
            values.AddRange(listResult);
            return values;
        }
        
        return values;
    }

    /// <summary>
    /// Évalue l'opérateur IN
    /// </summary>
    private bool EvaluateInOperator(object nodeValue, List<object> subQueryValues)
    {
        if (nodeValue == null) return false;
        
        return subQueryValues.Any(value => CompareForEquality(nodeValue, value));
    }

    /// <summary>
    /// Évalue l'opérateur EXISTS pour les sous-requêtes
    /// </summary>
    private bool EvaluateExistsOperator(List<object> subQueryValues)
    {
        return subQueryValues.Count > 0;
    }

    public void Dispose()
    {
        ClearSubQueryCache();
    }


    
    /// <summary>
    /// Extrait une sous-requête d'une condition
    /// </summary>
    private ParsedQuery? ExtractSubQueryFromCondition(string conditionKey, object expectedValue)
    {
        try
        {
            if (expectedValue is ParsedQuery subQuery)
            {
                return subQuery;
            }
            
            // Tenter de créer une sous-requête à partir de la valeur
            if (expectedValue is string queryString)
            {
                return _parser.Parse(queryString);
            }
            
            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parse une sous-requête depuis une chaîne de caractères
    /// </summary>
    private ParsedQuery? ParseSubQueryFromString(string query)
    {
        try
        {
            var parsedQuery = new ParsedQuery { Type = QueryType.SubQuery };
            ParseSubQueryFromString(query, parsedQuery);
            return parsedQuery;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parse une sous-requête depuis une chaîne de caractères (version avec ParsedQuery en paramètre)
    /// </summary>
    private void ParseSubQueryFromString(string query, ParsedQuery parsedQuery)
    {
        // Patterns pour sous-requêtes avec support amélioré des agrégations
        var patterns = new[]
        {
            // Pattern 1: "select [property] from [label] where [conditions]"
            @"select\s+(\w+|\*)\s+from\s+(\w+)(?:\s+where\s+(.+))?",
            // Pattern 2: "find [all] [label] where [conditions]"
            @"find\s+(?:all\s+)?(\w+)(?:\s+where\s+(.+))?",
            // Pattern 3: "count [all] [label] where [conditions]"
            @"count\s+(?:all\s+)?(\w+)(?:\s+where\s+(.+))?",
            // Pattern 4: "[aggregate_function]([property]) from [label] where [conditions]"
            @"(sum|avg|min|max|count)\s*\(\s*(\w+)\s*\)\s+from\s+(\w+)(?:\s+where\s+(.+))?",
            // Pattern 5: "select avg [property] from [label] where [conditions]" (sans parenthèses)
            @"select\s+(sum|avg|min|max|count)\s+(\w+)\s+from\s+(\w+)(?:\s+where\s+(.+))?",
            // Pattern 6: "avg [property] from [label] where [conditions]" (format simple)
            @"(sum|avg|min|max|count)\s+(\w+)\s+from\s+(\w+)(?:\s+where\s+(.+))?"
        };

        foreach (var pattern in patterns)
        {
            var match = System.Text.RegularExpressions.Regex.Match(query, pattern, System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (pattern.Contains("select") && !pattern.Contains("select\\s+(sum|avg|min|max|count)"))
                {
                    // Pattern 1: SELECT
                    parsedQuery.SubQueryProperty = match.Groups[1].Value;
                    parsedQuery.NodeLabel = match.Groups[2].Value;
                    parsedQuery.Type = QueryType.FindNodes;
                    
                    if (match.Groups.Count > 3 && !string.IsNullOrEmpty(match.Groups[3].Value))
                    {
                        ParseConditionsFromString(match.Groups[3].Value, parsedQuery.Conditions);
                    }
                }
                else if (pattern.Contains("find"))
                {
                    // Pattern 2: FIND
                    parsedQuery.NodeLabel = match.Groups[1].Value;
                    parsedQuery.Type = QueryType.FindNodes;
                    
                    if (match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[2].Value))
                    {
                        ParseConditionsFromString(match.Groups[2].Value, parsedQuery.Conditions);
                    }
                }
                else if (pattern.Contains("count"))
                {
                    // Pattern 3: COUNT
                    parsedQuery.NodeLabel = match.Groups[1].Value;
                    parsedQuery.Type = QueryType.Count;
                    
                    if (match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[2].Value))
                    {
                        ParseConditionsFromString(match.Groups[2].Value, parsedQuery.Conditions);
                    }
                }
                else if (pattern.Contains("\\("))
                {
                    // Pattern 4: AGGREGATE avec parenthèses
                    var functionName = match.Groups[1].Value.ToLower();
                    parsedQuery.AggregateFunction = functionName switch
                    {
                        "sum" => AggregateFunction.Sum,
                        "avg" => AggregateFunction.Avg,
                        "min" => AggregateFunction.Min,
                        "max" => AggregateFunction.Max,
                        "count" => AggregateFunction.Count,
                        _ => AggregateFunction.Count
                    };
                    
                    parsedQuery.AggregateProperty = match.Groups[2].Value;
                    parsedQuery.NodeLabel = match.Groups[3].Value;
                    parsedQuery.Type = QueryType.Aggregate;
                    
                    if (match.Groups.Count > 4 && !string.IsNullOrEmpty(match.Groups[4].Value))
                    {
                        ParseConditionsFromString(match.Groups[4].Value, parsedQuery.Conditions);
                    }
                }
                else
                {
                    // Patterns 5 et 6: AGGREGATE sans parenthèses
                    var functionName = match.Groups[1].Value.ToLower();
                    parsedQuery.AggregateFunction = functionName switch
                    {
                        "sum" => AggregateFunction.Sum,
                        "avg" => AggregateFunction.Avg,
                        "min" => AggregateFunction.Min,
                        "max" => AggregateFunction.Max,
                        "count" => AggregateFunction.Count,
                        _ => AggregateFunction.Count
                    };
                    
                    parsedQuery.AggregateProperty = match.Groups[2].Value;
                    parsedQuery.NodeLabel = match.Groups[3].Value;
                    parsedQuery.Type = QueryType.Aggregate;
                    
                    if (match.Groups.Count > 4 && !string.IsNullOrEmpty(match.Groups[4].Value))
                    {
                        ParseConditionsFromString(match.Groups[4].Value, parsedQuery.Conditions);
                    }
                }
                
                return;
            }
        }

        // Si aucun pattern ne correspond, essayer un parsing simple
        var simpleMatch = System.Text.RegularExpressions.Regex.Match(query, @"(\w+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (simpleMatch.Success)
        {
            parsedQuery.NodeLabel = simpleMatch.Groups[1].Value;
            parsedQuery.Type = QueryType.FindNodes;
        }
        else
        {
            throw new ArgumentException($"Format de sous-requête invalide : {query}");
        }
    }

    /// <summary>
    /// Parse les conditions depuis une chaîne de caractères
    /// </summary>
    private void ParseConditionsFromString(string conditionsText, Dictionary<string, object> conditions)
    {
        if (string.IsNullOrWhiteSpace(conditionsText))
            return;

        // Pattern simple pour les conditions : "property operator value"
        var match = System.Text.RegularExpressions.Regex.Match(conditionsText, @"(\w+)\s*([><=!]+)\s*(.+)");
        if (match.Success)
        {
            var property = match.Groups[1].Value;
            var operator_ = match.Groups[2].Value;
            var value = match.Groups[3].Value.Trim('"', '\'');
            
            var normalizedOperator = operator_ switch
            {
                "=" => "eq",
                "!=" => "ne",
                ">" => "gt",
                ">=" => "ge",
                "<" => "lt",
                "<=" => "le",
                _ => operator_.ToLowerInvariant()
            };
            
            conditions[$"{property}_{normalizedOperator}"] = value;
        }
    }

    /// <summary>
    /// Obtient la valeur d'un nœud pour une condition donnée
    /// </summary>
    private object? GetNodeValueForCondition(Node node, string conditionKey)
    {
        // Extraire la propriété de la clé de condition
        var keyParts = conditionKey.Split('_');
        var property = keyParts[0];
        
        return node.GetProperty<object>(property);
    }
    

    
    /// <summary>
    /// Améliore l'opérateur de comptage avec support de comparaisons avancées
    /// </summary>
    private bool EvaluateCountOperator(List<object> subQueryValues, string @operator, int comparisonValue)
    {
        var count = subQueryValues.Count;
        
        return @operator switch
        {
            ">" => count > comparisonValue,
            "<" => count < comparisonValue,
            "=" => count == comparisonValue,
            ">=" => count >= comparisonValue,
            "<=" => count <= comparisonValue,
            "!=" => count != comparisonValue,
            _ => count == comparisonValue // Par défaut
        };
    }
    
    /// <summary>
    /// Améliore l'extraction de la valeur de comparaison avec support de formats avancés
    /// </summary>
    private int ExtractComparisonValue(string conditionKey)
    {
        try
        {
            // Formats supportés: count_gt_5, count_lt_10, etc.
            var parts = conditionKey.Split('_');
            if (parts.Length >= 3 && int.TryParse(parts[2], out var value))
            {
                return value;
            }
            
            // Format alternatif: count_gt=5
            var equalIndex = conditionKey.IndexOf('=');
            if (equalIndex > 0 && int.TryParse(conditionKey.Substring(equalIndex + 1), out var altValue))
            {
                return altValue;
            }
            
            return 0;
        }
        catch
        {
            return 0;
        }
    }
    
    /// <summary>
    /// Améliore l'opérateur ANY avec support de collections complexes
    /// </summary>
    private bool EvaluateAnyOperator(object? nodeValue, List<object> subQueryValues)
    {
        if (nodeValue == null || !subQueryValues.Any()) return false;
        
        // Support des collections
        if (nodeValue is IEnumerable<object> nodeCollection)
        {
            return nodeCollection.Any(nodeItem => 
                subQueryValues.Any(subValue => CompareForEquality(nodeItem, subValue)));
        }
        
        // Support des chaînes avec séparateurs
        if (nodeValue is string nodeString)
        {
            var nodeItems = nodeString.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            return nodeItems.Any(nodeItem => 
                subQueryValues.Any(subValue => CompareForEquality(nodeItem.Trim(), subValue)));
        }
        
        // Support des tableaux
        if (nodeValue.GetType().IsArray)
        {
            var array = (Array)nodeValue;
            for (int i = 0; i < array.Length; i++)
            {
                var arrayItem = array.GetValue(i);
                if (subQueryValues.Any(subValue => CompareForEquality(arrayItem, subValue)))
                {
                    return true;
                }
            }
        }
        
        // Comparaison directe
        return subQueryValues.Any(subValue => CompareForEquality(nodeValue, subValue));
    }
    
    /// <summary>
    /// Améliore l'opérateur ALL avec support de collections complexes
    /// </summary>
    private bool EvaluateAllOperator(object? nodeValue, List<object> subQueryValues)
    {
        if (nodeValue == null || !subQueryValues.Any()) return false;
        
        // Support des collections
        if (nodeValue is IEnumerable<object> nodeCollection)
        {
            var nodeItems = nodeCollection.ToList();
            return nodeItems.All(nodeItem => 
                subQueryValues.Any(subValue => CompareForEquality(nodeItem, subValue)));
        }
        
        // Support des chaînes avec séparateurs
        if (nodeValue is string nodeString)
        {
            var nodeItems = nodeString.Split(new[] { ',', ';', '|' }, StringSplitOptions.RemoveEmptyEntries);
            return nodeItems.All(nodeItem => 
                subQueryValues.Any(subValue => CompareForEquality(nodeItem.Trim(), subValue)));
        }
        
        // Support des tableaux
        if (nodeValue.GetType().IsArray)
        {
            var array = (Array)nodeValue;
            for (int i = 0; i < array.Length; i++)
            {
                var arrayItem = array.GetValue(i);
                if (!subQueryValues.Any(subValue => CompareForEquality(arrayItem, subValue)))
                {
                    return false;
                }
            }
            return true;
        }
        
        // Pour les valeurs simples, vérifier si la valeur est dans la liste
        return subQueryValues.Any(subValue => CompareForEquality(nodeValue, subValue));
    }

    /// <summary>
    /// Méthode synchrone pour maintenir la compatibilité avec le code existant
    /// </summary>
    private List<Node> FilterNodesByConditions(List<Node> nodes, Dictionary<string, object> conditions)
    {
        return FilterNodesByConditionsAsync(nodes, conditions).Result;
    }

    private Task<QueryResult> UpdateEdgeAsync(ParsedQuery query)
    {
        try
        {
            // Parser les références de nœuds
            var fromNodeInfo = ParseNodeReference(query.FromNode!);
            var toNodeInfo = ParseNodeReference(query.ToNode!);
            
            var fromNodes = _storage.GetAllNodes()
                .Where(n => MatchesNodeReference(n, fromNodeInfo))
                .ToList();

            var toNodes = _storage.GetAllNodes()
                .Where(n => MatchesNodeReference(n, toNodeInfo))
                .ToList();

            if (!fromNodes.Any())
                return Task.FromResult(new QueryResult { Success = false, Error = $"Nœud source '{query.FromNode}' introuvable" });

            if (!toNodes.Any())
                return Task.FromResult(new QueryResult { Success = false, Error = $"Nœud destination '{query.ToNode}' introuvable" });

            var fromNodeId = fromNodes.First().Id;
            var toNodeId = toNodes.First().Id;

            // Trouver les arêtes à mettre à jour
            var allEdges = _storage.GetAllEdges();
            var edgesToUpdate = allEdges.Where(e => 
                (e.FromNodeId == fromNodeId && e.ToNodeId == toNodeId) ||
                (e.FromNodeId == toNodeId && e.ToNodeId == fromNodeId)
            ).ToList();

            // Appliquer les conditions si présentes
            if (query.Conditions.Any())
            {
                edgesToUpdate = FilterEdgesByConditions(edgesToUpdate, query.Conditions);
            }

            if (!edgesToUpdate.Any())
            {
                return Task.FromResult(new QueryResult
                {
                    Success = false,
                    Error = "Aucune arête trouvée correspondant aux critères"
                });
            }

            int updatedCount = 0;
            foreach (var edge in edgesToUpdate)
            {
                // Mettre à jour les propriétés
                foreach (var property in query.Properties)
                {
                    edge.SetProperty(property.Key, property.Value);
                }
                updatedCount++;
            }

            return Task.FromResult(new QueryResult
            {
                Success = true,
                Message = $"{updatedCount} arête(s) mise(s) à jour"
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new QueryResult
            {
                Success = false,
                Error = $"Erreur lors de la mise à jour d'arête : {ex.Message}"
            });
        }
    }

    private Task<QueryResult> ExecuteEdgeAggregateAsync(ParsedQuery query)
    {
        try
        {
            if (query.AggregateFunction == null || string.IsNullOrEmpty(query.AggregateProperty))
            {
                return Task.FromResult(new QueryResult
                {
                    Success = false,
                    Error = "Fonction d'agrégation ou propriété manquante pour les arêtes"
                });
            }

            var allEdges = _storage.GetAllEdges();
            var filteredEdges = new List<Edge>();

            // Appliquer les filtres selon les propriétés de la requête
            foreach (var edge in allEdges)
            {
                bool includeEdge = true;

                // Filtre par type d'arête
                if (!string.IsNullOrEmpty(query.EdgeType))
                {
                    if (!edge.RelationType.Equals(query.EdgeType, StringComparison.OrdinalIgnoreCase))
                    {
                        includeEdge = false;
                    }
                }

                // Filtre par type d'arête à éviter
                if (query.Properties.ContainsKey("avoid_edge_type"))
                {
                    var avoidEdgeType = query.Properties["avoid_edge_type"].ToString();
                    if (edge.RelationType.Equals(avoidEdgeType, StringComparison.OrdinalIgnoreCase))
                    {
                        includeEdge = false;
                    }
                }

                // Filtre par labels de nœuds source et destination
                if (query.Properties.ContainsKey("from_label") || query.Properties.ContainsKey("to_label"))
                {
                    var fromLabel = query.Properties.GetValueOrDefault("from_label")?.ToString();
                    var toLabel = query.Properties.GetValueOrDefault("to_label")?.ToString();
                    
                    var fromNode = _storage.GetNode(edge.FromNodeId);
                    var toNode = _storage.GetNode(edge.ToNodeId);
                    
                    if (fromNode != null && toNode != null)
                    {
                        if (!string.IsNullOrEmpty(fromLabel) && !fromNode.Label.Equals(fromLabel, StringComparison.OrdinalIgnoreCase))
                        {
                            includeEdge = false;
                        }
                        if (!string.IsNullOrEmpty(toLabel) && !toNode.Label.Equals(toLabel, StringComparison.OrdinalIgnoreCase))
                        {
                            includeEdge = false;
                        }
                    }
                }

                // Filtre par label de nœud connecté
                if (query.Properties.ContainsKey("connected_to_label"))
                {
                    var connectedToLabel = query.Properties["connected_to_label"].ToString();
                    var fromNode = _storage.GetNode(edge.FromNodeId);
                    var toNode = _storage.GetNode(edge.ToNodeId);
                    
                    bool hasConnectedNode = false;
                    if (fromNode != null && fromNode.Label.Equals(connectedToLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        hasConnectedNode = true;
                    }
                    if (toNode != null && toNode.Label.Equals(connectedToLabel, StringComparison.OrdinalIgnoreCase))
                    {
                        hasConnectedNode = true;
                    }
                    
                    if (!hasConnectedNode)
                    {
                        includeEdge = false;
                    }
                }

                // Filtre par nombre maximum d'étapes (pour les chemins)
                if (query.MaxSteps.HasValue)
                {
                    // Pour les agrégations avec max steps, on peut filtrer par distance
                    // Cette logique peut être étendue selon les besoins
                }

                // Appliquer les conditions WHERE si présentes
                if (includeEdge && query.Conditions.Any())
                {
                    includeEdge = FilterEdgesByConditions(new List<Edge> { edge }, query.Conditions).Any();
                }

                if (includeEdge)
                {
                    filteredEdges.Add(edge);
                }
            }

            // Extraire les valeurs numériques de la propriété spécifiée
            var numericValues = new List<double>();
            var missingPropertyCount = 0;
            var nonNumericCount = 0;

            foreach (var edge in filteredEdges)
            {
                if (edge.Properties.TryGetValue(query.AggregateProperty, out var value))
                {
                    if (TryConvertToDouble(value, out double numericValue))
                    {
                        numericValues.Add(numericValue);
                    }
                    else
                    {
                        nonNumericCount++;
                    }
                }
                else
                {
                    missingPropertyCount++;
                }
            }

            // Gestion intelligente des cas vides ou non numériques
            bool isEmpty = !numericValues.Any();
            object? result = null;
            string functionName = query.AggregateFunction.ToString().ToLowerInvariant();
            string message;

            if (isEmpty)
            {
                switch (query.AggregateFunction)
                {
                    case AggregateFunction.Sum:
                    case AggregateFunction.Count:
                        result = 0;
                        break;
                    case AggregateFunction.Avg:
                    case AggregateFunction.Min:
                    case AggregateFunction.Max:
                        result = null;
                        break;
                    default:
                        result = null;
                        break;
                }

                // Message détaillé pour le debug
                var details = new List<string>();
                if (missingPropertyCount > 0) details.Add($"{missingPropertyCount} arête(s) sans propriété '{query.AggregateProperty}'");
                if (nonNumericCount > 0) details.Add($"{nonNumericCount} arête(s) avec valeur non numérique");
                if (filteredEdges.Count == 0) details.Add("Aucune arête trouvée");

                var detailMessage = details.Any() ? $" ({string.Join(", ", details)})" : "";
                message = $"Aucune valeur numérique trouvée pour la propriété '{query.AggregateProperty}' sur les arêtes{detailMessage}";
            }
            else
            {
                result = query.AggregateFunction switch
                {
                    AggregateFunction.Sum => numericValues.Sum(),
                    AggregateFunction.Avg => numericValues.Average(),
                    AggregateFunction.Min => numericValues.Min(),
                    AggregateFunction.Max => numericValues.Max(),
                    AggregateFunction.Count => numericValues.Count,
                    _ => throw new NotSupportedException($"Fonction d'agrégation non supportée : {query.AggregateFunction}")
                };

                var detailMessage = "";
                if (missingPropertyCount > 0 || nonNumericCount > 0)
                {
                    var details = new List<string>();
                    if (missingPropertyCount > 0) details.Add($"{missingPropertyCount} sans propriété");
                    if (nonNumericCount > 0) details.Add($"{nonNumericCount} non numériques");
                    detailMessage = $" (ignoré: {string.Join(", ", details)})";
                }

                message = $"{functionName.ToUpperInvariant()}({query.AggregateProperty}) sur {numericValues.Count} arête(s) = {result}{detailMessage}";
            }

            return Task.FromResult(new QueryResult
            {
                Success = true,
                Message = message,
                Data = result
            });
        }
        catch (Exception ex)
        {
            return Task.FromResult(new QueryResult
            {
                Success = false,
                Error = $"Erreur lors de l'agrégation sur les arêtes : {ex.Message}"
            });
        }
    }

    private Task<QueryResult> ExecuteAggregateAsync(ParsedQuery query)
    {
        // Remplacer les variables dans la requête avant traitement
        ReplaceVariablesInParsedQuery(query);
        
        if (query.AggregateFunction == null || string.IsNullOrEmpty(query.AggregateProperty))
        {
            return Task.FromResult(new QueryResult
            {
                Success = false,
                Error = "Fonction d'agrégation ou propriété manquante"
            });
        }

        // Vérifier si c'est une agrégation sur les arêtes
        if (query.Properties.ContainsKey("aggregate_edges"))
        {
            return ExecuteEdgeAggregateAsync(query);
        }
        
        var nodes = _storage.GetNodesByLabel(query.NodeLabel!);
        
        // Appliquer les conditions WHERE si présentes
        if (query.Conditions.Any())
        {
            nodes = FilterNodesByConditions(nodes, query.Conditions);
        }

        // Extraire les valeurs numériques de la propriété spécifiée
        var numericValues = new List<double>();
        var missingPropertyCount = 0;
        var nonNumericCount = 0;
        
        foreach (var node in nodes)
        {
            if (node.Properties.TryGetValue(query.AggregateProperty, out var value))
            {
                if (TryConvertToDouble(value, out double numericValue))
                {
                    numericValues.Add(numericValue);
                }
                else
                {
                    nonNumericCount++;
                }
            }
            else
            {
                missingPropertyCount++;
            }
        }

        // Gestion intelligente des cas vides ou non numériques
        bool isEmpty = !numericValues.Any();
        object? result = null;
        string functionName = query.AggregateFunction.ToString().ToLowerInvariant();
        string message;
        
        if (isEmpty)
        {
            switch (query.AggregateFunction)
            {
                case AggregateFunction.Sum:
                case AggregateFunction.Count:
                    result = 0;
                    break;
                case AggregateFunction.Avg:
                case AggregateFunction.Min:
                case AggregateFunction.Max:
                    result = null;
                    break;
                default:
                    result = null;
                    break;
            }
            
            // Message détaillé pour le debug
            var details = new List<string>();
            if (missingPropertyCount > 0) details.Add($"{missingPropertyCount} nœud(s) sans propriété '{query.AggregateProperty}'");
            if (nonNumericCount > 0) details.Add($"{nonNumericCount} nœud(s) avec valeur non numérique");
            if (nodes.Count == 0) details.Add("Aucun nœud trouvé");
            
            var detailMessage = details.Any() ? $" ({string.Join(", ", details)})" : "";
            message = $"Aucune valeur numérique trouvée pour la propriété '{query.AggregateProperty}' sur les nœuds de type '{query.NodeLabel}'{detailMessage}";
        }
        else
        {
            result = query.AggregateFunction switch
            {
                AggregateFunction.Sum => numericValues.Sum(),
                AggregateFunction.Avg => numericValues.Average(),
                AggregateFunction.Min => numericValues.Min(),
                AggregateFunction.Max => numericValues.Max(),
                AggregateFunction.Count => numericValues.Count,
                _ => throw new NotSupportedException($"Fonction d'agrégation non supportée : {query.AggregateFunction}")
            };
            
            var detailMessage = "";
            if (missingPropertyCount > 0 || nonNumericCount > 0)
            {
                var details = new List<string>();
                if (missingPropertyCount > 0) details.Add($"{missingPropertyCount} sans propriété");
                if (nonNumericCount > 0) details.Add($"{nonNumericCount} non numériques");
                detailMessage = $" (ignoré: {string.Join(", ", details)})";
            }
            
            message = $"{functionName.ToUpperInvariant()}({query.AggregateProperty}) sur {numericValues.Count} nœud(s) de type '{query.NodeLabel}' = {result}{detailMessage}";
        }

        return Task.FromResult(new QueryResult
        {
            Success = true,
            Message = message,
            Data = result
        });
    }

    private async Task<QueryResult> ExecuteBatchOperationAsync(ParsedQuery query)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            var results = new List<BatchOperationResult>();
            
            // Exécuter les opérations batch selon le type
            if (query.BatchOperations != null && query.BatchOperations.Any())
            {
                foreach (var batchOp in query.BatchOperations)
                {
                    var result = await ExecuteParsedQueryAsync(batchOp);
                    results.Add(new BatchOperationResult
                    {
                        Success = result.Success,
                        Message = result.Message,
                        Error = result.Error,
                        Data = result.Data
                    });
                }
            }
            
            var endTime = DateTime.UtcNow;
            var totalTime = (endTime - startTime).TotalMilliseconds;
            
            return new QueryResult
            {
                Success = true,
                Message = $"Opérations batch terminées en {totalTime:F2}ms",
                Data = results
            };
        }
        catch (Exception ex)
        {
            return new QueryResult
            {
                Success = false,
                Error = $"Erreur lors de l'exécution des opérations batch : {ex.Message}"
            };
        }
    }

    private List<Edge> FilterEdgesByConditions(List<Edge> edges, Dictionary<string, object> conditions)
    {
        var filteredEdges = new List<Edge>();
        
        foreach (var edge in edges)
        {
            bool includeEdge = true;
            
            foreach (var condition in conditions)
            {
                if (!EvaluateEdgeCondition(edge, condition.Key, condition.Value))
                {
                    includeEdge = false;
                    break;
                }
            }
            
            if (includeEdge)
            {
                filteredEdges.Add(edge);
            }
        }
        
        return filteredEdges;
    }

    private List<Node> FilterNodesByConditionsSync(List<Node> nodes, Dictionary<string, object> conditions)
    {
        var filteredNodes = new List<Node>();
        
        foreach (var node in nodes)
        {
            bool includeNode = true;
            
            foreach (var condition in conditions)
            {
                if (!EvaluateCondition(node, condition.Key, condition.Value))
                {
                    includeNode = false;
                    break;
                }
            }
            
            if (includeNode)
            {
                filteredNodes.Add(node);
            }
        }
        
        return filteredNodes;
    }

    private async Task<bool> EvaluateSubQueryConditionAsync(Node node, string conditionKey, ParsedQuery subQuery)
    {
        try
        {
            // Exécuter la sous-requête
            var subQueryResult = await ExecuteSubQueryAsync(subQuery);
            
            if (!subQueryResult.Success)
            {
                return false;
            }
            
            // Extraire les valeurs de la sous-requête
            var subQueryValues = ExtractSubQueryValues(subQueryResult);
            
            if (!subQueryValues.Any())
            {
                return false;
            }
            
            // Évaluer selon l'opérateur de la condition
            var nodeValue = GetNodeValueForCondition(node, conditionKey);
            
            // Déterminer l'opérateur de comparaison
            if (conditionKey.Contains("_in"))
            {
                return EvaluateInOperator(nodeValue, subQueryValues);
            }
            else if (conditionKey.Contains("_exists"))
            {
                return EvaluateExistsOperator(subQueryValues);
            }
            else if (conditionKey.Contains("_any"))
            {
                return EvaluateAnyOperator(nodeValue, subQueryValues);
            }
            else if (conditionKey.Contains("_all"))
            {
                return EvaluateAllOperator(nodeValue, subQueryValues);
            }
            else if (conditionKey.Contains("_count"))
            {
                var comparisonValue = ExtractComparisonValue(conditionKey);
                return EvaluateCountOperator(subQueryValues, "eq", comparisonValue);
            }
            else
            {
                // Comparaison directe avec la première valeur
                return CompareForEquality(nodeValue, subQueryValues.First());
            }
        }
        catch (Exception ex)
        {
            return false;
        }
    }

    public void ClearSubQueryCache()
    {
        _subQueryCache.Clear();
    }

    private readonly Dictionary<string, QueryResult> _subQueryCache = new();

    private bool TryConvertToDouble(object? value, out double result)
    {
        result = 0;
        
        if (value == null)
            return false;
            
        if (value is double d)
        {
            result = d;
            return true;
        }
        
        if (value is int i)
        {
            result = i;
            return true;
        }
        
        if (value is long l)
        {
            result = l;
            return true;
        }
        
        if (value is float f)
        {
            result = f;
            return true;
        }
        
        if (value is decimal dec)
        {
            result = (double)dec;
            return true;
        }
        
        if (value is string str)
        {
            return double.TryParse(str, out result);
        }
        
        return false;
    }

    private bool EvaluateCondition(Node node, string conditionKey, object expectedValue)
    {
        try
        {
            var actualValue = GetNodeValueForCondition(node, conditionKey);
            
            if (actualValue == null && expectedValue == null)
                return true;
            
            if (actualValue == null || expectedValue == null)
                return false;
            
            // Comparaison directe
            return CompareForEquality(actualValue, expectedValue);
        }
        catch (Exception ex)
        {
            return false;
        }
    }
}
/// <summary>
/// Résultat d'une requête GraphQLite
/// </summary>
public class QueryResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
    public object? Data { get; set; }
}

/// <summary>
/// Classes de résultats améliorées pour les opérations batch
/// </summary>
public class BatchOperationResult
{
    public Guid OperationId { get; set; } = Guid.NewGuid();
    public string OperationType { get; set; } = "";
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public object? Data { get; set; }
    public string? NodeId { get; set; }
    public double ExecutionTime { get; set; }
    public int OperationIndex { get; set; }
    public Exception? Exception { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

public class BatchOperationSummary
{
    public BatchOperationMetrics Metrics { get; set; } = new();
    public List<object> Results { get; set; } = new();
    public List<string> Errors { get; set; } = new();
    public bool WasRolledBack { get; set; }
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public double TotalExecutionTime { get; set; }
}

public class BatchOperationMetrics
{
    public int TotalOperations { get; set; }
    public int SuccessfulOperations { get; set; }
    public int FailedOperations { get; set; }
    public double TotalExecutionTime { get; set; }
    public double AverageOperationTime { get; set; }
    public DateTime StartTime { get; set; } = DateTime.UtcNow;
    public DateTime? EndTime { get; set; }
}

public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
}


