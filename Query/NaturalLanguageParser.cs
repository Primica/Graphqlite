using System.Text.RegularExpressions;

namespace GraphQLite.Query;

/// <summary>
/// Parser pour le DSL GraphQLite en langage naturel
/// Exemples de requêtes supportées :
/// - "create person with name John and age 30"
/// - "find all users where age > 25"
/// - "connect user John to company Acme with relationship works_at"
/// - "find path from John to Mary"
/// - "create product with name Laptop and price 999.99 and tags [electronics, computer, portable] and metadata {brand: Apple, warranty: 2}"
/// - "find all persons where department in (select department from companies where industry = technology)"
/// </summary>
public class NaturalLanguageParser
{
    private static readonly Dictionary<string, QueryType> QueryKeywords = new()
    {
        { "create", QueryType.CreateNode },
        { "add", QueryType.CreateNode },
        { "find", QueryType.FindNodes },
        { "get", QueryType.FindNodes },
        { "search", QueryType.FindNodes },
        { "connect", QueryType.CreateEdge },
        { "link", QueryType.CreateEdge },
        { "relate", QueryType.CreateEdge },
        { "update", QueryType.UpdateNode },
        { "modify", QueryType.UpdateNode },
        { "delete", QueryType.DeleteNode },
        { "remove", QueryType.DeleteNode },
        { "count", QueryType.Count },
        { "path", QueryType.FindPath },
        { "show", QueryType.ShowSchema },
        { "describe", QueryType.ShowSchema },
        { "schema", QueryType.ShowSchema },
        { "sum", QueryType.Aggregate },
        { "avg", QueryType.Aggregate },
        { "min", QueryType.Aggregate },
        { "max", QueryType.Aggregate },
        { "aggregate", QueryType.Aggregate },
        { "let", QueryType.DefineVariable },
        { "set", QueryType.DefineVariable },
        { "var", QueryType.DefineVariable },
        { "define", QueryType.DefineVariable },
        { "batch", QueryType.BatchOperation },
        { "bulk", QueryType.BatchOperation },
        { "import", QueryType.BatchOperation },
        { "select", QueryType.SubQuery },
        { "subquery", QueryType.SubQuery }
    };

    /// <summary>
    /// Parse une requête en langage naturel
    /// </summary>
    public ParsedQuery Parse(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("La requête ne peut pas être vide", nameof(query));

        // Supprimer les points-virgules à la fin et nettoyer la requête
        var originalQuery = query.Trim().TrimEnd(';');
        
        // Déterminer le type de requête en utilisant une version en minuscules
        var queryForTypeDetection = originalQuery.ToLowerInvariant();
        var parsedQuery = new ParsedQuery();
        parsedQuery.Type = DetermineQueryType(queryForTypeDetection);

        // Pour les définitions de variables, utiliser la chaîne originale
        var queryForParsing = parsedQuery.Type == QueryType.DefineVariable ? originalQuery : ConvertToLowerPreservingVariables(originalQuery);

        switch (parsedQuery.Type)
        {
            case QueryType.CreateNode:
                ParseCreateNode(queryForParsing, parsedQuery);
                break;
            case QueryType.CreateEdge:
                ParseCreateEdge(queryForParsing, parsedQuery);
                break;
            case QueryType.FindNodes:
                ParseFindNodes(queryForParsing, parsedQuery);
                break;
            case QueryType.FindPath:
                ParseFindPath(queryForParsing, parsedQuery);
                break;
            case QueryType.FindWithinSteps:
                ParseFindWithinSteps(queryForParsing, parsedQuery);
                break;
            case QueryType.UpdateNode:
                ParseUpdateNode(queryForParsing, parsedQuery);
                break;
            case QueryType.DeleteNode:
                ParseDeleteNode(queryForParsing, parsedQuery);
                break;
            case QueryType.DeleteEdge:
                ParseDeleteEdge(queryForParsing, parsedQuery);
                break;
            case QueryType.Count:
                ParseCount(queryForParsing, parsedQuery);
                break;
            case QueryType.Aggregate:
                ParseAggregate(queryForParsing, parsedQuery);
                break;
            case QueryType.DefineVariable:
                ParseDefineVariable(queryForParsing, parsedQuery);
                break;
            case QueryType.BatchOperation:
                ParseBatchOperation(queryForParsing, parsedQuery);
                break;
            case QueryType.SubQuery:
                ParseSubQuery(queryForParsing, parsedQuery);
                break;
            case QueryType.ShowSchema:
                // Pour les commandes de schéma, aucune autre information n'est nécessaire
                break;
            default:
                throw new NotSupportedException($"Type de requête non reconnu dans : {query}");
        }

        return parsedQuery;
    }

    private string ConvertToLowerPreservingVariables(string query)
    {
        // Convertir en minuscules mais préserver les noms de variables avec $ même dans les chaînes
        var result = query;
        
        // Trouver et remplacer temporairement les variables dans les chaînes entre guillemets
        var variablesInStrings = new List<string>();
        var variableCounter = 0;
        
        // Pattern pour trouver les variables dans les chaînes : "text$variabletext"
        result = Regex.Replace(result, @"[""']([^""']*\$[^""']*)[""']", match =>
        {
            var content = match.Groups[1].Value;
            var placeholder = $"__VAR_{variableCounter}__";
            variablesInStrings.Add(content);
            variableCounter++;
            return $"\"{placeholder}\"";
        });
        
        // Convertir en minuscules
        result = result.ToLowerInvariant();
        
        // Restaurer les variables dans les chaînes
        for (int i = 0; i < variablesInStrings.Count; i++)
        {
            result = result.Replace($"\"__var_{i}__\"", $"\"{variablesInStrings[i]}\"");
        }
        
        // Préserver les variables qui ne sont pas dans des chaînes
        var words = result.Split(' ');
        for (int i = 0; i < words.Length; i++)
        {
            if (words[i].StartsWith("$"))
            {
                // Préserver le nom de variable tel quel
                continue;
            }
        }
        
        return result;
    }

    private QueryType DetermineQueryType(string query)
    {
        var words = query.Split(' ');
        var firstWord = words[0];
        
        // Cas spéciaux pour les opérations en lot
        if (firstWord == "create" && words.Length > 2 && words[1] == "batch" && words[2] == "of")
        {
            return QueryType.BatchOperation;
        }
        
        if (firstWord == "batch" && words.Length > 1)
        {
            return QueryType.BatchOperation;
        }
        
        if (QueryKeywords.TryGetValue(firstWord, out var queryType))
        {
            return queryType;
        }

        throw new NotSupportedException($"Commande non reconnue : {firstWord}");
    }

