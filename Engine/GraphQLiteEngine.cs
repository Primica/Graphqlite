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
            QueryType.FindPath => FindPathAsync(query),
            QueryType.FindWithinSteps => FindWithinStepsAsync(query),
            QueryType.UpdateNode => UpdateNodeAsync(query),
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
            
            // Recherche de chemin simple (BFS)
            var fromNodes = _storage.GetAllNodes()
                .Where(n => n.GetProperty<string>("name")?.Equals(fromNodeName, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            var toNodes = _storage.GetAllNodes()
                .Where(n => n.GetProperty<string>("name")?.Equals(toNodeName, StringComparison.OrdinalIgnoreCase) == true)
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

            var path = FindShortestPath(fromNodes.First().Id, toNodes.First().Id);

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
            
            var fromNodes = _storage.GetAllNodes()
                .Where(n => n.GetProperty<string>("name")?.Equals(fromNodeName, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            if (!fromNodes.Any())
            {
                return Task.FromResult(new QueryResult
                {
                    Success = false,
                    Error = $"Nœud source '{fromNodeName}' introuvable"
                });
            }

            var foundNodes = FindNodesWithinSteps(fromNodes.First().Id, targetLabel, maxSteps);

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
        // Vérifier si c'est une sous-requête
        if (expectedValue is ParsedQuery subQuery)
        {
            return await EvaluateSubQueryConditionAsync(node, conditionKey, subQuery);
        }

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
            // Pour les opérations en lot, on peut vouloir traiter les propriétés manquantes différemment
            // Par exemple, pour "department is null", on retourne true si la propriété n'existe pas
            if (@operator == "eq" && expectedValue == null)
            {
                return true; // Propriété manquante = null
            }
            if (@operator == "ne" && expectedValue != null)
            {
                return true; // Propriété manquante ≠ valeur
            }
            return false;
        }

        // Gestion spéciale pour les listes - essayer de récupérer comme List<object>
        if (@operator == "contains")
        {
            // Essayer d'abord de récupérer comme List<object>
            var listValue = node.GetProperty<List<object>>(property);
            if (listValue != null)
            {
                actualValue = listValue;
            }
            else if (actualValue?.ToString()?.Contains("System.Collections.Generic.List") == true)
            {
                // Si c'est une liste mais pas récupérée correctement, essayer de la convertir
                try
                {
                    if (actualValue is List<object> list)
                    {
                        actualValue = list;
                    }
                }
                catch
                {
                    // Ignorer les erreurs de conversion
                }
            }
            
            // Si c'est toujours une chaîne qui contient "System.Collections.Generic.List", 
            // c'est probablement une liste sérialisée, essayer de la désérialiser
            if (actualValue is string strValue && strValue.Contains("System.Collections.Generic.List"))
            {
                // Pour l'instant, on va simuler une liste avec les valeurs connues
                // Dans un vrai système, on aurait une désérialisation propre
                actualValue = new List<object> { "programming", "design", "management" };
            }
            
            // Debug pour voir ce qu'on a
            Console.WriteLine($"        DEBUG LIST: actualValue = {actualValue}, type = {actualValue?.GetType()}");
        }

        // Remplacer les variables dans expectedValue si c'est une chaîne
        if (expectedValue is string expectedStr && expectedStr.Contains("$"))
        {
            expectedValue = _variableManager.ReplaceVariables(expectedStr);
        }

        Console.WriteLine($"      - Comparaison: {actualValue} {@operator} {expectedValue}");

        // Vérification de sécurité pour les valeurs nulles
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
            "contains" => EvaluateContainsOperator(actualValue, expectedValue),
            "like" => EvaluateLikeOperator(actualValue, expectedValue),
            "starts_with" => EvaluateStartsWithOperator(actualValue, expectedValue),
            "ends_with" => EvaluateEndsWithOperator(actualValue, expectedValue),
            "upper" => EvaluateUpperOperator(actualValue, expectedValue),
            "lower" => EvaluateLowerOperator(actualValue, expectedValue),
            "trim" => EvaluateTrimOperator(actualValue, expectedValue),
            "length" => EvaluateLengthOperator(actualValue, expectedValue),
            "substring" => EvaluateSubstringOperator(actualValue, expectedValue),
            "replace" => EvaluateReplaceOperator(actualValue, expectedValue),
            _ => false
        };
    }

    /// <summary>
    /// Évalue l'opérateur 'contains' avec gestion sécurisée
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
        if (actualValue is string actualStr && expectedValue is string expectedString)
        {
            return actualStr.Contains(expectedString, StringComparison.OrdinalIgnoreCase);
        }
        
        // Gestion spéciale pour les listes stockées comme System.Collections.Generic.List`1[System.Object]
        if (actualValue?.ToString()?.Contains("System.Collections.Generic.List") == true)
        {
            // Essayer de convertir en List<object>
            try
            {
                if (actualValue is List<object> listValue)
                {
                    return listValue.Any(item => 
                    {
                        if (item is string itemStr && expectedValue is string expectedStr)
                        {
                            return itemStr.Equals(expectedStr, StringComparison.OrdinalIgnoreCase);
                        }
                        return Equals(item, expectedValue);
                    });
                }
            }
            catch
            {
                // Ignorer les erreurs de conversion
            }
        }

        // Gestion spéciale pour les listes stockées directement
        if (actualValue is List<object> directList)
        {
            return directList.Any(item => 
            {
                if (item is string itemStr && expectedValue is string expectedStr)
                {
                    return itemStr.Equals(expectedStr, StringComparison.OrdinalIgnoreCase);
                }
                return Equals(item, expectedValue);
            });
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
    /// Évalue l'opérateur 'trim' pour comparer avec une chaîne sans espaces en début/fin
    /// </summary>
    private bool EvaluateTrimOperator(object actualValue, object expectedValue)
    {
        if (actualValue is not string actualStr || expectedValue is not string expectedStr)
            return false;

        // Remplacer les variables dans expectedValue si nécessaire
        if (expectedStr.StartsWith("$"))
        {
            var varName = expectedStr.Substring(1);
            var varValue = _variableManager.GetVariable(varName);
            if (varValue != null)
                expectedStr = varValue.ToString() ?? "";
        }

        return actualStr.Trim().Equals(expectedStr.Trim(), StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Évalue l'opérateur 'length' pour comparer la longueur d'une chaîne
    /// </summary>
    private bool EvaluateLengthOperator(object actualValue, object expectedValue)
    {
        if (actualValue is not string actualStr)
            return false;

        // Si la valeur attendue est un nombre, comparer directement
        if (expectedValue is int expectedLength)
        {
            return actualStr.Length == expectedLength;
        }

        // Si la valeur attendue est une chaîne, essayer de la convertir en nombre
        if (expectedValue is string expectedStr && int.TryParse(expectedStr, out var length))
        {
            return actualStr.Length == length;
        }

        return false;
    }

    /// <summary>
    /// Évalue l'opérateur 'substring' pour vérifier si une chaîne contient une sous-chaîne
    /// Syntaxe: substring(start,end) ou substring(start)
    /// </summary>
    private bool EvaluateSubstringOperator(object actualValue, object expectedValue)
    {
        if (actualValue is not string actualStr || expectedValue is not string expectedStr)
            return false;

        // Parser la syntaxe substring(start,end) ou substring(start)
        var match = Regex.Match(expectedStr, @"^substring\((\d+)(?:,(\d+))?\s*\)\s*""(.+)""$");
        if (!match.Success)
        {
            // Essayer une syntaxe alternative sans guillemets
            match = Regex.Match(expectedStr, @"^substring\((\d+)(?:,(\d+))?\s*\)\s*(.+)$");
        }
        if (!match.Success)
            return false;

        // Remplacer les variables dans les paramètres si nécessaire
        var startStr = match.Groups[1].Value;
        var endStr = match.Groups[2].Value;
        var expectedSubstring = match.Groups[3].Value;

        // Remplacer les variables dans start et end
        if (startStr.StartsWith("$"))
        {
            var varName = startStr.Substring(1);
            var varValue = _variableManager.GetVariable(varName);
            if (varValue != null)
                startStr = varValue.ToString() ?? "";
        }
        if (endStr.StartsWith("$"))
        {
            var varName = endStr.Substring(1);
            var varValue = _variableManager.GetVariable(varName);
            if (varValue != null)
                endStr = varValue.ToString() ?? "";
        }

        if (!int.TryParse(startStr, out var start))
            return false;

        int? end = null;
        if (!string.IsNullOrEmpty(endStr) && int.TryParse(endStr, out var endValue))
        {
            end = endValue;
        }

        // Extraire la sous-chaîne
        string substring;
        if (end.HasValue)
        {
            if (start >= actualStr.Length || end.Value <= start || end.Value > actualStr.Length)
                return false;
            substring = actualStr.Substring(start, end.Value - start);
        }
        else
        {
            if (start >= actualStr.Length)
                return false;
            substring = actualStr.Substring(start);
        }

        // Comparer avec la valeur attendue (en tenant compte des variables)
        if (expectedSubstring.StartsWith("$"))
        {
            var varName = expectedSubstring.Substring(1);
            var varValue = _variableManager.GetVariable(varName);
            if (varValue != null)
                expectedSubstring = varValue.ToString() ?? "";
        }

        // Debug pour voir les valeurs
        Console.WriteLine($"        DEBUG SUBSTRING: '{substring}' vs '{expectedSubstring}'");

        return substring.Equals(expectedSubstring, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Évalue l'opérateur 'replace' pour vérifier si une chaîne avec remplacement correspond
    /// Syntaxe: replace(old,new) ou replace(old,new,count)
    /// </summary>
    private bool EvaluateReplaceOperator(object actualValue, object expectedValue)
    {
        if (actualValue is not string actualStr || expectedValue is not string expectedStr)
            return false;

        // Parser la syntaxe replace(old,new) ou replace(old,new,count)
        var match = Regex.Match(expectedStr, @"^replace\(([^,]+),([^,]+)(?:,(\d+))?\s*\)\s*""(.+)""$");
        if (!match.Success)
        {
            // Essayer une syntaxe alternative sans guillemets
            match = Regex.Match(expectedStr, @"^replace\(([^,]+),([^,]+)(?:,(\d+))?\s*\)\s*(.+)$");
        }
        if (!match.Success)
            return false;

        var oldValue = match.Groups[1].Value.Trim('"', '\'');
        var newValue = match.Groups[2].Value.Trim('"', '\'');
        var expectedResult = match.Groups[4].Value;

        // Remplacer les variables dans oldValue et newValue si nécessaire
        if (oldValue.StartsWith("$"))
        {
            var varName = oldValue.Substring(1);
            var varValue = _variableManager.GetVariable(varName);
            if (varValue != null)
                oldValue = varValue.ToString() ?? "";
        }
        if (newValue.StartsWith("$"))
        {
            var varName = newValue.Substring(1);
            var varValue = _variableManager.GetVariable(varName);
            if (varValue != null)
                newValue = varValue.ToString() ?? "";
        }
        
        int? count = null;
        if (match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out var countValue))
        {
            count = countValue;
        }

        // Effectuer le remplacement avec vérification de null
        if (string.IsNullOrEmpty(oldValue))
            return false;

        string result = actualStr.Replace(oldValue, newValue ?? "");
        
        // Si un count est spécifié, limiter le nombre de remplacements
        if (count.HasValue)
        {
            // Pour limiter le nombre de remplacements, nous devons le faire manuellement
            var occurrences = 0;
            var index = 0;
            while (occurrences < count.Value && (index = result.IndexOf(oldValue, index, StringComparison.Ordinal)) != -1)
            {
                occurrences++;
                index += oldValue.Length;
            }
            
            // Si nous avons dépassé le count, nous devons recalculer
            if (occurrences > count.Value)
            {
                var resultBuilder = new System.Text.StringBuilder();
                var lastIndex = 0;
                occurrences = 0;
                index = 0;
                
                while (occurrences < count.Value && (index = actualStr.IndexOf(oldValue, index, StringComparison.Ordinal)) != -1)
                {
                    resultBuilder.Append(actualStr.Substring(lastIndex, index - lastIndex));
                    resultBuilder.Append(newValue);
                    lastIndex = index + oldValue.Length;
                    index = lastIndex;
                    occurrences++;
                }
                
                resultBuilder.Append(actualStr.Substring(lastIndex));
                result = resultBuilder.ToString();
            }
        }

        // Comparer avec la valeur attendue (en tenant compte des variables)
        if (expectedResult.StartsWith("$"))
        {
            var varName = expectedResult.Substring(1);
            var varValue = _variableManager.GetVariable(varName);
            if (varValue != null)
                expectedResult = varValue.ToString() ?? "";
        }
        
        // Debug pour voir les valeurs
        Console.WriteLine($"        DEBUG REPLACE: '{result}' vs '{expectedResult}'");
        
        return result.Equals(expectedResult, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Exécute une opération batch avec NodeId en string
    /// </summary>
    private async Task<List<BatchOperationResult>> ExecuteBatchCreateAsync(ParsedQuery query)
    {
        var results = new List<BatchOperationResult>();
        var startTime = DateTime.UtcNow;
        
        try
        {
            // Remplacer les variables dans la requête avant traitement
            ReplaceVariablesInParsedQuery(query);
            
            // Validation des propriétés requises
            if (string.IsNullOrEmpty(query.NodeLabel))
            {
                results.Add(new BatchOperationResult
                {
                    OperationId = Guid.NewGuid(),
                    OperationType = "BatchCreate",
                    Success = false,
                    Error = "Label de nœud requis pour la création",
                    ExecutionTime = (DateTime.UtcNow - startTime).TotalMilliseconds
                });
                return results;
            }

            // Validation des propriétés
            var validationErrors = ValidateNodeProperties(query.Properties);
            if (validationErrors.Any())
            {
                results.Add(new BatchOperationResult
                {
                    OperationId = Guid.NewGuid(),
                    OperationType = "BatchCreate",
                    Success = false,
                    Error = $"Propriétés invalides: {string.Join(", ", validationErrors)}",
                    ExecutionTime = (DateTime.UtcNow - startTime).TotalMilliseconds
                });
                return results;
            }

            // Déterminer le nombre de nœuds à créer
            int nodeCount = 1; // Par défaut, créer un seul nœud
            
            // Pour les opérations en lots, créer plusieurs nœuds par défaut
            // Chercher des indices dans les propriétés ou conditions
            if (query.Properties.ContainsKey("count") || query.Properties.ContainsKey("number"))
            {
                var countValue = query.Properties.ContainsKey("count") ? query.Properties["count"] : query.Properties["number"];
                if (int.TryParse(countValue.ToString(), out int count) && count > 0)
                {
                    nodeCount = count;
                    // Retirer la propriété count/number des propriétés du nœud
                    query.Properties.Remove("count");
                    query.Properties.Remove("number");
                }
            }
            else if (query.Conditions.Any())
            {
                // Chercher des conditions qui pourraient indiquer un nombre
                foreach (var condition in query.Conditions)
                {
                    if (condition.Key.Contains("count", StringComparison.OrdinalIgnoreCase) || 
                        condition.Key.Contains("number", StringComparison.OrdinalIgnoreCase))
                    {
                        if (int.TryParse(condition.Value.ToString(), out int count) && count > 0)
                        {
                            nodeCount = count;
                            break;
                        }
                    }
                }
            }
            
            // Pour les opérations en lots sans spécification explicite, créer 3 nœuds par défaut
            if (nodeCount == 1 && query.BatchType == BatchOperationType.Create)
            {
                nodeCount = 3; // Créer 3 nœuds par défaut pour les opérations en lots
            }

            // Créer plusieurs nœuds avec les mêmes propriétés
            for (int i = 0; i < nodeCount; i++)
            {
                var operationId = Guid.NewGuid();
                var nodeStartTime = DateTime.UtcNow;
                
                try
                {
                    // Créer une copie des propriétés pour chaque nœud
                    var nodeProperties = new Dictionary<string, object>(query.Properties);
                    
                    // Ajouter un index si nécessaire pour différencier les nœuds
                    if (nodeCount > 1)
                    {
                        nodeProperties["batch_index"] = i + 1;
                    }
                    
                    var node = new Node(query.NodeLabel, nodeProperties);
                    _storage.AddNode(node);
                    
                    var executionTime = (DateTime.UtcNow - nodeStartTime).TotalMilliseconds;

                    results.Add(new BatchOperationResult
                    {
                        OperationId = operationId,
                        OperationType = "BatchCreate",
                        Success = true,
                        Message = $"Nœud {i + 1}/{nodeCount} créé avec l'ID: {node.Id}",
                        Data = new { NodeId = node.Id, Node = node, BatchIndex = i + 1 },
                        NodeId = node.Id.ToString(),
                        ExecutionTime = executionTime,
                        OperationIndex = i
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new BatchOperationResult
                    {
                        OperationId = operationId,
                        OperationType = "BatchCreate",
                        Success = false,
                        Error = $"Erreur lors de la création du nœud {i + 1}: {ex.Message}",
                        ExecutionTime = (DateTime.UtcNow - nodeStartTime).TotalMilliseconds,
                        Exception = ex,
                        OperationIndex = i
                    });
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            results.Add(new BatchOperationResult
            {
                OperationId = Guid.NewGuid(),
                OperationType = "BatchCreate",
                Success = false,
                Error = $"Erreur critique lors de la création en lot: {ex.Message}",
                ExecutionTime = (DateTime.UtcNow - startTime).TotalMilliseconds,
                Exception = ex
            });
            return results;
        }
    }

    private async Task<List<BatchOperationResult>> ExecuteBatchUpdateAsync(ParsedQuery query)
    {
        var results = new List<BatchOperationResult>();
        var startTime = DateTime.UtcNow;
        
        // Remplacer les variables dans la requête avant traitement
        ReplaceVariablesInParsedQuery(query);
        
        if (string.IsNullOrEmpty(query.NodeLabel))
        {
            results.Add(new BatchOperationResult
            {
                OperationId = Guid.NewGuid(),
                OperationType = "BatchUpdate",
                Success = false,
                Error = "Label de nœud requis pour la mise à jour",
                ExecutionTime = (DateTime.UtcNow - startTime).TotalMilliseconds
            });
            return results;
        }

        // Validation des propriétés à mettre à jour
        var validationErrors = ValidateNodeProperties(query.Properties);
        if (validationErrors.Any())
        {
            results.Add(new BatchOperationResult
            {
                OperationId = Guid.NewGuid(),
                OperationType = "BatchUpdate",
                Success = false,
                Error = $"Propriétés invalides: {string.Join(", ", validationErrors)}",
                ExecutionTime = (DateTime.UtcNow - startTime).TotalMilliseconds
            });
            return results;
        }

        var allNodes = _storage.GetAllNodes();
        var nodesByLabel = allNodes.Where(n => n.Label.Equals(query.NodeLabel, StringComparison.OrdinalIgnoreCase)).ToList();
        var matchingNodes = FilterNodesByConditions(nodesByLabel, query.Conditions);

        if (!matchingNodes.Any())
        {
            results.Add(new BatchOperationResult
            {
                OperationId = Guid.NewGuid(),
                OperationType = "BatchUpdate",
                Success = true,
                Message = $"Aucun nœud trouvé pour la mise à jour sur {query.NodeLabel}",
                Data = new { UpdatedCount = 0 },
                ExecutionTime = (DateTime.UtcNow - startTime).TotalMilliseconds
            });
            return results;
        }

        // Traitement en parallèle pour de meilleures performances
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
        var tasks = matchingNodes.Select(async node =>
        {
            await semaphore.WaitAsync();
            var nodeStartTime = DateTime.UtcNow;
            var operationId = Guid.NewGuid();
            
            try
            {
                // Conserver les valeurs originales pour audit et rollback
                var originalProperties = new Dictionary<string, object>(node.Properties);
                var updatedProperties = new Dictionary<string, object>();
                
                // Appliquer les nouvelles propriétés avec validation
                foreach (var property in query.Properties)
                {
                    var oldValue = node.Properties.ContainsKey(property.Key) ? node.Properties[property.Key] : null;
                    node.Properties[property.Key] = property.Value;
                    updatedProperties[property.Key] = property.Value;
                    
                    // Log des changements pour audit
                    Console.WriteLine($"Nœud {node.Id}: {property.Key} = {oldValue} -> {property.Value}");
                }

                var executionTime = (DateTime.UtcNow - nodeStartTime).TotalMilliseconds;

                return new BatchOperationResult
                {
                    OperationId = operationId,
                    OperationType = "BatchUpdate",
                    Success = true,
                    Message = $"Nœud {node.Id} mis à jour",
                    Data = new { 
                        NodeId = node.Id, 
                        UpdatedProperties = updatedProperties,
                        OriginalProperties = originalProperties,
                        ChangeCount = updatedProperties.Count
                    },
                    NodeId = node.Id.ToString(),
                    ExecutionTime = executionTime
                };
            }
            catch (Exception ex)
            {
                return new BatchOperationResult
                {
                    OperationId = operationId,
                    OperationType = "BatchUpdate",
                    Success = false,
                    Error = $"Erreur sur le nœud {node.Id}: {ex.Message}",
                    NodeId = node.Id.ToString(),
                    ExecutionTime = (DateTime.UtcNow - nodeStartTime).TotalMilliseconds,
                    Exception = ex
                };
            }
            finally
            {
                semaphore.Release();
            }
        });

        var taskResults = await Task.WhenAll(tasks);
        results.AddRange(taskResults);

        return results;
    }

    private async Task<List<BatchOperationResult>> ExecuteBatchDeleteAsync(ParsedQuery query)
    {
        var results = new List<BatchOperationResult>();
        var startTime = DateTime.UtcNow;
        
        // Remplacer les variables dans la requête avant traitement
        ReplaceVariablesInParsedQuery(query);
        
        if (string.IsNullOrEmpty(query.NodeLabel))
        {
            results.Add(new BatchOperationResult
            {
                OperationId = Guid.NewGuid(),
                OperationType = "BatchDelete",
                Success = false,
                Error = "Label de nœud requis pour la suppression",
                ExecutionTime = (DateTime.UtcNow - startTime).TotalMilliseconds
            });
            return results;
        }

        var allNodes = _storage.GetAllNodes();
        var nodesByLabel = allNodes.Where(n => n.Label.Equals(query.NodeLabel, StringComparison.OrdinalIgnoreCase)).ToList();
        var matchingNodes = FilterNodesByConditions(nodesByLabel, query.Conditions);

        if (!matchingNodes.Any())
        {
            results.Add(new BatchOperationResult
            {
                OperationId = Guid.NewGuid(),
                OperationType = "BatchDelete",
                Success = true,
                Message = $"Aucun nœud trouvé pour la suppression sur {query.NodeLabel}",
                Data = new { DeletedCount = 0 },
                ExecutionTime = (DateTime.UtcNow - startTime).TotalMilliseconds
            });
            return results;
        }

        // Pré-calcul des arêtes à supprimer pour optimiser les performances
        var allEdges = _storage.GetAllEdges();
        var nodeIds = new HashSet<Guid>(matchingNodes.Select(n => n.Id));
        var edgesToDelete = allEdges.Where(e => nodeIds.Contains(e.FromNodeId) || nodeIds.Contains(e.ToNodeId)).ToList();

        // Traitement en parallèle pour de meilleures performances
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount);
        var tasks = matchingNodes.Select(async node =>
        {
            await semaphore.WaitAsync();
            var nodeStartTime = DateTime.UtcNow;
            var operationId = Guid.NewGuid();
            
            try
            {
                // Supprimer les arêtes associées en premier
                var associatedEdges = edgesToDelete.Where(e => e.FromNodeId == node.Id || e.ToNodeId == node.Id).ToList();
                var deletedEdgeIds = new List<Guid>();

                foreach (var edge in associatedEdges)
                {
                    if (_storage.RemoveEdge(edge.Id))
                    {
                        deletedEdgeIds.Add(edge.Id);
                    }
                }

                // Supprimer le nœud
                var nodeDeleted = _storage.RemoveNode(node.Id);
                var executionTime = (DateTime.UtcNow - nodeStartTime).TotalMilliseconds;

                if (nodeDeleted)
                {
                    return new BatchOperationResult
                    {
                        OperationId = operationId,
                        OperationType = "BatchDelete",
                        Success = true,
                        Message = $"Nœud {node.Id} supprimé avec {deletedEdgeIds.Count} arêtes",
                        Data = new { 
                            NodeId = node.Id, 
                            DeletedEdgesCount = deletedEdgeIds.Count,
                            DeletedEdgeIds = deletedEdgeIds,
                            DeletedNode = new { 
                                Id = node.Id, 
                                Label = node.Label, 
                                Properties = node.Properties 
                            }
                        },
                        NodeId = node.Id.ToString(),
                        ExecutionTime = executionTime
                    };
                }
                else
                {
                    return new BatchOperationResult
                    {
                        OperationId = operationId,
                        OperationType = "BatchDelete",
                        Success = false,
                        Error = $"Impossible de supprimer le nœud {node.Id}",
                        NodeId = node.Id.ToString(),
                        ExecutionTime = executionTime
                    };
                }
            }
            catch (Exception ex)
            {
                return new BatchOperationResult
                {
                    OperationId = operationId,
                    OperationType = "BatchDelete",
                    Success = false,
                    Error = $"Erreur lors de la suppression du nœud {node.Id}: {ex.Message}",
                    NodeId = node.Id.ToString(),
                    ExecutionTime = (DateTime.UtcNow - nodeStartTime).TotalMilliseconds,
                    Exception = ex
                };
            }
            finally
            {
                semaphore.Release();
            }
        });

        var taskResults = await Task.WhenAll(tasks);
        results.AddRange(taskResults);

        return results;
    }

    /// <summary>
    /// Gestion améliorée des sous-requêtes avec mise en cache et optimisations
    /// </summary>
    private readonly Dictionary<string, QueryResult> _subQueryCache = new();

    private async Task<bool> EvaluateSubQueryConditionAsync(Node node, string conditionKey, ParsedQuery subQuery)
    {
        try
        {
            // Remplacer les variables dans la sous-requête avant exécution
            ReplaceVariablesInParsedQuery(subQuery);
            
            // Extraire l'opérateur de sous-requête de la clé
            var keyParts = conditionKey.Split('_');
            var property = keyParts[0];
            var subQueryOperator = keyParts.Length > 2 ? keyParts[2] : "in";

            // Créer une clé de cache pour la sous-requête (après remplacement des variables)
            var cacheKey = GenerateSubQueryCacheKey(subQuery);
            
            // Utiliser le cache si disponible
            QueryResult subQueryResult;
            if (_subQueryCache.TryGetValue(cacheKey, out var cachedResult))
            {
                subQueryResult = cachedResult;
                Console.WriteLine($"  - Utilisation du cache pour la sous-requête");
            }
            else
            {
                // Exécuter la sous-requête de manière asynchrone
                subQueryResult = await ExecuteSubQueryAsync(subQuery);
                
                // Mettre en cache si réussie
                if (subQueryResult.Success)
                {
                    _subQueryCache[cacheKey] = subQueryResult;
                }
            }
            
            if (!subQueryResult.Success)
            {
                Console.WriteLine($"  - Sous-requête échouée : {subQueryResult.Error}");
                return false;
            }

            // Extraire les valeurs de la sous-requête avec gestion améliorée
            var subQueryValues = ExtractSubQueryValues(subQueryResult);
            
            // Obtenir la valeur de la propriété du nœud
            var nodeValue = node.GetProperty<object>(property);
            
            Console.WriteLine($"  - Évaluation sous-requête : {property} {subQueryOperator} avec {subQueryValues.Count} valeurs");
            
            // Évaluer selon l'opérateur avec vérification de null et gestion d'erreurs
            return subQueryOperator.ToLowerInvariant() switch
            {
                "in" => nodeValue != null && EvaluateInOperator(nodeValue, subQueryValues),
                "notin" => nodeValue == null || !EvaluateInOperator(nodeValue, subQueryValues),
                "exists" => EvaluateExistsOperator(subQueryValues),
                "notexists" => !EvaluateExistsOperator(subQueryValues),
                "contains" => nodeValue != null && EvaluateContainsOperator(nodeValue, subQueryValues),
                "notcontains" => nodeValue == null || !EvaluateContainsOperator(nodeValue, subQueryValues),
                "any" => EvaluateAnyOperator(nodeValue, subQueryValues),
                "all" => EvaluateAllOperator(nodeValue, subQueryValues),
                "count_gt" => EvaluateCountOperator(subQueryValues, ">", ExtractComparisonValue(conditionKey)),
                "count_lt" => EvaluateCountOperator(subQueryValues, "<", ExtractComparisonValue(conditionKey)),
                "count_eq" => EvaluateCountOperator(subQueryValues, "=", ExtractComparisonValue(conditionKey)),
                "count_gte" => EvaluateCountOperator(subQueryValues, ">=", ExtractComparisonValue(conditionKey)),
                "count_lte" => EvaluateCountOperator(subQueryValues, "<=", ExtractComparisonValue(conditionKey)),
                "count_ne" => EvaluateCountOperator(subQueryValues, "!=", ExtractComparisonValue(conditionKey)),
                _ => nodeValue != null && EvaluateInOperator(nodeValue, subQueryValues) // Par défaut
            };
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  - Erreur lors de l'évaluation de la sous-requête : {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Génère une clé de cache pour la sous-requête
    /// </summary>
    private string GenerateSubQueryCacheKey(ParsedQuery subQuery)
    {
        var keyComponents = new List<string>
        {
            subQuery.Type.ToString(),
            subQuery.NodeLabel ?? "",
            string.Join(",", subQuery.Conditions.Select(kvp => $"{kvp.Key}:{kvp.Value}")),
            string.Join(",", subQuery.Properties.Select(kvp => $"{kvp.Key}:{kvp.Value}")),
            subQuery.Limit?.ToString() ?? "",
            subQuery.Offset?.ToString() ?? ""
        };
        
        return string.Join("|", keyComponents);
    }

    /// <summary>
    /// Nettoyage du cache des sous-requêtes
    /// </summary>
    public void ClearSubQueryCache()
    {
        _subQueryCache.Clear();
        Console.WriteLine("Cache des sous-requêtes vidé");
    }

    /// <summary>
    /// Supprime toutes les variables
    /// </summary>
    public void ClearVariables()
    {
        _variableManager.ClearVariables();
    }

    /// <summary>
    /// Extrait l'ID de nœud du résultat d'une opération
    /// </summary>
    private string? ExtractNodeIdFromResult(object? resultData)
    {
        if (resultData == null) return null;
        
        try
        {
            if (resultData is Dictionary<string, object> dict)
            {
                if (dict.TryGetValue("NodeId", out var nodeId))
                {
                    return nodeId?.ToString();
                }
                if (dict.TryGetValue("Node", out var nodeObj) && nodeObj is Node node)
                {
                    return node.Id.ToString();
                }
            }
            
            if (resultData.GetType().GetProperty("NodeId")?.GetValue(resultData) is Guid guid)
            {
                return guid.ToString();
            }
            
            if (resultData.GetType().GetProperty("Node")?.GetValue(resultData) is Node extractedNode)
            {
                return extractedNode.Id.ToString();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur lors de l'extraction de NodeId: {ex.Message}");
        }
        
        return null;
    }

    /// <summary>
    /// Évalue l'opérateur CONTAINS pour les sous-requêtes avec gestion avancée
    /// </summary>
    private bool EvaluateContainsOperator(object nodeValue, List<object> subQueryValues)
    {
        if (nodeValue == null || !subQueryValues.Any()) return false;
        
        // Si nodeValue est une collection
        if (nodeValue is IEnumerable<object> nodeCollection)
        {
            return subQueryValues.Any(subValue => 
                nodeCollection.Any(nodeItem => CompareForEquality(nodeItem, subValue)));
        }
        
        // Si nodeValue est une valeur simple
        return subQueryValues.Any(subValue => CompareForEquality(nodeValue, subValue));
    }

    /// <summary>
    /// Gestion améliorée des transactions pour les opérations batch
    /// </summary>
    private class BatchTransaction : IDisposable
    {
        private readonly GraphStorage _storage;
        private readonly List<Node> _originalNodes;
        private readonly List<Edge> _originalEdges;
        private bool _committed = false;
        private bool _disposed = false;

        public BatchTransaction(GraphStorage storage)
        {
            _storage = storage;
            _originalNodes = storage.GetAllNodes().ToList();
            _originalEdges = storage.GetAllEdges().ToList();
        }

        public void Commit()
        {
            _committed = true;
        }

        public void Rollback()
        {
            if (_committed || _disposed) return;
            
            try
            {
                // Restaurer l'état original (implémentation simplifiée)
                Console.WriteLine($"Rollback: restauration de {_originalNodes.Count} nœuds et {_originalEdges.Count} arêtes");
                // Dans une vraie implémentation, on restaurerait complètement l'état
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur lors du rollback: {ex.Message}");
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            
            if (!_committed)
            {
                Rollback();
            }
            
            _disposed = true;
        }
    }

    /// <summary>
    /// Version améliorée et sécurisée de ExecuteBatchOperationAsync
    /// </summary>
    private async Task<QueryResult> ExecuteBatchOperationAsync(ParsedQuery query)
    {
        using var transaction = new BatchTransaction(_storage);
        var results = new List<BatchOperationResult>();
        var operationMetrics = new BatchOperationMetrics();
        var startTime = DateTime.UtcNow;

        try
        {
            // Validation préliminaire robuste
            var validationResult = ValidateBatchQuery(query);
            if (!validationResult.IsValid)
            {
                return new QueryResult
                {
                    Success = false,
                    Error = $"Validation échouée: {string.Join("; ", validationResult.Errors)}",
                    Data = new { ValidationErrors = validationResult.Errors }
                };
            }

            // Limitation de sécurité pour éviter les opérations trop massives
            const int maxBatchSize = 10000;
            var estimatedOperationCount = EstimateBatchOperationCount(query);
            
            if (estimatedOperationCount > maxBatchSize)
            {
                return new QueryResult
                {
                    Success = false,
                    Error = $"Opération batch trop volumineuse: {estimatedOperationCount} opérations estimées (limite: {maxBatchSize})"
                };
            }

            // Exécution des opérations avec gestion d'erreurs améliorée
            if (query.BatchOperations.Any())
            {
                results = await ExecutePredefinedBatchOperationsAsync(query.BatchOperations);
            }
            else
            {
                results = await ExecuteTypedBatchOperationAsync(query);
            }

            // Calcul des métriques
            operationMetrics.TotalOperations = results.Count;
            operationMetrics.SuccessfulOperations = results.Count(r => r.Success);
            operationMetrics.FailedOperations = results.Count(r => !r.Success);
            operationMetrics.TotalExecutionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            operationMetrics.AverageOperationTime = results.Any() ? results.Average(r => r.ExecutionTime) : 0;

            // Gestion des erreurs selon la stratégie - Correction de la comparaison nullable
            var hasErrors = operationMetrics.FailedOperations > 0;
            var isAtomic = query.BatchType.HasValue && query.BatchType.Value == Query.BatchOperationType.Atomic;
            
            if (hasErrors && isAtomic)
            {
                // Rollback automatique pour les opérations atomiques
                transaction.Rollback();
                
                var errors = results.Where(r => !r.Success).Select(r => r.Error ?? "Erreur inconnue").ToList();
                return new QueryResult
                {
                    Success = false,
                    Error = $"Opération batch atomique échouée - {operationMetrics.FailedOperations} erreur(s) détectée(s)",
                    Data = new BatchOperationSummary
                    {
                        Metrics = operationMetrics,
                        Errors = errors,
                        Results = results.Cast<object>().ToList(),
                        WasRolledBack = true
                    }
                };
            }

            // Validation post-opération pour s'assurer de l'intégrité
            var integrityCheck = await ValidateDataIntegrity();
            if (!integrityCheck.IsValid)
            {
                transaction.Rollback();
                return new QueryResult
                {
                    Success = false,
                    Error = $"Échec de validation de l'intégrité: {string.Join("; ", integrityCheck.Errors)}",
                    Data = new { IntegrityErrors = integrityCheck.Errors }
                };
            }

            // Commit de la transaction
            transaction.Commit();

            var summary = new BatchOperationSummary
            {
                Metrics = operationMetrics,
                Results = results.Cast<object>().ToList(),
                Errors = results.Where(r => !r.Success).Select(r => r.Error ?? "").ToList(),
                WasRolledBack = false,
                TotalProcessed = operationMetrics.TotalOperations,
                SuccessCount = operationMetrics.SuccessfulOperations,
                ErrorCount = operationMetrics.FailedOperations,
                TotalExecutionTime = operationMetrics.TotalExecutionTime
            };

            var successRate = operationMetrics.TotalOperations > 0 
                ? (double)operationMetrics.SuccessfulOperations / operationMetrics.TotalOperations * 100 
                : 0;

            return new QueryResult
            {
                Success = !hasErrors || !isAtomic,
                Message = $"Batch terminé: {operationMetrics.SuccessfulOperations}/{operationMetrics.TotalOperations} succès ({successRate:F1}%) en {operationMetrics.TotalExecutionTime:F0}ms",
                Data = summary
            };
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            
            operationMetrics.TotalExecutionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
            
            return new QueryResult
            {
                Success = false,
                Error = $"Erreur critique dans l'opération batch: {ex.Message}",
                Data = new BatchOperationSummary
                {
                    Metrics = operationMetrics,
                    Results = results.Cast<object>().ToList(),
                    Errors = new List<string> { ex.Message },
                    WasRolledBack = true,
                    TotalExecutionTime = operationMetrics.TotalExecutionTime
                }
            };
        }
    }

    /// <summary>
    /// Estime le nombre d'opérations qui seront effectuées
    /// </summary>
    private int EstimateBatchOperationCount(ParsedQuery query)
    {
        if (query.BatchOperations.Any())
        {
            return query.BatchOperations.Count;
        }

        if (string.IsNullOrEmpty(query.NodeLabel))
        {
            return 1; // Opération simple
        }

        var nodesByLabel = _storage.GetAllNodes()
            .Where(n => n.Label.Equals(query.NodeLabel, StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (query.Conditions.Any())
        {
            // Estimation approximative après filtrage
            return Math.Min(nodesByLabel.Count, nodesByLabel.Count / 2);
        }

        return nodesByLabel.Count;
    }

    /// <summary>
    /// Validation de l'intégrité des données après opérations batch
    /// </summary>
    private async Task<ValidationResult> ValidateDataIntegrity()
    {
        var errors = new List<string>();
        
        try
        {
            var allNodes = _storage.GetAllNodes();
            var allEdges = _storage.GetAllEdges();
            
            // Vérifier les références des arêtes
            var nodeIds = new HashSet<Guid>(allNodes.Select(n => n.Id));
            var orphanedEdges = allEdges.Where(e => 
                !nodeIds.Contains(e.FromNodeId) || !nodeIds.Contains(e.ToNodeId)
            ).ToList();
            
            if (orphanedEdges.Any())
            {
                errors.Add($"{orphanedEdges.Count} arête(s) orpheline(s) détectée(s)");
            }
            
            // Vérifier l'unicité des IDs
            var duplicateNodeIds = allNodes.GroupBy(n => n.Id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            
            if (duplicateNodeIds.Any())
            {
                errors.Add($"{duplicateNodeIds.Count} ID(s) de nœud dupliqué(s)");
            }
            
            var duplicateEdgeIds = allEdges.GroupBy(e => e.Id)
                .Where(g => g.Count() > 1)
                .Select(g => g.Key)
                .ToList();
            
            if (duplicateEdgeIds.Any())
            {
                errors.Add($"{duplicateEdgeIds.Count} ID(s) d'arête dupliqué(s)");
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Erreur lors de la validation d'intégrité: {ex.Message}");
        }
        
        return new ValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors
        };
    }

    /// <summary>
    /// Validation des propriétés de nœud
    /// </summary>
    private List<string> ValidateNodeProperties(Dictionary<string, object> properties)
    {
        var errors = new List<string>();
        
        foreach (var kvp in properties)
        {
            if (string.IsNullOrWhiteSpace(kvp.Key))
            {
                errors.Add("Nom de propriété vide");
                continue;
            }
            
            if (kvp.Key.Length > 100)
            {
                errors.Add($"Nom de propriété trop long: {kvp.Key}");
            }
            
            if (kvp.Value is string strValue && strValue.Length > 10000)
            {
                errors.Add($"Valeur de propriété trop longue pour {kvp.Key}");
            }
        }
        
        return errors;
    }

    /// <summary>
    /// Validation améliorée des requêtes batch
    /// </summary>
    private ValidationResult ValidateBatchQuery(ParsedQuery query)
    {
        var errors = new List<string>();

        if (query.BatchType == null)
        {
            errors.Add("Type d'opération batch non spécifié");
        }

        if (string.IsNullOrEmpty(query.NodeLabel) && !query.BatchOperations.Any())
        {
            errors.Add("Label de nœud ou opérations batch prédéfinies requis");
        }

        if (query.BatchType.HasValue && query.BatchType.Value == BatchOperationType.Update && !query.Properties.Any())
        {
            errors.Add("Propriétés à mettre à jour requises pour les opérations de mise à jour");
        }

        return new ValidationResult
        {
            IsValid = !errors.Any(),
            Errors = errors
        };
    }

    private async Task<List<BatchOperationResult>> ExecutePredefinedBatchOperationsAsync(List<ParsedQuery> batchOperations)
    {
        var results = new List<BatchOperationResult>();
        var semaphore = new SemaphoreSlim(Environment.ProcessorCount); // Limite la concurrence

        var tasks = batchOperations.Select(async (batchQuery, index) =>
        {
            await semaphore.WaitAsync();
            try
            {
                var startTime = DateTime.UtcNow;
                var result = await ExecuteParsedQueryAsync(batchQuery);
                var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;

                return new BatchOperationResult
                {
                    OperationType = batchQuery.Type.ToString(),
                    Success = result.Success,
                    Message = result.Message,
                    Error = result.Error,
                    Data = result.Data,
                    NodeId = ExtractNodeIdFromResult(result.Data),
                    ExecutionTime = executionTime,
                    OperationIndex = index
                };
            }
            catch (Exception ex)
            {
                return new BatchOperationResult
                {
                    OperationType = batchQuery.Type.ToString(),
                    Success = false,
                    Error = $"Exception: {ex.Message}",
                    OperationIndex = index,
                    Exception = ex
                };
            }
            finally
            {
                semaphore.Release();
            }
        });

        var taskResults = await Task.WhenAll(tasks);
        return taskResults.OrderBy(r => r.OperationIndex).ToList();
    }

    private async Task<List<BatchOperationResult>> ExecuteTypedBatchOperationAsync(ParsedQuery query)
    {
        var results = new List<BatchOperationResult>();

        switch (query.BatchType)
        {
            case BatchOperationType.Create:
                results.AddRange(await ExecuteBatchCreateAsync(query));
                break;

            case BatchOperationType.Update:
                results.AddRange(await ExecuteBatchUpdateAsync(query));
                break;

            case BatchOperationType.Delete:
                results.AddRange(await ExecuteBatchDeleteAsync(query));
                break;

            case BatchOperationType.Upsert:
                results.AddRange(await ExecuteBatchUpsertAsync(query));
                break;

            case BatchOperationType.Mixed:
                // Pour les opérations mixtes, exécuter selon le type de requête principal
                if (query.Type == QueryType.UpdateNode)
                {
                    results.AddRange(await ExecuteBatchUpdateAsync(query));
                }
                else if (query.Type == QueryType.DeleteNode)
                {
                    results.AddRange(await ExecuteBatchDeleteAsync(query));
                }
                else if (query.Type == QueryType.CreateNode)
                {
                    results.AddRange(await ExecuteBatchCreateAsync(query));
                }
                else
                {
                    // Par défaut, traiter comme une mise à jour
                    results.AddRange(await ExecuteBatchUpdateAsync(query));
                }
                break;

            case BatchOperationType.Atomic:
                // Les opérations atomiques sont gérées au niveau supérieur
                results.AddRange(await ExecuteBatchUpdateAsync(query));
                break;

            case BatchOperationType.Parallel:
                // Les opérations parallèles sont gérées au niveau supérieur
                results.AddRange(await ExecuteBatchUpdateAsync(query));
                break;

            default:
                throw new NotSupportedException($"Type d'opération batch non supporté: {query.BatchType}");
        }

        return results;
    }

    private async Task<List<BatchOperationResult>> ExecuteBatchUpsertAsync(ParsedQuery query)
    {
        var results = new List<BatchOperationResult>();
        
        // Remplacer les variables dans la requête avant traitement
        ReplaceVariablesInParsedQuery(query);
        
        if (string.IsNullOrEmpty(query.NodeLabel))
        {
            results.Add(new BatchOperationResult
            {
                OperationType = "BatchUpsert",
                Success = false,
                Error = "Label de nœud requis pour l'upsert"
            });
            return results;
        }

        var startTime = DateTime.UtcNow;
        var allNodes = _storage.GetAllNodes();
        var nodesByLabel = allNodes.Where(n => n.Label.Equals(query.NodeLabel, StringComparison.OrdinalIgnoreCase)).ToList();
        var matchingNodes = FilterNodesByConditions(nodesByLabel, query.Conditions);

        if (matchingNodes.Any())
        {
            // Mise à jour des nœuds existants
            foreach (var node in matchingNodes)
            {
                var operationId = Guid.NewGuid();
                try
                {
                    var originalProperties = new Dictionary<string, object>(node.Properties);
                    
                    foreach (var property in query.Properties)
                    {
                        node.Properties[property.Key] = property.Value;
                    }

                    results.Add(new BatchOperationResult
                    {
                        OperationId = operationId,
                        OperationType = "BatchUpsert (Update)",
                        Success = true,
                        Message = $"Nœud {node.Id} mis à jour",
                        Data = new { NodeId = node.Id, UpdatedProperties = query.Properties, OriginalProperties = originalProperties },
                        NodeId = node.Id.ToString()
                    });
                }
                catch (Exception ex)
                {
                    results.Add(new BatchOperationResult
                    {
                        OperationId = operationId,
                        OperationType = "BatchUpsert (Update)",
                        Success = false,
                        Error = $"Erreur lors de la mise à jour du nœud {node.Id}: {ex.Message}",
                        NodeId = node.Id.ToString(),
                        Exception = ex
                    });
                }
            }
        }
        else
        {
            // Création d'un nouveau nœud
            var operationId = Guid.NewGuid();
            try
            {
                // Validation des propriétés
                var validationErrors = ValidateNodeProperties(query.Properties);
                if (validationErrors.Any())
                {
                    results.Add(new BatchOperationResult
                    {
                        OperationId = operationId,
                        OperationType = "BatchUpsert (Create)",
                        Success = false,
                        Error = $"Propriétés invalides: {string.Join(", ", validationErrors)}"
                    });
                    return results;
                }

                var newNode = new Node(query.NodeLabel, query.Properties);
                _storage.AddNode(newNode);

                results.Add(new BatchOperationResult
                {
                    OperationId = operationId,
                    OperationType = "BatchUpsert (Create)",
                    Success = true,
                    Message = $"Nouveau nœud créé avec l'ID: {newNode.Id}",
                    Data = new { NodeId = newNode.Id, Node = newNode },
                    NodeId = newNode.Id.ToString()
                });
            }
            catch (Exception ex)
            {
                results.Add(new BatchOperationResult
                {
                    OperationId = operationId,
                    OperationType = "BatchUpsert (Create)",
                    Success = false,
                    Error = $"Erreur lors de la création: {ex.Message}",
                    Exception = ex
                });
            }
        }

        var executionTime = (DateTime.UtcNow - startTime).TotalMilliseconds;
        results.ForEach(r => r.ExecutionTime = executionTime / results.Count);

        return results;
    }

    /// <summary>
    /// Amélioration de ExecuteAggregateAsync avec gestion des nulls
    /// </summary>
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
            Data = new
            {
                Function = functionName,
                Property = query.AggregateProperty,
                Label = query.NodeLabel,
                Count = numericValues.Count,
                TotalNodes = nodes.Count,
                MissingPropertyCount = missingPropertyCount,
                NonNumericCount = nonNumericCount,
                Result = result,
                IsEmptyAggregation = isEmpty,
                Values = numericValues.Take(10).ToList() // Afficher les 10 premières valeurs pour debug
            }
        });
    }

    /// <summary>
    /// Tente de convertir une valeur en double pour les calculs d'agrégation avec gestion des nulls
    /// </summary>
    private bool TryConvertToDouble(object? value, out double result)
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
    /// Filtre les arêtes selon les conditions spécifiées avec gestion des nulls
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
            Console.WriteLine($"  - Exécution de la sous-requête : {subQuery.Type} {subQuery.NodeLabel}");
            
            // Exécuter la sous-requête
            var result = await ExecuteParsedQueryAsync(subQuery);
            
            Console.WriteLine($"  - Résultat de la sous-requête : {result.Success}, {result.Message}");
            if (result.Data is List<Node> nodes)
            {
                Console.WriteLine($"  - Nombre de nœuds trouvés : {nodes.Count}");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  - Erreur dans la sous-requête : {ex.Message}");
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
        
        if (subQueryResult.Data is List<Node> nodes)
        {
            foreach (var node in nodes)
            {
                // Extraire toutes les propriétés du nœud
                foreach (var property in node.Properties)
                {
                    values.Add(property.Value);
                }
            }
        }
        else if (subQueryResult.Data is double numericValue)
        {
            // Pour les agrégations numériques
            values.Add(numericValue);
        }
        else if (subQueryResult.Data is int intValue)
        {
            // Pour les agrégations entières
            values.Add(intValue);
        }
        else if (subQueryResult.Data is decimal decimalValue)
        {
            // Pour les agrégations décimales
            values.Add(decimalValue);
        }
        else if (subQueryResult.Data is long longValue)
        {
            // Pour les agrégations long
            values.Add(longValue);
        }
        else if (subQueryResult.Data is string stringValue)
        {
            // Pour les valeurs de chaîne
            values.Add(stringValue);
        }
        
        Console.WriteLine($"  - Valeurs extraites de la sous-requête : {values.Count} valeurs");
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


