using GraphQLite.Models;
using GraphQLite.Storage;
using GraphQLite.Query;
using System.Text.RegularExpressions;

namespace GraphQLite.Engine;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Moteur principal de la base de données GraphQLite
/// Orchestre les opérations de requête et de stockage
/// </summary>
public class GraphQLiteEngine : IDisposable
{
    private readonly GraphStorage _storage;
    private readonly NaturalLanguageParser _parser;

    public GraphQLiteEngine(string databasePath)
    {
        _storage = new GraphStorage(databasePath);
        _parser = new NaturalLanguageParser();
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
            QueryType.FindPath => FindPathAsync(query),
            QueryType.FindWithinSteps => FindWithinStepsAsync(query),
            QueryType.UpdateNode => UpdateNodeAsync(query),
            QueryType.DeleteNode => DeleteNodeAsync(query),
            QueryType.DeleteEdge => DeleteEdgeAsync(query),
            QueryType.Count => CountNodesAsync(query),
            QueryType.Aggregate => ExecuteAggregateAsync(query),
            QueryType.ShowSchema => ShowSchemaAsync(),
            _ => throw new NotSupportedException($"Type de requête non supporté : {query.Type}")
        };
    }

    private Task<QueryResult> CreateNodeAsync(ParsedQuery query)
    {
        var node = new Node(query.NodeLabel!, query.Properties);
        _storage.AddNode(node);

        return Task.FromResult(new QueryResult
        {
            Success = true,
            Message = $"Nœud créé avec l'ID : {node.Id}",
            Data = new { NodeId = node.Id, Node = node }
        });
    }

    private Task<QueryResult> CreateEdgeAsync(ParsedQuery query)
    {
        // Chercher les nœuds par nom (propriété "name")
        var fromNodes = _storage.GetAllNodes()
            .Where(n => n.GetProperty<string>("name")?.Equals(query.FromNode, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        var toNodes = _storage.GetAllNodes()
            .Where(n => n.GetProperty<string>("name")?.Equals(query.ToNode, StringComparison.OrdinalIgnoreCase) == true)
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

    private Task<QueryResult> FindNodesAsync(ParsedQuery query)
    {
        var nodes = _storage.GetNodesByLabel(query.NodeLabel!);

        // Appliquer les conditions
        if (query.Conditions.Any())
        {
            nodes = FilterNodesByConditions(nodes, query.Conditions);
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

        return Task.FromResult(new QueryResult
        {
            Success = true,
            Message = $"{nodes.Count} nœud(s) trouvé(s)",
            Data = nodes
        });
    }

    private Task<QueryResult> FindPathAsync(ParsedQuery query)
    {
        // Recherche de chemin simple (BFS)
        var fromNodes = _storage.GetAllNodes()
            .Where(n => n.GetProperty<string>("name")?.Equals(query.FromNode, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        var toNodes = _storage.GetAllNodes()
            .Where(n => n.GetProperty<string>("name")?.Equals(query.ToNode, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (!fromNodes.Any() || !toNodes.Any())
        {
            return Task.FromResult(new QueryResult 
            { 
                Success = false, 
                Error = "Nœuds source ou destination introuvables" 
            });
        }

        var path = FindShortestPath(fromNodes.First().Id, toNodes.First().Id);

        return Task.FromResult(new QueryResult
        {
            Success = true,
            Message = path.Any() ? $"Chemin trouvé avec {path.Count} nœuds" : "Aucun chemin trouvé",
            Data = path
        });
    }

    private Task<QueryResult> FindWithinStepsAsync(ParsedQuery query)
    {
        // Rechercher le nœud de départ par nom
        var fromNodes = _storage.GetAllNodes()
            .Where(n => n.GetProperty<string>("name")?.Equals(query.FromNode, StringComparison.OrdinalIgnoreCase) == true)
            .ToList();

        if (!fromNodes.Any())
        {
            return Task.FromResult(new QueryResult 
            { 
                Success = false, 
                Error = $"Nœud source '{query.FromNode}' introuvable" 
            });
        }

        var startNode = fromNodes.First();
        var maxSteps = query.MaxSteps ?? 1;

        List<Node> result;

        if (!string.IsNullOrEmpty(query.ToNode))
        {
            // Recherche avec nœud de destination spécifique
            var toNodes = _storage.GetAllNodes()
                .Where(n => n.GetProperty<string>("name")?.Equals(query.ToNode, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            if (!toNodes.Any())
            {
                return Task.FromResult(new QueryResult 
                { 
                    Success = false, 
                    Error = $"Nœud destination '{query.ToNode}' introuvable" 
                });
            }

            var targetNode = toNodes.First();
            result = FindNodesWithinSteps(startNode.Id, query.NodeLabel!, maxSteps)
                .Where(n => n.Id == targetNode.Id)
                .ToList();

            var message = result.Any() 
                ? $"Nœud '{query.ToNode}' trouvé à {GetDistanceBetweenNodes(startNode.Id, targetNode.Id, maxSteps)} étape(s) de '{query.FromNode}'"
                : $"Nœud '{query.ToNode}' non trouvé dans les {maxSteps} étapes depuis '{query.FromNode}'";

            return Task.FromResult(new QueryResult
            {
                Success = true,
                Message = message,
                Data = result
            });
        }
        else
        {
            // Recherche de tous les nœuds du type spécifié dans la limite d'étapes
            result = FindNodesWithinSteps(startNode.Id, query.NodeLabel!, maxSteps);

            return Task.FromResult(new QueryResult
            {
                Success = true,
                Message = $"{result.Count} nœud(s) de type '{query.NodeLabel}' trouvé(s) dans les {maxSteps} étapes depuis '{query.FromNode}'",
                Data = result
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

    private List<Node> FilterNodesByConditions(List<Node> nodes, Dictionary<string, object> conditions)
    {
        if (!conditions.Any()) return nodes;

        // DEBUG : Logs simplifiés
        Console.WriteLine($"DEBUG: Filtrage {nodes.Count} nœuds avec {conditions.Count} conditions:");
        foreach (var condition in conditions)
        {
            Console.WriteLine($"  - Condition: {condition.Key} = {condition.Value}");
        }

        return nodes.Where(node =>
        {
            // Séparer les conditions AND et OR
            var andConditions = new List<KeyValuePair<string, object>>();
            var orConditions = new List<KeyValuePair<string, object>>();

            foreach (var condition in conditions)
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
                andResult = andConditions.All(condition =>
                {
                    var result = EvaluateCondition(node, condition.Key, condition.Value);
                    Console.WriteLine($"    - Évaluation AND {condition.Key} sur nœud {node.GetProperty<string>("name")}: {result}");
                    return result;
                });
            }

            // Évaluer les conditions OR
            bool orResult = false;
            if (orConditions.Any())
            {
                orResult = orConditions.Any(condition =>
                {
                    var result = EvaluateCondition(node, condition.Key, condition.Value);
                    Console.WriteLine($"    - Évaluation OR {condition.Key} sur nœud {node.GetProperty<string>("name")}: {result}");
                    return result;
                });
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
            
            Console.WriteLine($"    - Résultat final pour nœud {node.GetProperty<string>("name")}: {finalResult} (AND: {andResult}, OR: {orResult})");

            return finalResult;
        }).ToList();
    }

    /// <summary>
    /// Évalue une condition individuelle sur un nœud
    /// </summary>
    private bool EvaluateCondition(Node node, string conditionKey, object expectedValue)
    {
        // Parser la clé de condition avec support des suffixes numériques pour les conditions OR
        var keyParts = conditionKey.Split('_');
        
        string property;
        string @operator;

        // Améliorer le parsing pour les opérateurs composés
        if (keyParts.Length == 2)
        {
            // Format: property_operator
            property = keyParts[0];
            @operator = keyParts[1];
        }
        else if (keyParts.Length == 3)
        {
            // Vérifier si c'est un opérateur composé ou And/Or_property_operator
            if (keyParts[0] == "And" || keyParts[0] == "Or")
            {
                // Format: And/Or_property_operator
                property = keyParts[1];
                @operator = keyParts[2];
            }
            else
            {
                // Format: property_operator1_operator2 (ex: name_starts_with)
                property = keyParts[0];
                @operator = $"{keyParts[1]}_{keyParts[2]}";
            }
        }
        else if (keyParts.Length == 4)
        {
            if (keyParts[0] == "And" || keyParts[0] == "Or")
            {
                // Format: And/Or_property_operator1_operator2 ou And/Or_property_operator_N
                if (int.TryParse(keyParts[3], out _))
                {
                    // Format: Or_property_operator_N
                    property = keyParts[1];
                    @operator = keyParts[2];
                }
                else
                {
                    // Format: And/Or_property_operator1_operator2
                    property = keyParts[1];
                    @operator = $"{keyParts[2]}_{keyParts[3]}";
                }
            }
            else
            {
                // Format: property_operator1_operator2_operator3 (rare)
                property = keyParts[0];
                @operator = string.Join("_", keyParts.Skip(1));
            }
        }
        else if (keyParts.Length == 5)
        {
            // Format: And/Or_property_operator1_operator2_N
            property = keyParts[1];
            @operator = $"{keyParts[2]}_{keyParts[3]}";
            // keyParts[4] est le suffixe numérique
        }
        else
        {
            // Format simple: property
            property = conditionKey;
            @operator = "eq"; // Par défaut
        }

        // Obtenir la valeur de la propriété
        if (!node.Properties.TryGetValue(property, out var actualValue))
        {
            Console.WriteLine($"      - Propriété '{property}' non trouvée dans le nœud");
            return false;
        }

        Console.WriteLine($"      - Comparaison: {actualValue} {@operator} {expectedValue}");

        // Évaluer selon l'opérateur
        return @operator.ToLower() switch
        {
            "eq" => CompareForEquality(actualValue, expectedValue),
            "ne" => !CompareForEquality(actualValue, expectedValue),
            "gt" => CompareValues(actualValue, expectedValue) > 0,
            "lt" => CompareValues(actualValue, expectedValue) < 0,
            "ge" => CompareValues(actualValue, expectedValue) >= 0,
            "le" => CompareValues(actualValue, expectedValue) <= 0,
            "contains" => EvaluateContainsOperator(actualValue, expectedValue),
            "like" => EvaluateLikeOperator(actualValue, expectedValue),
            "starts_with" => EvaluateStartsWithOperator(actualValue, expectedValue),
            "ends_with" => EvaluateEndsWithOperator(actualValue, expectedValue),
            "upper" => EvaluateUpperOperator(actualValue, expectedValue),
            "lower" => EvaluateLowerOperator(actualValue, expectedValue),
            _ => false
        };
    }

    /// <summary>
    /// Évalue l'opérateur 'contains' pour les listes et chaînes
    /// </summary>
    private bool EvaluateContainsOperator(object actualValue, object expectedValue)
    {
        // Si la valeur actuelle est une liste/array
        if (actualValue is List<object> list)
        {
            // Recherche dans la liste avec comparaison insensible à la casse pour les chaînes
            return list.Any(item => 
            {
                if (item is string itemStr && expectedValue is string expectedStr)
                {
                    return itemStr.Equals(expectedStr, StringComparison.OrdinalIgnoreCase);
                }
                return Equals(item, expectedValue);
            });
        }
        
        // Si la valeur actuelle est une chaîne, vérifier si elle contient la valeur attendue
        if (actualValue is string actualStr && expectedValue is string expectedStr)
        {
            return actualStr.Contains(expectedStr, StringComparison.OrdinalIgnoreCase);
        }
        
        // Pour les autres types, retourner false
        return false;
    }

    /// <summary>
    /// Évalue l'opérateur 'like' pour les patterns de chaînes (avec wildcards % et _)
    /// </summary>
    private bool EvaluateLikeOperator(object actualValue, object expectedValue)
    {
        if (actualValue is not string actualStr || expectedValue is not string expectedStr)
            return false;

        // Convertir le pattern LIKE en regex
        // % = zéro ou plusieurs caractères
        // _ = exactement un caractère
        
        // CORRECTION: D'abord échapper tous les caractères regex sauf % et _
        var pattern = expectedStr;
        
        // Échapper les caractères spéciaux regex sauf % et _
        pattern = pattern.Replace("\\", "\\\\")  // Échapper les backslashes d'abord
                        .Replace(".", "\\.")
                        .Replace("^", "\\^")
                        .Replace("$", "\\$")
                        .Replace("+", "\\+")
                        .Replace("*", "\\*")
                        .Replace("?", "\\?")
                        .Replace("{", "\\{")
                        .Replace("}", "\\}")
                        .Replace("[", "\\[")
                        .Replace("]", "\\]")
                        .Replace("(", "\\(")
                        .Replace(")", "\\)")
                        .Replace("|", "\\|");
        
        // Maintenant convertir les wildcards LIKE en regex
        pattern = pattern.Replace("%", ".*")     // % devient .*
                        .Replace("_", ".");      // _ devient .
        
        var regexPattern = "^" + pattern + "$";
        
        Console.WriteLine($"        DEBUG LIKE: '{actualStr}' vs pattern '{expectedStr}' -> regex '{regexPattern}'");
        var result = Regex.IsMatch(actualStr, regexPattern, RegexOptions.IgnoreCase);
        Console.WriteLine($"        DEBUG LIKE: Résultat = {result}");
        
        return result;
    }

    /// <summary>
    /// Évalue l'opérateur 'starts_with' pour vérifier le début d'une chaîne
    /// </summary>
    private bool EvaluateStartsWithOperator(object actualValue, object expectedValue)
    {
        if (actualValue is not string actualStr || expectedValue is not string expectedStr)
            return false;

        return actualStr.StartsWith(expectedStr, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Évalue l'opérateur 'ends_with' pour vérifier la fin d'une chaîne
    /// </summary>
    private bool EvaluateEndsWithOperator(object actualValue, object expectedValue)
    {
        if (actualValue is not string actualStr || expectedValue is not string expectedStr)
            return false;

        return actualStr.EndsWith(expectedStr, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Évalue l'opérateur 'upper' pour comparer avec une chaîne en majuscules
    /// </summary>
    private bool EvaluateUpperOperator(object actualValue, object expectedValue)
    {
        if (actualValue is not string actualStr || expectedValue is not string expectedStr)
            return false;

        return actualStr.ToUpperInvariant().Equals(expectedStr.ToUpperInvariant(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Évalue l'opérateur 'lower' pour comparer avec une chaîne en minuscules
    /// </summary>
    private bool EvaluateLowerOperator(object actualValue, object expectedValue)
    {
        if (actualValue is not string actualStr || expectedValue is not string expectedStr)
            return false;

        return actualStr.ToLowerInvariant().Equals(expectedStr.ToLowerInvariant(), StringComparison.Ordinal);
    }

    /// <summary>
    /// Compare deux valeurs pour l'égalité avec gestion insensible à la casse pour les chaînes
    /// </summary>
    private bool CompareForEquality(object actual, object expected)
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

    private int CompareValues(object actual, object expected)
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

    private bool IsModifyingOperation(QueryType type)
    {
        return type is QueryType.CreateNode or QueryType.CreateEdge or 
               QueryType.UpdateNode or QueryType.UpdateEdge or 
               QueryType.DeleteNode or QueryType.DeleteEdge;
    }

    public void Dispose()
    {
        _storage.SaveAsync().Wait();
    }

    private Task<QueryResult> ExecuteAggregateAsync(ParsedQuery query)
    {
        if (query.AggregateFunction == null || string.IsNullOrEmpty(query.AggregateProperty))
        {
            return Task.FromResult(new QueryResult
            {
                Success = false,
                Error = "Fonction d'agrégation ou propriété manquante"
            });
        }

        var nodes = _storage.GetNodesByLabel(query.NodeLabel!);

        // Appliquer les conditions WHERE si présentes
        if (query.Conditions.Any())
        {
            nodes = FilterNodesByConditions(nodes, query.Conditions);
        }

        // Extraire les valeurs numériques de la propriété spécifiée
        var numericValues = new List<double>();
        foreach (var node in nodes)
        {
            if (node.Properties.TryGetValue(query.AggregateProperty, out var value))
            {
                if (TryConvertToDouble(value, out double numericValue))
                {
                    numericValues.Add(numericValue);
                }
            }
        }

        if (!numericValues.Any())
        {
            return Task.FromResult(new QueryResult
            {
                Success = false,
                Error = $"Aucune valeur numérique trouvée pour la propriété '{query.AggregateProperty}' dans les nœuds de type '{query.NodeLabel}'"
            });
        }

        // Calculer l'agrégation selon la fonction demandée
        object result = query.AggregateFunction switch
        {
            AggregateFunction.Sum => numericValues.Sum(),
            AggregateFunction.Avg => numericValues.Average(),
            AggregateFunction.Min => numericValues.Min(),
            AggregateFunction.Max => numericValues.Max(),
            AggregateFunction.Count => numericValues.Count,
            _ => throw new NotSupportedException($"Fonction d'agrégation non supportée : {query.AggregateFunction}")
        };

        var functionName = query.AggregateFunction.ToString().ToLowerInvariant();
        var conditionMessage = query.Conditions.Any() ? " (avec conditions)" : "";

        return Task.FromResult(new QueryResult
        {
            Success = true,
            Message = $"{functionName.ToUpperInvariant()}({query.AggregateProperty}) sur {numericValues.Count} nœud(s) de type '{query.NodeLabel}'{conditionMessage} = {result}",
            Data = new
            {
                Function = functionName,
                Property = query.AggregateProperty,
                Label = query.NodeLabel,
                Count = numericValues.Count,
                Result = result,
                Values = numericValues.Take(10).ToList() // Afficher les 10 premières valeurs pour debug
            }
        });
    }

    /// <summary>
    /// Tente de convertir une valeur en double pour les calculs d'agrégation
    /// </summary>
    private bool TryConvertToDouble(object value, out double result)
    {
        result = 0;

        if (value == null) return false;

        // Types numériques directs
        if (value is double d)
        {
            result = d;
            return true;
        }
        
        if (value is float f)
        {
            result = f;
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
        
        if (value is decimal dec)
        {
            result = (double)dec;
            return true;
        }

        // Tentative de conversion depuis une chaîne
        if (value is string str)
        {
            return double.TryParse(str, out result);
        }

        // Tentative de conversion générique
        try
        {
            result = Convert.ToDouble(value);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Filtre les arêtes selon les conditions spécifiées
    /// </summary>
    private List<Edge> FilterEdgesByConditions(List<Edge> edges, Dictionary<string, object> conditions)
    {
        return edges.Where(edge =>
        {
            // Séparer les conditions AND et OR
            var andConditions = new List<KeyValuePair<string, object>>();
            var orConditions = new List<KeyValuePair<string, object>>();

            foreach (var condition in conditions)
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

            bool andResult = true;
            if (andConditions.Any())
            {
                andResult = andConditions.All(condition => EvaluateEdgeCondition(edge, condition.Key, condition.Value));
            }

            bool orResult = false;
            if (orConditions.Any())
            {
                orResult = orConditions.Any(condition => EvaluateEdgeCondition(edge, condition.Key, condition.Value));
            }

            // Logique finale similaire aux nœuds
            if (andConditions.Any() && orConditions.Any())
            {
                return andResult && orResult;
            }
            else if (andConditions.Any())
            {
                return andResult;
            }
            else if (orConditions.Any())
            {
                return orResult;
            }
            else
            {
                return true;
            }
        }).ToList();
    }

    /// <summary>
    /// Évalue une condition individuelle sur une arête
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
        object actualValue = property.ToLower() switch
        {
            "type" or "relationtype" => edge.RelationType,
            _ => edge.Properties.TryGetValue(property, out var propValue) ? propValue : null
        };

        if (actualValue == null)
        {
            return false;
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