    private void ParseCreateNode(string query, ParsedQuery parsedQuery)
    {
        // Pattern : "create [label] with [properties]"
        var match = Regex.Match(query, @"create\s+(\w+)\s+with\s+(.+)", RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            parsedQuery.NodeLabel = match.Groups[1].Value;
            var propertiesText = match.Groups[2].Value.Trim();
            
            // Parser les propriétés avec support des propriétés dynamiques
            ParseDynamicProperties(propertiesText, parsedQuery.Properties);
        }
        else
        {
            throw new ArgumentException($"Format de création de nœud invalide : {query}");
        }
    }

    private void ParseCreateEdge(string query, ParsedQuery parsedQuery)
    {
        // Pattern : "connect [from] to [to] with relationship [type] [properties]"
        var match = Regex.Match(query, @"connect\s+(\w+)\s+to\s+(\w+)\s+with\s+relationship\s+(\w+)(?:\s+(.+))?", RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            parsedQuery.FromNode = match.Groups[1].Value;
            parsedQuery.ToNode = match.Groups[2].Value;
            parsedQuery.EdgeType = match.Groups[3].Value;
            
            var propertiesText = match.Groups[4].Value.Trim();
            if (!string.IsNullOrEmpty(propertiesText))
            {
                ParseDynamicProperties(propertiesText, parsedQuery.Properties);
            }
        }
        else
        {
            throw new ArgumentException($"Format de création d'arête invalide : {query}");
        }
    }

    private void ParseFindNodes(string query, ParsedQuery parsedQuery)
    {
        // Pattern 1 : "find [all] [label] where [conditions] [limit/offset]"
        var match = Regex.Match(query, @"find\s+(all\s+)?(\w+)\s+where\s+(.+?)(?:\s+(limit\s+\d+)(?:\s+offset\s+\d+)?)?$", RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            parsedQuery.NodeLabel = NormalizeLabel(match.Groups[2].Value);
            var conditionsText = match.Groups[3].Value.Trim();
            
            // Parser les conditions avec support des propriétés dynamiques et sous-requêtes
            ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
            
            // Parser limit et offset si présents
            if (match.Groups[4].Success)
            {
                var limitMatch = Regex.Match(match.Groups[4].Value, @"limit\s+(\d+)", RegexOptions.IgnoreCase);
                if (limitMatch.Success)
                {
                    parsedQuery.Limit = int.Parse(limitMatch.Groups[1].Value);
                }
                
                var offsetMatch = Regex.Match(match.Groups[4].Value, @"offset\s+(\d+)", RegexOptions.IgnoreCase);
                if (offsetMatch.Success)
                {
                    parsedQuery.Offset = int.Parse(offsetMatch.Groups[1].Value);
                }
            }
        }
        else
        {
            // Pattern 2 : "find [all] [label]" (sans conditions)
            var simpleMatch = Regex.Match(query, @"find\s+(all\s+)?(\w+)(?:\s+(limit\s+\d+)(?:\s+offset\s+\d+)?)?$", RegexOptions.IgnoreCase);
            
            if (simpleMatch.Success)
            {
                parsedQuery.NodeLabel = NormalizeLabel(simpleMatch.Groups[2].Value);
                
                // Parser limit et offset si présents
                if (simpleMatch.Groups[3].Success)
                {
                    var limitMatch = Regex.Match(simpleMatch.Groups[3].Value, @"limit\s+(\d+)", RegexOptions.IgnoreCase);
                    if (limitMatch.Success)
                    {
                        parsedQuery.Limit = int.Parse(limitMatch.Groups[1].Value);
                    }
                    
                    var offsetMatch = Regex.Match(simpleMatch.Groups[3].Value, @"offset\s+(\d+)", RegexOptions.IgnoreCase);
                    if (offsetMatch.Success)
                    {
                        parsedQuery.Offset = int.Parse(offsetMatch.Groups[1].Value);
                    }
                }
            }
            else
            {
                throw new ArgumentException($"Format de recherche de nœuds invalide : {query}");
            }
        }
    }

    private void ParseFindPath(string query, ParsedQuery parsedQuery)
    {
        // Pattern : "find path from [from] to [to]" avec support des variables
        var match = Regex.Match(query, @"find\s+path\s+from\s+([^\s]+)\s+to\s+([^\s]+)", RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            parsedQuery.FromNode = match.Groups[1].Value.Trim();
            parsedQuery.ToNode = match.Groups[2].Value.Trim();
        }
        else
        {
            throw new ArgumentException($"Format de recherche de chemin invalide : {query}");
        }
    }

    private void ParseFindWithinSteps(string query, ParsedQuery parsedQuery)
    {
        // Pattern : "find [label] from [from] over [steps] steps" avec support des variables
        var match = Regex.Match(query, @"find\s+(\w+)\s+from\s+([^\s]+)\s+over\s+([^\s]+)\s+steps", RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
            parsedQuery.FromNode = match.Groups[2].Value.Trim();
            
            // Gérer les variables dans MaxSteps
            var stepsValue = match.Groups[3].Value.Trim();
            if (stepsValue.StartsWith("$"))
            {
                // C'est une variable, on la stocke temporairement
                parsedQuery.Properties["_maxStepsVariable"] = stepsValue;
                parsedQuery.MaxSteps = 0; // Valeur temporaire
            }
            else if (int.TryParse(stepsValue, out int steps))
            {
                parsedQuery.MaxSteps = steps;
            }
            else
            {
                throw new ArgumentException($"Valeur d'étapes invalide : {stepsValue}");
            }
        }
        else
        {
            throw new ArgumentException($"Format de recherche par étapes invalide : {query}");
        }
    }

    private void ParseUpdateNode(string query, ParsedQuery parsedQuery)
    {
        // Pattern : "update [label] set [properties] where [conditions]"
        var match = Regex.Match(query, @"update\s+(\w+)\s+set\s+(.+?)\s+where\s+(.+)", RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            parsedQuery.NodeLabel = match.Groups[1].Value;
            var propertiesText = match.Groups[2].Value.Trim();
            var conditionsText = match.Groups[3].Value.Trim();
            
            ParseDynamicProperties(propertiesText, parsedQuery.Properties);
            ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
        }
        else
        {
            throw new ArgumentException($"Format de mise à jour invalide : {query}");
        }
    }

    private void ParseDeleteNode(string query, ParsedQuery parsedQuery)
    {
        // Pattern : "delete [label] where [conditions]"
        var match = Regex.Match(query, @"delete\s+(\w+)\s+where\s+(.+)", RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            parsedQuery.NodeLabel = match.Groups[1].Value;
            var conditionsText = match.Groups[2].Value.Trim();
            ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
        }
        else
        {
            throw new ArgumentException($"Format de suppression invalide : {query}");
        }
    }

    private void ParseDeleteEdge(string query, ParsedQuery parsedQuery)
    {
        // Pattern : "delete edge from [from] to [to] [conditions]"
        var match = Regex.Match(query, @"delete\s+edge\s+from\s+(\w+)\s+to\s+(\w+)(?:\s+(.+))?", RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            parsedQuery.FromNode = match.Groups[1].Value;
            parsedQuery.ToNode = match.Groups[2].Value;
            
            var conditionsText = match.Groups[3].Value.Trim();
            if (!string.IsNullOrEmpty(conditionsText))
            {
                ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
            }
        }
        else
        {
            throw new ArgumentException($"Format de suppression d'arête invalide : {query}");
        }
    }

    private void ParseCount(string query, ParsedQuery parsedQuery)
    {
        // Pattern 1 : "count [label] where [conditions]"
        var match = Regex.Match(query, @"count\s+(\w+)\s+where\s+(.+)", RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
            var conditionsText = match.Groups[2].Value.Trim();
            ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
        }
        else
        {
            // Pattern 2 : "count [all] [label]" (sans conditions)
            var simpleMatch = Regex.Match(query, @"count\s+(all\s+)?(\w+)", RegexOptions.IgnoreCase);
            
            if (simpleMatch.Success)
            {
                parsedQuery.NodeLabel = NormalizeLabel(simpleMatch.Groups[2].Value);
            }
            else
            {
                throw new ArgumentException($"Format de comptage invalide : {query}");
            }
        }
    }

    private void ParseAggregate(string query, ParsedQuery parsedQuery)
    {
        // Patterns multiples pour les agrégations
        var patterns = new[]
        {
            // Pattern 1 : "aggregate [label] [function] [property] [where conditions]"
            @"aggregate\s+(\w+)\s+(sum|avg|min|max|count)\s+([^\s]+)(?:\s+where\s+(.+))?",
            // Pattern 2 : "sum/avg/min/max [label] property [property] [where conditions]"
            @"(sum|avg|min|max)\s+(\w+)\s+property\s+([^\s]+)(?:\s+where\s+(.+))?",
            // Pattern 3 : "sum/avg/min/max [property] from [label] [where conditions]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+from\s+(\w+)(?:\s+where\s+(.+))?",
            // Pattern 4 : "sum/avg/min/max [label] [property] [where conditions]"
            @"(sum|avg|min|max)\s+(\w+)\s+([^\s]+)(?:\s+where\s+(.+))?"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                string functionName;
                
                if (pattern.StartsWith("aggregate"))
                {
                    // Pattern 1 : aggregate label function property
                    functionName = match.Groups[2].Value.ToLowerInvariant();
                    parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                    parsedQuery.AggregateProperty = match.Groups[3].Value.Trim();
                    var conditionsText = match.Groups[4].Value.Trim();
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                else if (pattern.Contains("property"))
                {
                    // Pattern 2 : sum persons property age
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.NodeLabel = NormalizeLabel(match.Groups[2].Value);
                    parsedQuery.AggregateProperty = match.Groups[3].Value.Trim();
                    var conditionsText = match.Groups[4].Value.Trim();
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                else if (pattern.Contains("from"))
                {
                    // Pattern 3 : sum age from persons
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.NodeLabel = NormalizeLabel(match.Groups[3].Value);
                    var conditionsText = match.Groups[4].Value.Trim();
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                else
                {
                    // Pattern 4 : sum persons age
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.NodeLabel = NormalizeLabel(match.Groups[2].Value);
                    parsedQuery.AggregateProperty = match.Groups[3].Value.Trim();
                    var conditionsText = match.Groups[4].Value.Trim();
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                
                parsedQuery.AggregateFunction = functionName switch
                {
                    "sum" => AggregateFunction.Sum,
                    "avg" => AggregateFunction.Avg,
                    "min" => AggregateFunction.Min,
                    "max" => AggregateFunction.Max,
                    "count" => AggregateFunction.Count,
                    _ => throw new ArgumentException($"Fonction d'agrégation non supportée : {functionName}")
                };
                
                return;
            }
        }

        throw new ArgumentException($"Format d'agrégation invalide : {query}");
    }

    private void ParseDefineVariable(string query, ParsedQuery parsedQuery)
    {
        query = query.Replace("\n", " ").Trim();
        
        // Pattern direct qui fonctionne
        var pattern = @"define\s+variable\s+([$\w]+)\s+as\s+(.+)";
        var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
        
        if (match.Success)
        {
            var variableName = match.Groups[1].Value;
            var value = match.Groups[2].Value.Trim();
            if (!variableName.StartsWith("$"))
                variableName = "$" + variableName;
            parsedQuery.VariableName = variableName;
            parsedQuery.VariableValue = value;
            return;
        }
        
        throw new ArgumentException($"Format de définition de variable invalide : {query}");
    }

    private void ParseBatchOperation(string query, ParsedQuery parsedQuery)
    {
        try
        {
            var startTime = DateTime.UtcNow;
            
            // Patterns multiples pour les opérations en lot avec support étendu
            var patterns = new[]
            {
                // Pattern 1 : "create batch of [count] [label] with [properties]" - PRIORITÉ HAUTE
                @"create\s+batch\s+of\s+(\d+)\s+(\w+)\s+with\s+(.+?)(?:\s+atomic)?",
                // Pattern 2 : "batch atomic update [label] set [properties] where [conditions]"
                @"batch\s+(atomic\s+)?update\s+(\w+)\s+set\s+(.+?)\s+where\s+(.+)",
                // Pattern 3 : "batch parallel create [label] with [properties]"
                @"batch\s+(parallel|atomic)\s+(create|update|delete)\s+(\w+)(?:\s+set\s+(.+?))?(?:\s+with\s+(.+?))?(?:\s+where\s+(.+))?",
                // Pattern 4 : "batch update [label] set [properties]"
                @"batch\s+update\s+(\w+)\s+set\s+(.+)",
                // Pattern 5 : "batch create [label] with [properties]"
                @"batch\s+create\s+(\w+)\s+with\s+(.+)",
                // Pattern 5b : "batch create [label] with [properties] where [conditions]"
                @"batch\s+create\s+(\w+)\s+with\s+(.+?)\s+where\s+(.+)",
                // Pattern 6 : "batch upsert [label] with [properties] where [conditions]"
                @"batch\s+upsert\s+(\w+)\s+with\s+(.+?)\s+where\s+(.+)",
                // Pattern 7 : "batch [operation] [label] with [properties] where [conditions]"
                @"batch\s+(create|update|delete|upsert)\s+(\w+)\s+with\s+(.+?)\s+where\s+(.+)",
                // Pattern 8 : "batch [operation] [label] with [properties]"
                @"batch\s+(create|update|delete|upsert)\s+(\w+)\s+with\s+(.+)",
                // Pattern 8b : "batch delete [label] where [conditions]" - PRIORITÉ HAUTE POUR DELETE
                @"batch\s+delete\s+(\w+)\s+where\s+(.+)",
                // Pattern 9 : Support des sous-requêtes dans les batch
                @"batch\s+update\s+(\w+)\s+set\s+(.+?)\s+where\s+(\w+)\s+in\s+\((.+?)\)",
                // Pattern 10 : Batch avec taille spécifiée
                @"batch\s+size\s+(\d+)\s+(create|update|delete)\s+(\w+)\s+with\s+(.+?)(?:\s+where\s+(.+))?",
                // Pattern 11 : "batch update [label] with [properties] where [conditions]" - NOUVEAU
                @"batch\s+update\s+(\w+)\s+with\s+(.+?)\s+where\s+(.+)"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    if (pattern.Contains("create batch of"))
                    {
                        // Pattern 1 : create batch of 3 companies with name ["Startup1", "Startup2", "Startup3"]
                        ParseCreateBatchOfPattern(match, parsedQuery, query);
                    }
                    else if (pattern.Contains("parallel|atomic"))
                    {
                        // Pattern 3 : batch parallel/atomic operations
                        ParseParallelAtomicBatchPattern(match, parsedQuery);
                    }
                    else if (pattern.Contains("batch size"))
                    {
                        // Pattern 10 : batch avec taille
                        ParseBatchSizePattern(match, parsedQuery);
                    }
                    else if (pattern.Contains("in \\("))
                    {
                        // Pattern 9 : batch avec sous-requête
                        ParseBatchWithSubQueryPattern(match, parsedQuery);
                    }
                    else if (pattern.Contains("upsert"))
                    {
                        // Pattern 6 : batch upsert
                        ParseBatchUpsertPattern(match, parsedQuery);
                    }
                    else if (pattern.Contains("batch update") && pattern.Contains("set"))
                    {
                        // Patterns 2 et 4 : batch update
                        ParseBatchUpdatePattern(match, parsedQuery);
                    }
                    else if (pattern.Contains("batch update") && pattern.Contains("with"))
                    {
                        // Pattern 11 : batch update avec "with"
                        ParseBatchUpdateWithPattern(match, parsedQuery);
                    }
                    else if (pattern.Contains("batch create"))
                    {
                        // Pattern 5 : batch create
                        ParseBatchCreatePattern(match, parsedQuery);
                    }
                    else if (pattern.Contains("batch delete") && pattern.Contains("where"))
                    {
                        // Pattern 8b : batch delete where - PRIORITÉ HAUTE
                        ParseBatchDeletePattern(match, parsedQuery);
                    }
                    else
                    {
                        // Patterns 7 et 8 : batch générique
                        ParseGenericBatchPattern(match, parsedQuery);
                    }
                    
                    parsedQuery.ParseDuration = DateTime.UtcNow - startTime;
                    parsedQuery.Validate();
                    return;
                }
            }

            // Si aucun pattern ne correspond, essayer une approche simplifiée mais robuste
            ParseFallbackBatchOperation(query, parsedQuery);
            
            parsedQuery.ParseDuration = DateTime.UtcNow - startTime;
            parsedQuery.Validate();
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Erreur lors du parsing des opérations en lot : {ex.Message} pour la requête : {query}");
        }
    }

    private void ParseCreateBatchOfPattern(Match match, ParsedQuery parsedQuery, string originalQuery)
    {
        var count = int.Parse(match.Groups[1].Value);
        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[2].Value);
        var propertiesText = match.Groups[3].Value.Trim();
        parsedQuery.BatchType = BatchOperationType.Create;
        parsedQuery.IsAtomic = originalQuery.Contains("atomic");
        
        // Parser les propriétés avec support des arrays
        ParseBatchProperties(propertiesText, parsedQuery.Properties);
        
        // Créer des opérations batch individuelles
        parsedQuery.BatchOperations = new List<ParsedQuery>();
        
        // Si il y a un array de noms, créer plusieurs nœuds avec des noms différents
        if (parsedQuery.Properties.ContainsKey("name") && parsedQuery.Properties["name"] is List<object> names)
        {
            for (int i = 0; i < Math.Min(count, names.Count); i++)
            {
                var batchQuery = parsedQuery.Clone();
                batchQuery.Type = QueryType.CreateNode;
                batchQuery.Properties = new Dictionary<string, object>(parsedQuery.Properties);
                batchQuery.Properties["name"] = names[i];
                
                // Ajouter un index pour traçabilité
                batchQuery.BatchMetadata["OriginalIndex"] = i;
                batchQuery.BatchMetadata["BatchId"] = Guid.NewGuid().ToString();
                
                parsedQuery.BatchOperations.Add(batchQuery);
            }
        }
        else
        {
            // Créer le nombre spécifié de nœuds avec les mêmes propriétés (mais IDs différents)
            for (int i = 0; i < count; i++)
            {
                var batchQuery = parsedQuery.Clone();
                batchQuery.Type = QueryType.CreateNode;
                batchQuery.Properties = new Dictionary<string, object>(parsedQuery.Properties);
                
                // Ajouter un suffixe numérique si nécessaire pour différencier
                if (batchQuery.Properties.ContainsKey("name"))
                {
                    var baseName = batchQuery.Properties["name"].ToString();
                    batchQuery.Properties["name"] = $"{baseName}_{i + 1}";
                }
                
                batchQuery.BatchMetadata["OriginalIndex"] = i;
                batchQuery.BatchMetadata["BatchId"] = Guid.NewGuid().ToString();
                
                parsedQuery.BatchOperations.Add(batchQuery);
            }
        }
    }

    private void ParseParallelAtomicBatchPattern(Match match, ParsedQuery parsedQuery)
    {
        var mode = match.Groups[1].Value.Trim().ToLower();
        var operation = match.Groups[2].Value.Trim().ToLower();
        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[3].Value);
        
        parsedQuery.IsAtomic = mode == "atomic";
        parsedQuery.BatchType = operation switch
        {
            "create" => BatchOperationType.Create,
            "update" => BatchOperationType.Update,
            "delete" => BatchOperationType.Delete,
            _ => BatchOperationType.Mixed
        };
        
        // Parser les propriétés selon la position dans le match
        var setClause = match.Groups[4].Value;
        var withClause = match.Groups[5].Value;
        var whereClause = match.Groups[6].Value;
        
        if (!string.IsNullOrEmpty(setClause))
        {
            ParseBatchProperties(setClause, parsedQuery.Properties);
        }
        else if (!string.IsNullOrEmpty(withClause))
        {
            ParseBatchProperties(withClause, parsedQuery.Properties);
        }
        
        if (!string.IsNullOrEmpty(whereClause))
        {
            ParseConditionsWithSubQueries(whereClause, parsedQuery.Conditions, parsedQuery);
        }
    }

    private void ParseBatchSizePattern(Match match, ParsedQuery parsedQuery)
    {
        parsedQuery.BatchSize = int.Parse(match.Groups[1].Value);
        var operation = match.Groups[2].Value.Trim().ToLower();
        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[3].Value);
        var propertiesText = match.Groups[4].Value.Trim();
        var conditionsText = match.Groups[5].Value.Trim();
        
        parsedQuery.BatchType = operation switch
        {
            "create" => BatchOperationType.Create,
            "update" => BatchOperationType.Update,
            "delete" => BatchOperationType.Delete,
            _ => BatchOperationType.Mixed
        };
        
        ParseBatchProperties(propertiesText, parsedQuery.Properties);
        
        if (!string.IsNullOrEmpty(conditionsText))
        {
            ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
        }
    }

    private void ParseBatchWithSubQueryPattern(Match match, ParsedQuery parsedQuery)
    {
        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
        var propertiesText = match.Groups[2].Value.Trim();
        var conditionProperty = match.Groups[3].Value.Trim();
        var subQueryText = match.Groups[4].Value.Trim();
        
        parsedQuery.BatchType = BatchOperationType.Update;
        
        ParseBatchProperties(propertiesText, parsedQuery.Properties);
        
        // Parser la sous-requête
        var subQuery = new ParsedQuery
        {
            Type = QueryType.SubQuery,
            ParentQuery = parsedQuery,
            SubQueryDepth = parsedQuery.SubQueryDepth + 1
        };
        
        // Parser le contenu de la sous-requête
        ParseSubQuery(subQueryText, subQuery);
        parsedQuery.SubQueries.Add(subQuery);
        
        // Ajouter la condition avec la sous-requête
        parsedQuery.Conditions[$"{conditionProperty}_subquery_in"] = subQuery;
    }

    private void ParseBatchUpsertPattern(Match match, ParsedQuery parsedQuery)
    {
        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
        var propertiesText = match.Groups[2].Value.Trim();
        var conditionsText = match.Groups[3].Value.Trim();
        
        parsedQuery.BatchType = BatchOperationType.Upsert;
        
        ParseBatchProperties(propertiesText, parsedQuery.Properties);
        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
    }

    private void ParseBatchUpdatePattern(Match match, ParsedQuery parsedQuery)
    {
        // Gérer les deux formats: avec et sans "atomic"
        if (match.Groups.Count > 4 && !string.IsNullOrEmpty(match.Groups[1].Value))
        {
            // Format avec "atomic"
            parsedQuery.IsAtomic = match.Groups[1].Value.Trim().ToLower() == "atomic";
            parsedQuery.NodeLabel = NormalizeLabel(match.Groups[2].Value);
            var propertiesText = match.Groups[3].Value.Trim();
            var conditionsText = match.Groups[4].Value.Trim();
            
            parsedQuery.BatchType = BatchOperationType.Update;
            ParseBatchProperties(propertiesText, parsedQuery.Properties);
            ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
        }
        else if (match.Groups.Count > 3)
        {
            // Format avec conditions
            parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
            var propertiesText = match.Groups[2].Value.Trim();
            var conditionsText = match.Groups[3].Value.Trim();
            
            parsedQuery.BatchType = BatchOperationType.Update;
            ParseBatchProperties(propertiesText, parsedQuery.Properties);
            ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
        }
        else
        {
            // Format simple
            parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
            var propertiesText = match.Groups[2].Value.Trim();
            
            parsedQuery.BatchType = BatchOperationType.Update;
            ParseBatchProperties(propertiesText, parsedQuery.Properties);
        }
    }

    private void ParseBatchCreatePattern(Match match, ParsedQuery parsedQuery)
    {
        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
        var propertiesText = match.Groups[2].Value.Trim();
        parsedQuery.BatchType = BatchOperationType.Create;
        ParseBatchProperties(propertiesText, parsedQuery.Properties);
    }

    private void ParseGenericBatchPattern(Match match, ParsedQuery parsedQuery)
    {
        var operation = match.Groups[1].Value.Trim().ToLower();
        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[2].Value);
        var propertiesText = match.Groups[3].Value.Trim();
        
        parsedQuery.BatchType = operation switch
        {
            "create" => BatchOperationType.Create,
            "update" => BatchOperationType.Update,
            "delete" => BatchOperationType.Delete,
            "upsert" => BatchOperationType.Upsert,
            _ => BatchOperationType.Mixed
        };
        
        ParseBatchProperties(propertiesText, parsedQuery.Properties);
        
        if (match.Groups.Count > 4 && !string.IsNullOrEmpty(match.Groups[4].Value))
        {
            var conditionsText = match.Groups[4].Value.Trim();
            ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
        }
    }

    private void ParseFallbackBatchOperation(string query, ParsedQuery parsedQuery)
    {
        // Approche de secours pour les formats non reconnus
        if (query.Contains("batch create"))
        {
            var match = Regex.Match(query, @"batch\s+create\s+(\w+)\s+with\s+(.+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                var propertiesText = match.Groups[2].Value.Trim();
                parsedQuery.BatchType = BatchOperationType.Create;
                ParseBatchProperties(propertiesText, parsedQuery.Properties);
                return;
            }
        }
        
        if (query.Contains("batch update"))
        {
            var match = Regex.Match(query, @"batch\s+update\s+(\w+)\s+set\s+(.+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                var propertiesText = match.Groups[2].Value.Trim();
                parsedQuery.BatchType = BatchOperationType.Update;
                ParseBatchProperties(propertiesText, parsedQuery.Properties);
                return;
            }
        }
        
        if (query.Contains("create batch of"))
        {
            var match = Regex.Match(query, @"create\s+batch\s+of\s+(\d+)\s+(\w+)\s+with\s+(.+)", RegexOptions.IgnoreCase);
            if (match.Success)
            {
                var count = int.Parse(match.Groups[1].Value);
                parsedQuery.NodeLabel = NormalizeLabel(match.Groups[2].Value);
                var propertiesText = match.Groups[3].Value.Trim();
                parsedQuery.BatchType = BatchOperationType.Create;
                ParseBatchProperties(propertiesText, parsedQuery.Properties);
                
                // Créer les opérations batch
                parsedQuery.BatchOperations = new List<ParsedQuery>();
                for (int i = 0; i < count; i++)
                {
                    var batchQuery = parsedQuery.Clone();
                    batchQuery.Type = QueryType.CreateNode;
                    batchQuery.BatchMetadata["OriginalIndex"] = i;
                    parsedQuery.BatchOperations.Add(batchQuery);
                }
                return;
            }
        }

        throw new ArgumentException($"Format d'opération en lot invalide : {query}");
    }

    /// <summary>
    /// Parse les conditions avec support des sous-requêtes
    /// </summary>
    private void ParseConditionsWithSubQueries(string conditionsText, Dictionary<string, object> conditions, ParsedQuery parentQuery)
    {
        if (string.IsNullOrWhiteSpace(conditionsText))
            return;

        // Détecter les sous-requêtes dans les conditions
        var subQueryPatterns = new[]
        {
            @"(\w+)\s+in\s+\(([^)]+)\)",
            @"(\w+)\s+not\s+in\s+\(([^)]+)\)",
            @"(\w+)\s+exists\s+\(([^)]+)\)",
            @"(\w+)\s+not\s+exists\s+\(([^)]+)\)",
            @"(\w+)\s+contains\s+\(([^)]+)\)",
            @"(\w+)\s+any\s+\(([^)]+)\)",
            @"(\w+)\s+all\s+\(([^)]+)\)"
        };

        foreach (var pattern in subQueryPatterns)
        {
            var matches = Regex.Matches(conditionsText, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                var property = match.Groups[1].Value;
                var subQueryText = match.Groups[2].Value;
                var @operator = ExtractSubQueryOperator(pattern);

                // Créer et parser la sous-requête
                var subQuery = new ParsedQuery
                {
                    Type = QueryType.SubQuery,
                    ParentQuery = parentQuery,
                    SubQueryDepth = parentQuery.SubQueryDepth + 1,
                    SubQueryOperator = @operator
                };

                ParseSubQuery(subQueryText, subQuery);
                parentQuery.SubQueries.Add(subQuery);

                // Ajouter la condition avec référence à la sous-requête
                var conditionKey = $"{property}_subquery_{@operator.ToString().ToLower()}";
                conditions[conditionKey] = subQuery;

                // Supprimer le texte de la sous-requête du texte des conditions
                conditionsText = conditionsText.Replace(match.Value, "");
            }
        }

        // Parser les conditions normales restantes
        if (!string.IsNullOrWhiteSpace(conditionsText.Trim()))
        {
            ParseConditions(conditionsText.Trim(), conditions);
        }
    }

    private SubQueryOperator ExtractSubQueryOperator(string pattern)
    {
        if (pattern.Contains("not in")) return SubQueryOperator.NotIn;
        if (pattern.Contains(" in ")) return SubQueryOperator.In;
        if (pattern.Contains("not exists")) return SubQueryOperator.NotExists;
        if (pattern.Contains("exists")) return SubQueryOperator.Exists;
        if (pattern.Contains("contains")) return SubQueryOperator.Contains;
        if (pattern.Contains("any")) return SubQueryOperator.Any;
        if (pattern.Contains("all")) return SubQueryOperator.All;
        
        return SubQueryOperator.In; // Par défaut
    }

    /// <summary>
    /// Parse une sous-requête améliorée
    /// </summary>
    private void ParseSubQuery(string query, ParsedQuery parsedQuery)
    {
        try
        {
            // Patterns pour sous-requêtes
            var patterns = new[]
            {
                // Pattern 1: "select [property] from [label] where [conditions]"
                @"select\s+(\w+|\*)\s+from\s+(\w+)(?:\s+where\s+(.+))?",
                // Pattern 2: "find [all] [label] where [conditions]"
                @"find\s+(?:all\s+)?(\w+)(?:\s+where\s+(.+))?",
                // Pattern 3: "count [all] [label] where [conditions]"
                @"count\s+(?:all\s+)?(\w+)(?:\s+where\s+(.+))?",
                // Pattern 4: "[aggregate_function]([property]) from [label] where [conditions]"
                @"(sum|avg|min|max|count)\s*\(\s*(\w+)\s*\)\s+from\s+(\w+)(?:\s+where\s+(.+))?"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    if (pattern.Contains("select"))
                    {
                        // Pattern 1: SELECT
                        parsedQuery.SubQueryProperty = match.Groups[1].Value;
                        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[2].Value);
                        parsedQuery.Type = QueryType.FindNodes;
                        
                        if (match.Groups.Count > 3 && !string.IsNullOrEmpty(match.Groups[3].Value))
                        {
                            ParseConditions(match.Groups[3].Value, parsedQuery.Conditions);
                        }
                    }
                    else if (pattern.Contains("find"))
                    {
                        // Pattern 2: FIND
                        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                        parsedQuery.Type = QueryType.FindNodes;
                        
                        if (match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[2].Value))
                        {
                            ParseConditions(match.Groups[2].Value, parsedQuery.Conditions);
                        }
                    }
                    else if (pattern.Contains("count"))
                    {
                        // Pattern 3: COUNT
                        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                        parsedQuery.Type = QueryType.Count;
                        
                        if (match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[2].Value))
                        {
                            ParseConditions(match.Groups[2].Value, parsedQuery.Conditions);
                        }
                    }
                    else
                    {
                        // Pattern 4: AGGREGATE
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
                        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[3].Value);
                        parsedQuery.Type = QueryType.Aggregate;
                        
                        if (match.Groups.Count > 4 && !string.IsNullOrEmpty(match.Groups[4].Value))
                        {
                            ParseConditions(match.Groups[4].Value, parsedQuery.Conditions);
                        }
                    }
                    
                    return;
                }
            }

            // Si aucun pattern ne correspond, essayer un parsing simple
            var simpleMatch = Regex.Match(query, @"(\w+)", RegexOptions.IgnoreCase);
            if (simpleMatch.Success)
            {
                parsedQuery.NodeLabel = NormalizeLabel(simpleMatch.Groups[1].Value);
                parsedQuery.Type = QueryType.FindNodes;
            }
            else
            {
                throw new ArgumentException($"Format de sous-requête invalide : {query}");
            }
        }
        catch (Exception ex)
        {
            parsedQuery.ValidationErrors.Add($"Erreur de parsing de sous-requête: {ex.Message}");
        }
    }

    /// <summary>
    /// Parse les propriétés dynamiques avec support avancé des types et métadonnées
    /// </summary>
    private void ParseDynamicProperties(string propertiesText, Dictionary<string, object> properties)
    {
        // Pattern amélioré pour supporter les valeurs complexes comme les arrays et objets
        // Gère : property1 value1 and property2 [val1, val2, val3] and property3 {key1: val1, key2: val2}
        var matches = Regex.Matches(propertiesText, @"(\w+)\s+([^\s](?:[^a]|a(?!nd\s))*?)(?:\s+and\s|$)", RegexOptions.IgnoreCase);
        
        // Si le pattern complexe échoue, essayer le pattern simple
        if (matches.Count == 0)
        {
            matches = Regex.Matches(propertiesText, @"(\w+)\s+([^\s]+)(?:\s+and\s+|$)");
        }
        
        foreach (Match match in matches)
        {
            var key = match.Groups[1].Value;
            var value = ParseDynamicValue(match.Groups[2].Value.Trim());
            properties[key] = value;
        }
        
        // Si aucun match, essayer une approche alternative pour les cas complexes
        if (properties.Count == 0)
        {
            ParseDynamicPropertiesAlternative(propertiesText, properties);
        }
    }

    /// <summary>
    /// Méthode alternative de parsing pour les propriétés dynamiques complexes
    /// </summary>
    private void ParseDynamicPropertiesAlternative(string propertiesText, Dictionary<string, object> properties)
    {
        // Diviser par " and " tout en préservant les arrays et objets
        var parts = new List<string>();
        var currentPart = "";
        var bracketCount = 0;
        var braceCount = 0;
        var i = 0;
        
        while (i < propertiesText.Length)
        {
            var c = propertiesText[i];
            
            if (c == '[') bracketCount++;
            else if (c == ']') bracketCount--;
            else if (c == '{') braceCount++;
            else if (c == '}') braceCount--;
            
            // Vérifier si on a " and " et qu'on n'est pas dans un array ou objet
            if (bracketCount == 0 && braceCount == 0 && i + 5 < propertiesText.Length && 
                propertiesText.Substring(i, 5).Equals(" and ", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(currentPart.Trim());
                currentPart = "";
                i += 5;
                continue;
            }
            
            currentPart += c;
            i++;
        }
        
        if (!string.IsNullOrEmpty(currentPart.Trim()))
        {
            parts.Add(currentPart.Trim());
        }
        
        // Parser chaque partie : "property value"
        foreach (var part in parts)
        {
            var spaceIndex = part.IndexOf(' ');
            if (spaceIndex > 0)
            {
                var key = part.Substring(0, spaceIndex);
                var value = part.Substring(spaceIndex + 1).Trim();
                properties[key] = ParseDynamicValue(value);
            }
        }
    }

    /// <summary>
    /// Parse une valeur dynamique avec support des types complexes
    /// </summary>
    private object ParseDynamicValue(string inputValue)
    {
        // Vérifier si c'est un objet JSON-like
        if (inputValue.StartsWith("{") && inputValue.EndsWith("}"))
        {
            return ParseObjectValue(inputValue);
        }
        
        // Vérifier si c'est un array
        if (inputValue.StartsWith("[") && inputValue.EndsWith("]"))
        {
            return ParseArrayValue(inputValue);
        }
        
        // Vérifier si c'est une date
        if (DateTime.TryParse(inputValue, out var date))
        {
            return date;
        }
        
        // Vérifier si c'est un nombre
        if (int.TryParse(inputValue, out var intValue))
        {
            return intValue;
        }
        
        if (double.TryParse(inputValue, out var doubleValue))
        {
            return doubleValue;
        }
        
        // Vérifier si c'est un booléen
        if (bool.TryParse(inputValue, out var boolValue))
        {
            return boolValue;
        }
        
        // Sinon, c'est une chaîne (enlever les guillemets si présents)
        return inputValue.Trim('"', '\'');
    }

    /// <summary>
    /// Parse un objet JSON-like : {key1: value1, key2: value2}
    /// </summary>
    private Dictionary<string, object> ParseObjectValue(string objectValue)
    {
        var result = new Dictionary<string, object>();
        
        // Enlever les accolades
        var content = objectValue.Substring(1, objectValue.Length - 2).Trim();
        
        if (string.IsNullOrEmpty(content))
            return result;
        
        // Diviser par les virgules en préservant les valeurs complexes
        var parts = new List<string>();
        var currentPart = "";
        var bracketCount = 0;
        var braceCount = 0;
        var i = 0;
        
        while (i < content.Length)
        {
            var c = content[i];
            
            if (c == '[') bracketCount++;
            else if (c == ']') bracketCount--;
            else if (c == '{') braceCount++;
            else if (c == '}') braceCount--;
            
            // Vérifier si on a une virgule et qu'on n'est pas dans un array ou objet
            if (bracketCount == 0 && braceCount == 0 && c == ',')
            {
                parts.Add(currentPart.Trim());
                currentPart = "";
                i++;
                continue;
            }
            
            currentPart += c;
            i++;
        }
        
        if (!string.IsNullOrEmpty(currentPart.Trim()))
        {
            parts.Add(currentPart.Trim());
        }
        
        // Parser chaque partie : "key: value"
        foreach (var part in parts)
        {
            var colonIndex = part.IndexOf(':');
            if (colonIndex > 0)
            {
                var key = part.Substring(0, colonIndex).Trim();
                var value = part.Substring(colonIndex + 1).Trim();
                result[key] = ParseDynamicValue(value);
            }
        }
        
        return result;
    }

    /// <summary>
    /// Parse un array : [value1, value2, value3]
    /// </summary>
    private List<object> ParseArrayValue(string value)
    {
        var result = new List<object>();
        
        // Enlever les crochets
        var content = value.Substring(1, value.Length - 2).Trim();
        
        if (string.IsNullOrEmpty(content))
            return result;
        
        // Diviser par les virgules
        var parts = content.Split(',');
        
        foreach (var part in parts)
        {
            var trimmedPart = part.Trim();
            if (!string.IsNullOrEmpty(trimmedPart))
            {
                result.Add(ParseDynamicValue(trimmedPart));
            }
        }
        
        return result;
    }

    private void ParseConditions(string conditionsText, Dictionary<string, object> conditions)
    {
        // Pattern amélioré pour supporter AND et OR
        // Ex: "age > 25 and name = John or status = active"
        var parts = SplitConditions(conditionsText);
        
        // Compteur pour générer des clés uniques pour les conditions OR
        var orConditionCounter = 0;
        
        foreach (var part in parts)
        {
            // Pattern étendu pour supporter les fonctions de chaînes et opérateurs avancés
            // Correction : inclure les underscores dans les noms d'opérateurs
            var match = Regex.Match(part.Condition, @"(\w+)\s*(like|starts_with|ends_with|contains|upper|lower|trim|length|substring|replace|[><=!]+)\s*(.+)");
            
            if (match.Success)
            {
                var property = match.Groups[1].Value;
                var operator_ = match.Groups[2].Value;
                var value = ParseDynamicValue(match.Groups[3].Value.Trim());
                
                // Normaliser l'opérateur
                var normalizedOperator = NormalizeOperator(operator_);
                
                // Générer une clé unique pour les conditions OR
                string conditionKey;
                if (part.LogicalOperator == LogicalOperator.Or)
                {
                    conditionKey = $"Or_{property}_{normalizedOperator}";
                    orConditionCounter++;
                }
                else if (part.LogicalOperator == LogicalOperator.And)
                {
                    conditionKey = $"And_{property}_{normalizedOperator}";
                }
                else
                {
                    conditionKey = $"{property}_{normalizedOperator}";
                }
                
                conditions[conditionKey] = value;
            }
        }
    }

    private List<ConditionPart> SplitConditions(string conditionsText)
    {
        var parts = new List<ConditionPart>();
        var currentCondition = "";
        var i = 0;
        
        while (i < conditionsText.Length)
        {
            // Vérifier si on a " and " ou " or "
            if (i + 5 < conditionsText.Length && 
                conditionsText.Substring(i, 5).Equals(" and ", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(new ConditionPart 
                { 
                    Condition = currentCondition.Trim(), 
                    LogicalOperator = LogicalOperator.And 
                });
                currentCondition = "";
                i += 5;
                continue;
            }
            
            if (i + 4 < conditionsText.Length && 
                conditionsText.Substring(i, 4).Equals(" or ", StringComparison.OrdinalIgnoreCase))
            {
                parts.Add(new ConditionPart 
                { 
                    Condition = currentCondition.Trim(), 
                    LogicalOperator = LogicalOperator.Or 
                });
                currentCondition = "";
                i += 4;
                continue;
            }
            
            currentCondition += conditionsText[i];
            i++;
        }
        
        if (!string.IsNullOrEmpty(currentCondition.Trim()))
        {
            parts.Add(new ConditionPart 
            { 
                Condition = currentCondition.Trim(), 
                LogicalOperator = LogicalOperator.None 
            });
        }
        
        return parts;
    }

    private class ConditionPart
    {
        public string Condition { get; set; } = string.Empty;
        public LogicalOperator LogicalOperator { get; set; }
    }

    private enum LogicalOperator
    {
        None,
        And,
        Or
    }

    private string NormalizeOperator(string operator_)
    {
        return operator_.ToLowerInvariant() switch
        {
            "=" => "eq",
            "!=" => "ne",
            ">" => "gt",
            ">=" => "ge",
            "<" => "lt",
            "<=" => "le",
            "like" => "like",
            "contains" => "contains",
            "starts_with" => "starts_with",
            "ends_with" => "ends_with",
            "upper" => "upper",
            "lower" => "lower",
            "trim" => "trim",
            "length" => "length",
            "substring" => "substring",
            "replace" => "replace",
            _ => operator_.ToLowerInvariant()
        };
    }

    /// <summary>
    /// Normalise un label en gérant les pluriels
    /// </summary>
    private string NormalizeLabel(string label)
    {
        // Convertir en minuscules
        var normalized = label.ToLowerInvariant();
        
        // Gérer les pluriels courants
        var singularToPlural = new Dictionary<string, string>
        {
            { "persons", "person" },
            { "people", "person" },
            { "companies", "company" },
            { "projects", "project" },
            { "products", "product" },
            { "departments", "department" },
            { "documents", "document" },
            { "events", "event" },
            { "tests", "test" }
        };

        return singularToPlural.TryGetValue(normalized, out var singular) ? singular : normalized;
    }

    /// <summary>
    /// Parse les propriétés pour les opérations en lot avec support des valeurs complexes
    /// </summary>
    private void ParseBatchProperties(string propertiesText, Dictionary<string, object> properties)
    {
        // Approche ultra-simple et robuste
        var parts = propertiesText.Split(new[] { " and " }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            var trimmedPart = part.Trim();
            var spaceIndex = trimmedPart.IndexOf(' ');
            
            if (spaceIndex > 0)
            {
                var key = trimmedPart.Substring(0, spaceIndex);
                var value = trimmedPart.Substring(spaceIndex + 1).Trim();
                
                // Supprimer les guillemets si présents
                if (value.StartsWith("\"") && value.EndsWith("\""))
                {
                    value = value.Substring(1, value.Length - 2);
                }
                
                // Parser la valeur
                try
                {
                    properties[key] = ParseDynamicValue(value);
                }
                catch
                {
                    // En cas d'erreur, utiliser la valeur brute
                    properties[key] = value;
                }
            }
        }
    }

    private object ParseValue(string value)
    {
        var trimmedValue = value.Trim();
        
        // Vérifier si c'est un array
        if (trimmedValue.StartsWith("[") && trimmedValue.EndsWith("]"))
        {
            return ParseArrayValue(trimmedValue);
        }
        
        // Vérifier si c'est une date
        if (DateTime.TryParse(trimmedValue, out var date))
        {
            return date;
        }
        
        // Vérifier si c'est un nombre
        if (int.TryParse(trimmedValue, out var intValue))
        {
            return intValue;
        }
        
        if (double.TryParse(trimmedValue, out var doubleValue))
        {
            return doubleValue;
        }
        
        // Vérifier si c'est un booléen
        if (bool.TryParse(trimmedValue, out var boolValue))
        {
            return boolValue;
        }
        
        // Sinon, c'est une chaîne (enlever les guillemets si présents)
        if (trimmedValue.StartsWith("\"") && trimmedValue.EndsWith("\""))
        {
            var result = trimmedValue.Substring(1, trimmedValue.Length - 2);
            return result;
        }
        
        if (trimmedValue.StartsWith("'") && trimmedValue.EndsWith("'"))
        {
            var result = trimmedValue.Substring(1, trimmedValue.Length - 2);
            return result;
        }
        
        // Retourner la valeur telle quelle si elle n'est pas vide
        var finalResult = string.IsNullOrEmpty(trimmedValue) ? "" : trimmedValue;
        return finalResult;
    }

    private void ParseBatchUpdateWithPattern(Match match, ParsedQuery parsedQuery)
    {
        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
        var propertiesText = match.Groups[2].Value.Trim();
        var conditionsText = match.Groups[3].Value.Trim();
        
        parsedQuery.BatchType = BatchOperationType.Update;
        ParseBatchProperties(propertiesText, parsedQuery.Properties);
        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
    }

    private void ParseBatchDeletePattern(Match match, ParsedQuery parsedQuery)
    {
        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
        var conditionsText = match.Groups[2].Value.Trim();
        parsedQuery.BatchType = BatchOperationType.Delete;
        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
    }
}

