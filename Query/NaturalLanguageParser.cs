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
        { "subquery", QueryType.SubQuery },
        { "edges", QueryType.FindEdges },
        { "relations", QueryType.FindEdges },
        { "connections", QueryType.FindEdges },
        { "shortest", QueryType.FindPath },
        { "route", QueryType.FindPath },
        { "traverse", QueryType.FindWithinSteps },
        { "neighbors", QueryType.FindWithinSteps },
        { "adjacent", QueryType.FindWithinSteps },
        { "join", QueryType.VirtualJoin },
        { "virtual", QueryType.VirtualJoin },
        { "merge", QueryType.VirtualJoin },
        { "combine", QueryType.VirtualJoin },
        { "group", QueryType.GroupBy },
        { "groupby", QueryType.GroupBy },
        { "order", QueryType.OrderBy },
        { "orderby", QueryType.OrderBy },
        { "sort", QueryType.OrderBy },
        { "having", QueryType.Having },
        { "row_number", QueryType.WindowFunction },
        { "rownumber", QueryType.WindowFunction },
        { "rank", QueryType.WindowFunction },
        { "dense_rank", QueryType.WindowFunction },
        { "denserank", QueryType.WindowFunction },
        { "percent_rank", QueryType.WindowFunction },
        { "percentrank", QueryType.WindowFunction },
        { "ntile", QueryType.WindowFunction },
        { "lead", QueryType.WindowFunction },
        { "lag", QueryType.WindowFunction },
        { "first_value", QueryType.WindowFunction },
        { "firstvalue", QueryType.WindowFunction },
        { "last_value", QueryType.WindowFunction },
        { "lastvalue", QueryType.WindowFunction },
        { "nth_value", QueryType.WindowFunction },
        { "nthvalue", QueryType.WindowFunction },
                            { "indexed", QueryType.ShowIndexedProperties },
                    { "index", QueryType.ShowIndexedProperties },
                    { "stats", QueryType.ShowIndexStats },
                    { "optimize", QueryType.GraphOptimization },
                    { "dijkstra", QueryType.GraphOptimization },
                    { "astar", QueryType.GraphOptimization },
                    { "floyd", QueryType.GraphOptimization },
                    { "components", QueryType.GraphOptimization },
                    { "cycles", QueryType.GraphOptimization },
                    { "diameter", QueryType.GraphOptimization },
                    { "radius", QueryType.GraphOptimization },
                    { "centrality", QueryType.GraphOptimization },
                    { "bridges", QueryType.GraphOptimization },
                    { "articulation", QueryType.GraphOptimization },
                    { "performance", QueryType.GraphOptimization },
                    { "calculate", QueryType.GraphOptimization },
                    { "detect", QueryType.GraphOptimization }
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

        // Traitement spécial pour les commandes d'indexation
        if (queryForTypeDetection.Contains("show indexed properties") || queryForTypeDetection.Contains("show index"))
        {
            parsedQuery.Type = QueryType.ShowIndexedProperties;
        }
        else if (queryForTypeDetection.Contains("show index stats") || queryForTypeDetection.Contains("index stats"))
        {
            parsedQuery.Type = QueryType.ShowIndexStats;
        }
        else if (queryForTypeDetection.StartsWith("add index property"))
        {
            parsedQuery.Type = QueryType.AddIndexProperty;
        }
        else if (queryForTypeDetection.StartsWith("remove index property"))
        {
            parsedQuery.Type = QueryType.RemoveIndexProperty;
        }
        else if (queryForTypeDetection.StartsWith("optimize") || 
                 queryForTypeDetection.StartsWith("dijkstra") ||
                 queryForTypeDetection.StartsWith("astar") ||
                 queryForTypeDetection.StartsWith("floyd") ||
                 queryForTypeDetection.Contains("components") ||
                 queryForTypeDetection.Contains("cycles") ||
                 queryForTypeDetection.Contains("diameter") ||
                 queryForTypeDetection.Contains("radius") ||
                 queryForTypeDetection.Contains("centrality") ||
                 queryForTypeDetection.Contains("bridges") ||
                 queryForTypeDetection.Contains("articulation") ||
                 queryForTypeDetection.Contains("performance metrics") ||
                 queryForTypeDetection.StartsWith("detect") ||
                 queryForTypeDetection.StartsWith("calculate"))
        {
            parsedQuery.Type = QueryType.GraphOptimization;
        }

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
            case QueryType.FindEdges:
                ParseFindEdges(queryForParsing, parsedQuery);
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
            case QueryType.UpdateEdge:
                ParseUpdateEdge(queryForParsing, parsedQuery);
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
            case QueryType.VirtualJoin:
                ParseVirtualJoin(queryForParsing, parsedQuery);
                break;
            case QueryType.GroupBy:
                ParseGroupBy(queryForParsing, parsedQuery);
                break;
            case QueryType.OrderBy:
                ParseOrderBy(queryForParsing, parsedQuery);
                break;
            case QueryType.Having:
                ParseHaving(queryForParsing, parsedQuery);
                break;
            case QueryType.WindowFunction:
                ParseWindowFunction(queryForParsing, parsedQuery);
                break;
            case QueryType.ShowSchema:
                // Pour les commandes de schéma, aucune autre information n'est nécessaire
                break;
            case QueryType.ShowIndexedProperties:
                ParseShowIndexedProperties(queryForParsing, parsedQuery);
                break;
            case QueryType.ShowIndexStats:
                ParseShowIndexStats(queryForParsing, parsedQuery);
                break;
            case QueryType.AddIndexProperty:
                ParseAddIndexProperty(queryForParsing, parsedQuery);
                break;
            case QueryType.RemoveIndexProperty:
                ParseRemoveIndexProperty(queryForParsing, parsedQuery);
                break;
            case QueryType.GraphOptimization:
                ParseGraphOptimization(queryForParsing, parsedQuery);
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
        
        // Cas spéciaux pour les fonctions de fenêtre
        if (query.Contains("() over ("))
        {
            return QueryType.WindowFunction;
        }
        
        // Cas spéciaux pour les opérations en lot
        if (firstWord == "create" && words.Length > 2 && words[1] == "batch" && words[2] == "of")
        {
            return QueryType.BatchOperation;
        }
        
        if (firstWord == "batch" && words.Length > 1)
        {
            return QueryType.BatchOperation;
        }
        
        // Cas spéciaux pour les commandes d'arêtes
        if (firstWord == "create" && words.Length > 2 && words[1] == "edge")
        {
            return QueryType.CreateEdge;
        }
        
        if (firstWord == "update" && words.Length > 2 && words[1] == "edge")
        {
            return QueryType.UpdateEdge;
        }
        
        if (firstWord == "delete" && words.Length > 2 && words[1] == "edge")
        {
            return QueryType.DeleteEdge;
        }
        
        // Cas spéciaux pour les commandes de navigation
        if (firstWord == "traverse" || firstWord == "neighbors" || firstWord == "adjacent")
        {
            return QueryType.FindWithinSteps;
        }
        
        // Cas spéciaux pour les commandes de recherche d'arêtes
        if (firstWord == "find" && words.Length > 1 && words[1] == "edges")
        {
            return QueryType.FindEdges;
        }
        
        // Cas spéciaux pour les commandes de chemins
        if (firstWord == "find" && words.Length > 1 && (words[1] == "path" || words[1] == "shortest" || words[1] == "route"))
        {
            return QueryType.FindPath;
        }
        
        // Cas spécial pour "find bidirectional path" (deux mots)
        if (firstWord == "find" && words.Length > 2 && words[1] == "bidirectional" && words[2] == "path")
        {
            return QueryType.FindPath;
        }
        
        // Cas spécial pour "find shortest path" (deux mots)
        if (firstWord == "find" && words.Length > 2 && words[1] == "shortest" && words[2] == "path")
        {
            return QueryType.FindPath;
        }
        
        // Cas spéciaux pour les commandes de navigation
        if (firstWord == "find" && words.Length > 1 && (words[1] == "neighbors" || words[1] == "adjacent"))
        {
            return QueryType.FindWithinSteps;
        }
        
        // Cas spécial pour "find [label] within [steps] steps from [node]"
        if (firstWord == "find" && words.Length > 4 && words.Contains("within") && words.Contains("steps") && words.Contains("from"))
        {
            return QueryType.FindWithinSteps;
        }
        
        if (QueryKeywords.TryGetValue(firstWord, out var queryType))
        {
            return queryType;
        }

        throw new NotSupportedException($"Commande non reconnue : {firstWord}");
    }

    private void ParseCreateNode(string query, ParsedQuery parsedQuery)
    {
        // Patterns multiples pour supporter différents formats de création de nœuds
        // IMPORTANT: L'ordre est crucial - les patterns plus spécifiques doivent être en premier
        var patterns = new[]
        {
            // Pattern 1 : "create [label] with [properties]" (format avec "with")
            @"create\s+(\w+)\s+with\s+(.+)",
            // Pattern 2 : "create [label] ""name"" [properties]" (format avec guillemets)
            @"create\s+(\w+)\s+""([^""]+)""(?:\s+(.+))?",
            // Pattern 3 : "create [label] [name] [properties]" (format simple)
            @"create\s+(\w+)\s+([^\s]+(?:\s+[^\s]+)*)(?:\s+(.+))?"
        };

        for (int patternIndex = 0; patternIndex < patterns.Length; patternIndex++)
        {
            var pattern = patterns[patternIndex];
            var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                parsedQuery.NodeLabel = match.Groups[1].Value;
                
                if (patternIndex == 0) // Pattern 1 : avec "with"
                {
                    var propertiesText = match.Groups[2].Value.Trim();
                    
                    // Gérer le format "properties {...}"
                    if (propertiesText.StartsWith("properties", StringComparison.OrdinalIgnoreCase))
                    {
                        // Extraire le contenu entre accolades
                        var braceMatch = Regex.Match(propertiesText, @"properties\s*\{([^}]+)\}", RegexOptions.IgnoreCase);
                        if (braceMatch.Success)
                        {
                            var propertiesContent = braceMatch.Groups[1].Value.Trim();
                            ParsePropertiesFromBraceContent(propertiesContent, parsedQuery.Properties);
                        }
                        else
                        {
                            ParseDynamicProperties(propertiesText, parsedQuery.Properties);
                        }
                    }
                    else
                    {
                        ParseDynamicProperties(propertiesText, parsedQuery.Properties);
                    }
                }
                else if (patternIndex == 1) // Pattern 2 : avec guillemets
                {
                    parsedQuery.Properties["name"] = match.Groups[2].Value;
                    
                    var propertiesText = match.Groups.Count > 3 ? match.Groups[3].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(propertiesText))
                    {
                        ParseDynamicProperties(propertiesText, parsedQuery.Properties);
                    }
                }
                else if (patternIndex == 2) // Pattern 3 : sans guillemets
                {
                    parsedQuery.Properties["name"] = match.Groups[2].Value;
                    
                    var propertiesText = match.Groups.Count > 3 ? match.Groups[3].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(propertiesText))
                    {
                        ParseDynamicProperties(propertiesText, parsedQuery.Properties);
                    }
                }
                return;
            }
        }
        
        throw new ArgumentException($"Format de création de nœud invalide : {query}");
    }

    private void ParseCreateEdge(string query, ParsedQuery parsedQuery)
    {
        // Patterns multiples pour supporter différents formats de création d'arêtes
        var patterns = new[]
        {
            // Pattern 1 : "create edge from [label] ""name"" to [label] ""name"" with type [type] [properties]"
            @"create\s+edge\s+from\s+(\w+)\s+""([^""]+)""\s+to\s+(\w+)\s+""([^""]+)""\s+with\s+type\s+(\w+)(?:\s+(.+))?",
            // Pattern 2 : "create edge from ""name"" to ""name"" with type [type] [properties]" (format simple sans labels)
            @"create\s+edge\s+from\s+""([^""]+)""\s+to\s+""([^""]+)""\s+with\s+type\s+(\w+)(?:\s+(.+))?",
            // Pattern 3 : "create edge from [name] to [name] with type [type] [properties]" (format simple sans guillemets)
            @"create\s+edge\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+with\s+type\s+(\w+)(?:\s+(.+))?",
            // Pattern 4 : "connect [from] to [to] with relationship [type] [properties]"
            @"connect\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+with\s+relationship\s+(\w+)(?:\s+(.+))?",
            // Pattern 5 : "link [from] to [to] with type [type] [properties]"
            @"link\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+with\s+type\s+(\w+)(?:\s+(.+))?",
            // Pattern 6 : "relate [from] to [to] with [type] [properties]"
            @"relate\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+with\s+(\w+)(?:\s+(.+))?"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (match.Groups.Count >= 6)
                {
                    // Pattern 1 : avec guillemets et labels
                    parsedQuery.FromNode = $"{match.Groups[1].Value} \"{match.Groups[2].Value}\"";
                    parsedQuery.ToNode = $"{match.Groups[3].Value} \"{match.Groups[4].Value}\"";
                    parsedQuery.EdgeType = match.Groups[5].Value;
                    
                    var propertiesText = match.Groups.Count > 6 ? match.Groups[6].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(propertiesText))
                    {
                        ParseDynamicProperties(propertiesText, parsedQuery.Properties);
                    }
                }
                else if (pattern.Contains("""([^""]+)""") && !pattern.Contains("(\\w+)\\s+\""))
                {
                    // Pattern 2 : avec guillemets mais sans labels
                    parsedQuery.FromNode = match.Groups[1].Value;
                    parsedQuery.ToNode = match.Groups[2].Value;
                    parsedQuery.EdgeType = match.Groups[3].Value;
                    
                    var propertiesText = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(propertiesText))
                    {
                        ParseDynamicProperties(propertiesText, parsedQuery.Properties);
                    }
                }
                else
                {
                    // Patterns 3-6 : sans guillemets
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    parsedQuery.ToNode = match.Groups[2].Value.Trim();
                    parsedQuery.EdgeType = match.Groups[3].Value;
                    
                    var propertiesText = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(propertiesText))
                    {
                        ParseDynamicProperties(propertiesText, parsedQuery.Properties);
                    }
                }
                return;
            }
        }
        
        throw new ArgumentException($"Format de création d'arête invalide : {query}");
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

    private void ParseFindEdges(string query, ParsedQuery parsedQuery)
    {
        // Patterns multiples pour supporter différents formats de recherche d'arêtes
        var patterns = new[]
        {
            // Pattern 1 : "find edges from [label] ""name"" to [label] ""name""
            @"find\s+(?:edges?|relations?|connections?)\s+from\s+(\w+)\s+""([^""]+)""\s+to\s+(\w+)\s+""([^""]+)""(?:\s+where\s+(.+))?",
            // Pattern 2 : "find edges from ""name"" to ""name""
            @"find\s+(?:edges?|relations?|connections?)\s+from\s+""([^""]+)""\s+to\s+""([^""]+)""(?:\s+where\s+(.+))?",
            // Pattern 2b : "find edges from ""name"" to ""name"" (format alternatif)
            @"find\s+(?:edges?|relations?|connections?)\s+from\s+""([^""]+)""\s+to\s+""([^""]+)""",
            // Pattern 3 : "find edges from [name] to [name]" avec support des noms avec espaces
            @"find\s+(?:edges?|relations?|connections?)\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)(?:\s+where\s+(.+))?",
            // Pattern 4 : "find edges between [from] and [to]"
            @"find\s+(?:edges?|relations?|connections?)\s+between\s+([^\s]+(?:\s+[^\s]+)*)\s+and\s+([^\s]+(?:\s+[^\s]+)*)(?:\s+where\s+(.+))?",
            // Pattern 5 : "find edges [from] to [to]"
            @"find\s+(?:edges?|relations?|connections?)\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)(?:\s+where\s+(.+))?",
            // Pattern 6 : "find edges where type = [type]"
            @"find\s+(?:edges?|relations?|connections?)\s+where\s+(.+)",
            // Pattern 7 : "find edges from [name]"
            @"find\s+(?:edges?|relations?|connections?)\s+from\s+([^\s]+(?:\s+[^\s]+)*)(?:\s+where\s+(.+))?",
            // Pattern 8 : "find edges to [name]"
            @"find\s+(?:edges?|relations?|connections?)\s+to\s+([^\s]+(?:\s+[^\s]+)*)(?:\s+where\s+(.+))?",
            // Pattern 9 : "find edges from [label] to [variable] where [conditions]"
            @"find\s+(?:edges?|relations?|connections?)\s+from\s+(\w+)\s+to\s+(\$[^\s]+)(?:\s+where\s+(.+))?",
            // Pattern 10 : "find [label] within [steps] steps from [variable] where [conditions]"
            @"find\s+(\w+)\s+within\s+([^\s]+)\s+steps\s+from\s+(\$[^\s]+)\s+where\s+(.+)",
            // Pattern 11 : "find edges from [label] to [variable] (format simple)"
            @"find\s+(?:edges?|relations?|connections?)\s+from\s+(\w+)\s+to\s+(\$[^\s]+)",
            // Pattern 12 : "find [label] within [steps] steps from [variable] (format simple)"
            @"find\s+(\w+)\s+within\s+([^\s]+)\s+steps\s+from\s+(\$[^\s]+)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (pattern.Contains("""([^""]+)"""))
                {
                    // Pattern 1 : avec guillemets et labels
                    parsedQuery.FromNode = $"{match.Groups[1].Value} \"{match.Groups[2].Value}\"";
                    parsedQuery.ToNode = $"{match.Groups[3].Value} \"{match.Groups[4].Value}\"";
                    
                    var conditionsText = match.Groups.Count > 5 ? match.Groups[5].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                else if (pattern.Contains("""([^""]+)""") && !pattern.Contains("(\\w+)\\s+\""))
                {
                    // Pattern 2 : avec guillemets mais sans labels
                    parsedQuery.FromNode = match.Groups[1].Value;
                    parsedQuery.ToNode = match.Groups[2].Value;
                    
                    var conditionsText = match.Groups.Count > 3 ? match.Groups[3].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                else if (pattern.Contains("from\\s+\"") && pattern.Contains("to\\s+\"") && !pattern.Contains("(\\w+)\\s+\""))
                {
                    // Pattern 2 alternatif : avec guillemets mais sans labels
                    parsedQuery.FromNode = match.Groups[1].Value;
                    parsedQuery.ToNode = match.Groups[2].Value;
                    
                    var conditionsText = match.Groups.Count > 3 ? match.Groups[3].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                else if (pattern.Contains("from\\s+\"") && pattern.Contains("to\\s+\"") && pattern.Contains("2b"))
                {
                    // Pattern 2b : format alternatif sans conditions
                    parsedQuery.FromNode = match.Groups[1].Value;
                    parsedQuery.ToNode = match.Groups[2].Value;
                }
                else if (pattern.Contains("where") && !pattern.Contains("from") && !pattern.Contains("to"))
                {
                    // Pattern 6 : recherche par conditions seulement
                    var conditionsText = match.Groups[1].Value.Trim();
                    ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                }
                else if (pattern.Contains("\\$") && pattern.Contains("from"))
                {
                    // Pattern 9-10 : avec variables
                    if (pattern.Contains("to\\s+\\$"))
                    {
                        // Pattern 9 : find edges from label to variable where conditions
                        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                        parsedQuery.ToNode = match.Groups[2].Value; // Variable
                        var conditionsText = match.Groups.Count > 3 ? match.Groups[3].Value.Trim() : "";
                        if (!string.IsNullOrEmpty(conditionsText))
                        {
                            ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                        }
                    }
                    else if (pattern.Contains("from\\s+\\$"))
                    {
                        // Pattern 10 : find label within steps steps from variable where conditions
                        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                        parsedQuery.MaxSteps = int.Parse(match.Groups[2].Value);
                        parsedQuery.FromNode = match.Groups[3].Value; // Variable
                        var conditionsText = match.Groups[4].Value.Trim();
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                else if (pattern.Contains("\\$") && !pattern.Contains("from"))
                {
                    // Pattern 11-12 : avec variables (format simple)
                    if (pattern.Contains("to\\s+\\$"))
                    {
                        // Pattern 11 : find edges from label to variable (sans conditions)
                        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                        parsedQuery.ToNode = match.Groups[2].Value; // Variable
                    }
                    else if (pattern.Contains("from\\s+\\$"))
                    {
                        // Pattern 12 : find label within steps steps from variable (sans conditions)
                        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                        parsedQuery.MaxSteps = int.Parse(match.Groups[2].Value);
                        parsedQuery.FromNode = match.Groups[3].Value; // Variable
                    }
                }
                else if (pattern.Contains("from") && !pattern.Contains("to"))
                {
                    // Pattern 6 : recherche depuis un nœud
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    
                    var conditionsText = match.Groups.Count > 2 ? match.Groups[2].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                else if (pattern.Contains("to") && !pattern.Contains("from"))
                {
                    // Pattern 7 : recherche vers un nœud
                    parsedQuery.ToNode = match.Groups[1].Value.Trim();
                    
                    var conditionsText = match.Groups.Count > 2 ? match.Groups[2].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                else
                {
                    // Patterns 2-4 : sans guillemets
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    parsedQuery.ToNode = match.Groups[2].Value.Trim();
                    
                    var conditionsText = match.Groups.Count > 3 ? match.Groups[3].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                return;
            }
        }
        
        throw new ArgumentException($"Format de recherche d'arêtes invalide : {query}");
    }

    private void ParseFindPath(string query, ParsedQuery parsedQuery)
    {
        // Patterns multiples pour supporter différents formats de recherche de chemins
        var patterns = new[]
        {
            // Patterns prioritaires pour les cas spécifiques qui échouent
            // Pattern 1 : "find bidirectional path from [name] to [name]" (format simple)
            @"find\s+bidirectional\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)",
            // Pattern 2 : "find shortest path from [name] to [name] via [edge_type]" (format simple)
            @"find\s+shortest\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+via\s+(\w+)",
            // Pattern 3 : "find shortest path from [name] to [name] avoiding [edge_type]"
            @"find\s+shortest\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+avoiding\s+(\w+)(?:\s+where\s+(.+))?",
            // Pattern 4 : "find shortest path from [name] to [name] via [edge_type] with max steps [number]"
            @"find\s+shortest\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+via\s+(\w+)\s+with\s+max\s+steps\s+(\d+)(?:\s+where\s+(.+))?",
            // Pattern 5 : "find shortest path from [name] to [name]" (priorité haute)
            @"find\s+shortest\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)(?:\s+where\s+(.+))?",
            // Pattern 6 : "find path from [label] ""name"" to [label] ""name""
            @"find\s+(?:path|route)\s+from\s+(\w+)\s+""([^""]+)""\s+to\s+(\w+)\s+""([^""]+)""(?:\s+where\s+(.+))?",
            // Pattern 7 : "find path from [name] to [name]" avec support des noms avec espaces
            @"find\s+(?:path|route)\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)(?:\s+where\s+(.+))?",
            // Pattern 8 : "find path between [from] and [to]"
            @"find\s+(?:path|route)\s+between\s+([^\s]+(?:\s+[^\s]+)*)\s+and\s+([^\s]+(?:\s+[^\s]+)*)(?:\s+where\s+(.+))?",
            // Pattern 9 : "find path [from] to [to]"
            @"find\s+(?:path|route)\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)(?:\s+where\s+(.+))?",
            // Pattern 10 : "find path from [name] to [name] via [edge_type]"
            @"find\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+via\s+(\w+)(?:\s+where\s+(.+))?",
            // Pattern 11 : "find path from [name] to [name] avoiding [edge_type]"
            @"find\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+avoiding\s+(\w+)(?:\s+where\s+(.+))?",
            // Pattern 12 : "find path from [name] to [name] with max steps [number]"
            @"find\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+with\s+max\s+steps\s+(\d+)(?:\s+where\s+(.+))?",
            // Pattern 13 : "find bidirectional path from [name] to [name]"
            @"find\s+bidirectional\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)(?:\s+where\s+(.+))?",
            // Pattern 14 : "find shortest path from [name] to [name] via [edge_type]"
            @"find\s+shortest\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+via\s+(\w+)(?:\s+where\s+(.+))?",
            // Pattern 15 : "find path from [name] to [name] via [edge_type] (priorité haute)"
            @"find\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+via\s+(\w+)",
            // Pattern 16 : "find path from [name] to [name] avoiding [edge_type] (priorité haute)"
            @"find\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+avoiding\s+(\w+)",
            // Pattern 17 : "find path from [name] to [name] with max steps [number] (priorité haute)"
            @"find\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+with\s+max\s+steps\s+(\d+)",
            // Pattern 18 : "find bidirectional path from [name] to [name] (priorité haute)"
            @"find\s+bidirectional\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)",
            // Pattern 19 : "find path from [name] to [name] via [edge_type] (format simple)"
            @"find\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+via\s+(\w+)(?:\s+where\s+(.+))?",
            // Pattern 20 : "find path from [name] to [name] avoiding [edge_type] (format simple)"
            @"find\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+avoiding\s+(\w+)(?:\s+where\s+(.+))?",
            // Pattern 21 : "find path from [name] to [name] with max steps [number] (format simple)"
            @"find\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+with\s+max\s+steps\s+(\d+)(?:\s+where\s+(.+))?",
            // Pattern 22 : "find bidirectional path from [name] to [name] (format simple)"
            @"find\s+bidirectional\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)(?:\s+where\s+(.+))?",
            // Pattern 23 : "find path from [name] to [name] via [edge_type] (format sans conditions)"
            @"find\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+via\s+(\w+)",
            // Pattern 24 : "find path from [name] to [name] avoiding [edge_type] (format sans conditions)"
            @"find\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+avoiding\s+(\w+)",
            // Pattern 25 : "find path from [name] to [name] with max steps [number] (format sans conditions)"
            @"find\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+with\s+max\s+steps\s+(\d+)",
            // Pattern 26 : "find bidirectional path from [name] to [name] (format sans conditions)"
            @"find\s+bidirectional\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)",
            // Nouveaux patterns pour les fonctionnalités avancées
            // Pattern 27 : "find path from [name] to [name] via [edge_type] avoiding [edge_type]"
            @"find\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+via\s+(\w+)\s+avoiding\s+(\w+)(?:\s+where\s+(.+))?",
            // Pattern 28 : "find path from [name] to [name] via [edge_type] with max steps [number]"
            @"find\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+via\s+(\w+)\s+with\s+max\s+steps\s+(\d+)(?:\s+where\s+(.+))?",
            // Pattern 29 : "find path from [name] to [name] avoiding [edge_type] with max steps [number]"
            @"find\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+avoiding\s+(\w+)\s+with\s+max\s+steps\s+(\d+)(?:\s+where\s+(.+))?",
            // Pattern 30 : "find bidirectional path from [name] to [name] via [edge_type]"
            @"find\s+bidirectional\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+via\s+(\w+)(?:\s+where\s+(.+))?",
            // Pattern 31 : "find bidirectional path from [name] to [name] avoiding [edge_type]"
            @"find\s+bidirectional\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+avoiding\s+(\w+)(?:\s+where\s+(.+))?",
            // Pattern 32 : "find bidirectional path from [name] to [name] with max steps [number]"
            @"find\s+bidirectional\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+with\s+max\s+steps\s+(\d+)(?:\s+where\s+(.+))?",
            // Pattern 33 : "find shortest path from [name] to [name] via [edge_type] avoiding [edge_type]"
            @"find\s+shortest\s+path\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+via\s+(\w+)\s+avoiding\s+(\w+)(?:\s+where\s+(.+))?"
        };

        for (int i = 0; i < patterns.Length; i++)
        {
            var pattern = patterns[i];
            var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                // Patterns prioritaires pour les cas spécifiques qui échouent
                if (i == 0) // Pattern 1 : bidirectional path (format simple)
                {
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    parsedQuery.ToNode = match.Groups[2].Value.Trim();
                    parsedQuery.Properties["bidirectional"] = true;
                }
                else if (i == 1) // Pattern 2 : shortest path avec via (format simple)
                {
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    parsedQuery.ToNode = match.Groups[2].Value.Trim();
                    parsedQuery.EdgeType = match.Groups[3].Value.Trim();
                    parsedQuery.Properties["shortest"] = true;
                }
                else if (i == 2) // Pattern 3 : shortest path avec avoiding
                {
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    parsedQuery.ToNode = match.Groups[2].Value.Trim();
                    parsedQuery.Properties["avoid_edge_type"] = match.Groups[3].Value.Trim();
                    parsedQuery.Properties["shortest"] = true;
                    
                    var conditionsText = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                else if (i == 3) // Pattern 4 : shortest path avec via et max steps
                {
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    parsedQuery.ToNode = match.Groups[2].Value.Trim();
                    parsedQuery.EdgeType = match.Groups[3].Value.Trim();
                    parsedQuery.MaxSteps = int.Parse(match.Groups[4].Value);
                    parsedQuery.Properties["shortest"] = true;
                    
                    var conditionsText = match.Groups.Count > 5 ? match.Groups[5].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                // Pattern 6 : avec guillemets et labels (5+ groupes)
                else if (match.Groups.Count >= 5 && i == 5)
                {
                    parsedQuery.FromNode = $"{match.Groups[1].Value} \"{match.Groups[2].Value}\"";
                    parsedQuery.ToNode = $"{match.Groups[3].Value} \"{match.Groups[4].Value}\"";
                    
                    var conditionsText = match.Groups.Count > 5 ? match.Groups[5].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                // Pattern 13 : bidirectional path avec conditions
                else if (i == 12)
                {
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    parsedQuery.ToNode = match.Groups[2].Value.Trim();
                    parsedQuery.Properties["bidirectional"] = true;
                    
                    var conditionsText = match.Groups.Count > 3 ? match.Groups[3].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                // Patterns 26-28 : bidirectional path
                else if (i >= 25 && i <= 27)
                {
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    parsedQuery.ToNode = match.Groups[2].Value.Trim();
                    parsedQuery.Properties["bidirectional"] = true;
                    
                    if (i == 25) // Pattern 26 : avec via
                    {
                        parsedQuery.EdgeType = match.Groups[3].Value.Trim();
                    }
                    else if (i == 26) // Pattern 27 : avec avoiding
                    {
                        parsedQuery.Properties["avoid_edge_type"] = match.Groups[3].Value.Trim();
                    }
                    else // Pattern 28 : avec max steps
                    {
                        parsedQuery.MaxSteps = int.Parse(match.Groups[3].Value);
                    }
                    
                    var conditionsText = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                // Pattern 5 : shortest path simple
                else if (i == 4)
                {
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    parsedQuery.ToNode = match.Groups[2].Value.Trim();
                    parsedQuery.Properties["shortest"] = true;
                    
                    var conditionsText = match.Groups.Count > 3 ? match.Groups[3].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                // Pattern 14 : shortest path avec via
                else if (i == 13)
                {
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    parsedQuery.ToNode = match.Groups[2].Value.Trim();
                    parsedQuery.EdgeType = match.Groups[3].Value.Trim();
                    parsedQuery.Properties["shortest"] = true;
                    
                    var conditionsText = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                // Patterns 23-25 : via avec avoiding/max steps
                else if (i >= 22 && i <= 24)
                {
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    parsedQuery.ToNode = match.Groups[2].Value.Trim();
                    parsedQuery.EdgeType = match.Groups[3].Value.Trim();
                    
                    if (i == 22) // Pattern 23 : avec avoiding
                    {
                        parsedQuery.Properties["avoid_edge_type"] = match.Groups[4].Value.Trim();
                    }
                    else if (i == 23) // Pattern 24 : avec max steps
                    {
                        parsedQuery.MaxSteps = int.Parse(match.Groups[4].Value);
                    }
                    else // Pattern 25 : avec avoiding et max steps
                    {
                        parsedQuery.Properties["avoid_edge_type"] = match.Groups[3].Value.Trim();
                        parsedQuery.MaxSteps = int.Parse(match.Groups[4].Value);
                    }
                    
                    var conditionsText = match.Groups.Count > 5 ? match.Groups[5].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                // Patterns 6-8 : via, avoiding, max steps simples
                else if (i >= 5 && i <= 7)
                {
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    parsedQuery.ToNode = match.Groups[2].Value.Trim();
                    
                    if (i == 5) // Pattern 6 : via
                    {
                        parsedQuery.EdgeType = match.Groups[3].Value.Trim();
                    }
                    else if (i == 6) // Pattern 7 : avoiding
                    {
                        parsedQuery.Properties["avoid_edge_type"] = match.Groups[3].Value.Trim();
                    }
                    else // Pattern 8 : max steps
                    {
                        parsedQuery.MaxSteps = int.Parse(match.Groups[3].Value);
                    }
                    
                    var conditionsText = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                // Patterns 11-22 : formats simples sans conditions
                else if (i >= 10 && i <= 21)
                {
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    parsedQuery.ToNode = match.Groups[2].Value.Trim();
                    
                    if (i == 10 || i == 18) // via
                    {
                        parsedQuery.EdgeType = match.Groups[3].Value.Trim();
                    }
                    else if (i == 11 || i == 19) // avoiding
                    {
                        parsedQuery.Properties["avoid_edge_type"] = match.Groups[3].Value.Trim();
                    }
                    else if (i == 12 || i == 20) // max steps
                    {
                        parsedQuery.MaxSteps = int.Parse(match.Groups[3].Value);
                    }
                    else if (i == 13 || i == 21) // bidirectional
                    {
                        parsedQuery.Properties["bidirectional"] = true;
                    }
                }
                // Patterns 14-18 : formats avec conditions
                else if (i >= 13 && i <= 17)
                {
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    parsedQuery.ToNode = match.Groups[2].Value.Trim();
                    
                    if (i == 14) // via avec conditions
                    {
                        parsedQuery.EdgeType = match.Groups[3].Value.Trim();
                        var conditionsText = match.Groups[4].Value.Trim();
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                    else if (i == 15) // avoiding avec conditions
                    {
                        parsedQuery.Properties["avoid_edge_type"] = match.Groups[3].Value.Trim();
                        var conditionsText = match.Groups[4].Value.Trim();
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                    else if (i == 16) // max steps avec conditions
                    {
                        parsedQuery.MaxSteps = int.Parse(match.Groups[3].Value);
                        var conditionsText = match.Groups[4].Value.Trim();
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                    else if (i == 17) // bidirectional avec conditions
                    {
                        parsedQuery.Properties["bidirectional"] = true;
                        var conditionsText = match.Groups[3].Value.Trim();
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                // Patterns 3-5 : formats de base
                else
                {
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    parsedQuery.ToNode = match.Groups[2].Value.Trim();
                    
                    var conditionsText = match.Groups.Count > 3 ? match.Groups[3].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                return;
            }
        }
        
        throw new ArgumentException($"Format de recherche de chemin invalide : {query}");
    }

    private void ParseFindWithinSteps(string query, ParsedQuery parsedQuery)
    {
        // Patterns multiples pour supporter différents formats de recherche par étapes
        var patterns = new[]
        {
            // Pattern 1 : "find [label] within [steps] steps from [label] ""name""
            @"find\s+(\w+)\s+within\s+([^\s]+)\s+steps\s+from\s+(\w+)\s+""([^""]+)""(?:\s+where\s+(.+))?",
            // Pattern 2 : "find [label] within [steps] steps from [name]" avec support des noms avec espaces
            @"find\s+(\w+)\s+within\s+([^\s]+)\s+steps\s+from\s+([^\s]+(?:\s+[^\s]+)*)(?:\s+where\s+(.+))?",
            // Pattern 3 : "find [label] from [name] over [steps] steps"
            @"find\s+(\w+)\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+over\s+([^\s]+)\s+steps(?:\s+where\s+(.+))?",
            // Pattern 4 : "find [label] within [steps] of [name]"
            @"find\s+(\w+)\s+within\s+([^\s]+)\s+of\s+([^\s]+(?:\s+[^\s]+)*)(?:\s+where\s+(.+))?",
            // Pattern 5 : "find neighbors of [name] within [steps] steps"
            @"find\s+(?:neighbors?|adjacent)\s+of\s+([^\s]+(?:\s+[^\s]+)*)\s+within\s+([^\s]+)\s+steps(?:\s+where\s+(.+))?",
            // Pattern 6 : "find [label] connected to [name] via [edge_type]"
            @"find\s+(\w+)\s+connected\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+via\s+(\w+)(?:\s+where\s+(.+))?",
            // Pattern 7 : "traverse from [name] to [label] within [steps] steps"
            @"traverse\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+(\w+)\s+within\s+([^\s]+)\s+steps(?:\s+where\s+(.+))?",
            // Pattern 8 : "find [label] within [steps] steps from [name] where [conditions]"
            @"find\s+(\w+)\s+within\s+([^\s]+)\s+steps\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+where\s+(.+)",
            // Pattern 9 : "find [label] connected to [name] via [edge_type] (format simple)"
            @"find\s+(\w+)\s+connected\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+via\s+(\w+)",
            // Pattern 10 : "find [label] within [steps] steps from [name] where [conditions] (format simple)"
            @"find\s+(\w+)\s+within\s+([^\s]+)\s+steps\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+where\s+(.+)",
            // Pattern 11 : "find [label] connected to [name] via [edge_type] (format sans conditions)"
            @"find\s+(\w+)\s+connected\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+via\s+(\w+)",
            // Pattern 12 : "find [label] within [steps] steps from [name] where [conditions] (format alternatif)"
            @"find\s+(\w+)\s+within\s+([^\s]+)\s+steps\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+where\s+(.+)",
            // Pattern 11 : "find [label] reachable from [name] in [steps] steps"
            @"find\s+(\w+)\s+reachable\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+in\s+([^\s]+)\s+steps(?:\s+where\s+(.+))?"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (pattern.Contains("""([^""]+)"""))
                {
                    // Pattern 1 : "find label within steps steps from label ""name"""
                    parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                    var stepsValue = match.Groups[2].Value.Trim();
                    parsedQuery.FromNode = $"{match.Groups[3].Value} \"{match.Groups[4].Value}\"";
                    
                    var conditionsText = match.Groups.Count > 5 ? match.Groups[5].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                else if (pattern.Contains("neighbors") || pattern.Contains("adjacent"))
                {
                    // Pattern 5 : recherche de voisins
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    var stepsValue = match.Groups[2].Value.Trim();
                    parsedQuery.NodeLabel = "neighbor"; // Label spécial pour les voisins
                    
                    var conditionsText = match.Groups.Count > 3 ? match.Groups[3].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                    
                    // Gérer les variables dans MaxSteps
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
                else if (pattern.Contains("connected to") && pattern.Contains("via"))
                {
                    // Pattern 6 : recherche par type de connexion
                    parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                    parsedQuery.FromNode = match.Groups[2].Value.Trim();
                    parsedQuery.EdgeType = match.Groups[3].Value.Trim();
                    
                    var conditionsText = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                else if (pattern.Contains("traverse"))
                {
                    // Pattern 7 : traversée
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    parsedQuery.NodeLabel = NormalizeLabel(match.Groups[2].Value);
                    var stepsValue = match.Groups[3].Value.Trim();
                    
                    var conditionsText = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                    
                    // Gérer les variables dans MaxSteps
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
                else if (pattern.Contains("reachable"))
                {
                    // Pattern 8 : nœuds atteignables
                    parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                    parsedQuery.FromNode = match.Groups[2].Value.Trim();
                    var stepsValue = match.Groups[3].Value.Trim();
                    
                    var conditionsText = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                    
                    // Gérer les variables dans MaxSteps
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
                else if (pattern.Contains("within") && pattern.Contains("from"))
                {
                    // Pattern 2 : "find label within steps steps from name"
                    parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                    var stepsValue = match.Groups[2].Value.Trim();
                    parsedQuery.FromNode = match.Groups[3].Value.Trim();
                    
                    var conditionsText = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                    
                    // Gérer les variables dans MaxSteps
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
                else if (pattern.Contains("where") && pattern.Contains("within") && pattern.Contains("from"))
                {
                    // Pattern 8 : recherche avec conditions
                    parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                    parsedQuery.MaxSteps = int.Parse(match.Groups[2].Value);
                    parsedQuery.FromNode = match.Groups[3].Value.Trim();
                    
                    var conditionsText = match.Groups[4].Value.Trim();
                    ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                }
                else if (pattern.Contains("connected") && !pattern.Contains("where"))
                {
                    // Pattern 9 : recherche de nœuds connectés via un type d'arête (sans conditions)
                    parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                    parsedQuery.FromNode = match.Groups[2].Value.Trim();
                    parsedQuery.EdgeType = match.Groups[3].Value.Trim();
                }
                else if (pattern.Contains("where") && pattern.Contains("within") && pattern.Contains("from") && !pattern.Contains("connected"))
                {
                    // Pattern 10 : recherche avec conditions (format simple)
                    parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                    parsedQuery.MaxSteps = int.Parse(match.Groups[2].Value);
                    parsedQuery.FromNode = match.Groups[3].Value.Trim();
                    
                    var conditionsText = match.Groups[4].Value.Trim();
                    ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                }
                else if (pattern.Contains("over"))
                {
                    // Pattern 3 : "find label from name over steps steps"
                    parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                    parsedQuery.FromNode = match.Groups[2].Value.Trim();
                    var stepsValue = match.Groups[3].Value.Trim();
                    
                    var conditionsText = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                    
                    // Gérer les variables dans MaxSteps
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
                    // Pattern 4 : "find label within steps of name"
                    parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                    var stepsValue = match.Groups[2].Value.Trim();
                    parsedQuery.FromNode = match.Groups[3].Value.Trim();
                    
                    var conditionsText = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                    
                    // Gérer les variables dans MaxSteps
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
                
                return;
            }
        }
        
        throw new ArgumentException($"Format de recherche par étapes invalide : {query}");
    }

    private void ParseUpdateNode(string query, ParsedQuery parsedQuery)
    {
        // Patterns multiples pour supporter différents formats de mise à jour
        var patterns = new[]
        {
            // Pattern 1 : "update [label] set [properties] where [conditions]"
            @"update\s+(\w+)\s+set\s+(.+?)\s+where\s+(.+)",
            // Pattern 2 : "update [label] with [properties] where [conditions]"
            @"update\s+(\w+)\s+with\s+(.+?)\s+where\s+(.+)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                parsedQuery.NodeLabel = match.Groups[1].Value;
                var propertiesText = match.Groups[2].Value.Trim();
                var conditionsText = match.Groups[3].Value.Trim();
                
                ParseDynamicProperties(propertiesText, parsedQuery.Properties);
                ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                return;
            }
        }
        
        throw new ArgumentException($"Format de mise à jour invalide : {query}");
    }

    private void ParseUpdateEdge(string query, ParsedQuery parsedQuery)
    {
        // Patterns multiples pour supporter différents formats de mise à jour d'arêtes
        var patterns = new[]
        {
            // Pattern 1 : "update edge from [label] ""name"" to [label] ""name"" set [properties] where [conditions]"
            @"update\s+edge\s+from\s+(\w+)\s+""([^""]+)""\s+to\s+(\w+)\s+""([^""]+)""\s+set\s+(.+?)(?:\s+where\s+(.+))?",
            // Pattern 2 : "update edge from [name] to [name] set [properties] where [conditions]"
            @"update\s+edge\s+from\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+set\s+(.+?)(?:\s+where\s+(.+))?",
            // Pattern 3 : "update edge between [from] and [to] set [properties] where [conditions]"
            @"update\s+edge\s+between\s+([^\s]+(?:\s+[^\s]+)*)\s+and\s+([^\s]+(?:\s+[^\s]+)*)\s+set\s+(.+?)(?:\s+where\s+(.+))?",
            // Pattern 4 : "update edge [from] to [to] set [properties] where [conditions]"
            @"update\s+edge\s+([^\s]+(?:\s+[^\s]+)*)\s+to\s+([^\s]+(?:\s+[^\s]+)*)\s+set\s+(.+?)(?:\s+where\s+(.+))?"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (pattern.Contains("""([^""]+)"""))
                {
                    // Pattern 1 : avec guillemets et labels
                    parsedQuery.FromNode = $"{match.Groups[1].Value} \"{match.Groups[2].Value}\"";
                    parsedQuery.ToNode = $"{match.Groups[3].Value} \"{match.Groups[4].Value}\"";
                    var propertiesText = match.Groups[5].Value.Trim();
                    var conditionsText = match.Groups.Count > 6 ? match.Groups[6].Value.Trim() : "";
                    
                    ParseDynamicProperties(propertiesText, parsedQuery.Properties);
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                else
                {
                    // Patterns 2-4 : sans guillemets
                    parsedQuery.FromNode = match.Groups[1].Value.Trim();
                    parsedQuery.ToNode = match.Groups[2].Value.Trim();
                    var propertiesText = match.Groups[3].Value.Trim();
                    var conditionsText = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";
                    
                    ParseDynamicProperties(propertiesText, parsedQuery.Properties);
                    if (!string.IsNullOrEmpty(conditionsText))
                    {
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                }
                return;
            }
        }
        
        throw new ArgumentException($"Format de mise à jour d'arête invalide : {query}");
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
        // Patterns multiples pour le comptage
        var patterns = new[]
        {
            // Pattern 1 : "count [label] where [conditions]"
            @"count\s+(\w+)\s+where\s+(.+)",
            // Pattern 2 : "count [all] [label]" (sans conditions)
            @"count\s+(all\s+)?(\w+)",
            // Pattern 3 : "count edges where [conditions]"
            @"count\s+edges?\s+where\s+(.+)",
            // Pattern 4 : "count edges from [label] where [conditions]"
            @"count\s+edges?\s+from\s+(\w+)\s+where\s+(.+)"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (pattern.Contains("edges"))
                {
                    // Patterns 3-4 : comptage d'arêtes
                    if (pattern.Contains("from"))
                    {
                        // Pattern 4 : count edges from label where conditions
                        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                        var conditionsText = match.Groups[2].Value.Trim();
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                    else
                    {
                        // Pattern 3 : count edges where conditions
                        var conditionsText = match.Groups[1].Value.Trim();
                        ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    }
                    parsedQuery.Properties["count_edges"] = true;
                }
                else if (pattern.Contains("where"))
                {
                    // Pattern 1 : count label where conditions
                    parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                    var conditionsText = match.Groups[2].Value.Trim();
                    ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                }
                else
                {
                    // Pattern 2 : count [all] label
                    parsedQuery.NodeLabel = NormalizeLabel(match.Groups[2].Value);
                }
                return;
            }
        }
        
        throw new ArgumentException($"Format de comptage invalide : {query}");
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
            @"(sum|avg|min|max)\s+(\w+)\s+([^\s]+)(?:\s+where\s+(.+))?",
            // Pattern 5 : "sum/avg/min/max [property] of edges where [conditions]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+where\s+(.+)",
            // Pattern 6 : "sum/avg/min/max [property] of edges"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?",
            // Pattern 7 : "sum/avg/min/max [property] of edges (format alternatif)"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+where\s+(.+)",
            // Pattern 8 : "sum/avg/min/max [property] of edges (format simple)"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?",
            // Nouveaux patterns pour les agrégations avancées
            // Pattern 9 : "sum/avg/min/max [property] of edges with type [edge_type]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+with\s+type\s+(\w+)",
            // Pattern 10 : "sum/avg/min/max [property] of edges with type [edge_type] where [conditions]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+with\s+type\s+(\w+)\s+where\s+(.+)",
            // Pattern 11 : "sum/avg/min/max [property] of edges from [label] to [label]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+from\s+(\w+)\s+to\s+(\w+)",
            // Pattern 12 : "sum/avg/min/max [property] of edges from [label] to [label] where [conditions]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+from\s+(\w+)\s+to\s+(\w+)\s+where\s+(.+)",
            // Pattern 13 : "sum/avg/min/max [property] of edges between [label] and [label]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+between\s+(\w+)\s+and\s+(\w+)",
            // Pattern 14 : "sum/avg/min/max [property] of edges between [label] and [label] where [conditions]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+between\s+(\w+)\s+and\s+(\w+)\s+where\s+(.+)",
            // Pattern 15 : "sum/avg/min/max [property] of edges via [edge_type]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+via\s+(\w+)",
            // Pattern 16 : "sum/avg/min/max [property] of edges via [edge_type] where [conditions]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+via\s+(\w+)\s+where\s+(.+)",
            // Pattern 17 : "sum/avg/min/max [property] of edges avoiding [edge_type]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+avoiding\s+(\w+)",
            // Pattern 18 : "sum/avg/min/max [property] of edges avoiding [edge_type] where [conditions]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+avoiding\s+(\w+)\s+where\s+(.+)",
            // Pattern 19 : "sum/avg/min/max [property] of edges with max steps [number]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+with\s+max\s+steps\s+(\d+)",
            // Pattern 20 : "sum/avg/min/max [property] of edges with max steps [number] where [conditions]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+with\s+max\s+steps\s+(\d+)\s+where\s+(.+)",
            // Pattern 21 : "sum/avg/min/max [property] of edges connected to [label]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+connected\s+to\s+(\w+)",
            // Pattern 22 : "sum/avg/min/max [property] of edges connected to [label] where [conditions]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+connected\s+to\s+(\w+)\s+where\s+(.+)",
            // Pattern 23 : "sum/avg/min/max [property] of edges connected to [label] via [edge_type]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+connected\s+to\s+(\w+)\s+via\s+(\w+)",
            // Pattern 24 : "sum/avg/min/max [property] of edges connected to [label] via [edge_type] where [conditions]"
            @"(sum|avg|min|max)\s+([^\s]+)\s+of\s+edges?\s+connected\s+to\s+(\w+)\s+via\s+(\w+)\s+where\s+(.+)"
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
                else if (match.Groups.Count >= 5 && pattern.Contains("with type") && pattern.Contains("where"))
                {
                    // Pattern 10 : sum/avg/min/max property of edges with type [edge_type] where conditions
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.EdgeType = match.Groups[3].Value.Trim();
                    var conditionsText = match.Groups[4].Value.Trim();
                    ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    parsedQuery.Properties["aggregate_edges"] = true;
                }
                else if (match.Groups.Count >= 4 && pattern.Contains("with type"))
                {
                    // Pattern 9 : sum/avg/min/max property of edges with type [edge_type]
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.EdgeType = match.Groups[3].Value.Trim();
                    parsedQuery.Properties["aggregate_edges"] = true;
                }
                else if (match.Groups.Count >= 6 && pattern.Contains("from") && pattern.Contains("to") && pattern.Contains("where"))
                {
                    // Pattern 12 : sum/avg/min/max property of edges from [label] to [label] where conditions
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.Properties["from_label"] = match.Groups[3].Value.Trim();
                    parsedQuery.Properties["to_label"] = match.Groups[4].Value.Trim();
                    var conditionsText = match.Groups[5].Value.Trim();
                    ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    parsedQuery.Properties["aggregate_edges"] = true;
                }
                else if (match.Groups.Count >= 5 && pattern.Contains("from") && pattern.Contains("to"))
                {
                    // Pattern 11 : sum/avg/min/max property of edges from [label] to [label]
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.Properties["from_label"] = match.Groups[3].Value.Trim();
                    parsedQuery.Properties["to_label"] = match.Groups[4].Value.Trim();
                    parsedQuery.Properties["aggregate_edges"] = true;
                }
                else if (match.Groups.Count >= 6 && pattern.Contains("between") && pattern.Contains("where"))
                {
                    // Pattern 14 : sum/avg/min/max property of edges between [label] and [label] where conditions
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.Properties["from_label"] = match.Groups[3].Value.Trim();
                    parsedQuery.Properties["to_label"] = match.Groups[4].Value.Trim();
                    var conditionsText = match.Groups[5].Value.Trim();
                    ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    parsedQuery.Properties["aggregate_edges"] = true;
                }
                else if (match.Groups.Count >= 5 && pattern.Contains("between"))
                {
                    // Pattern 13 : sum/avg/min/max property of edges between [label] and [label]
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.Properties["from_label"] = match.Groups[3].Value.Trim();
                    parsedQuery.Properties["to_label"] = match.Groups[4].Value.Trim();
                    parsedQuery.Properties["aggregate_edges"] = true;
                }
                else if (match.Groups.Count >= 5 && pattern.Contains("via") && pattern.Contains("where"))
                {
                    // Pattern 16 : sum/avg/min/max property of edges via [edge_type] where conditions
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.EdgeType = match.Groups[3].Value.Trim();
                    var conditionsText = match.Groups[4].Value.Trim();
                    ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    parsedQuery.Properties["aggregate_edges"] = true;
                }
                else if (match.Groups.Count >= 4 && pattern.Contains("via"))
                {
                    // Pattern 15 : sum/avg/min/max property of edges via [edge_type]
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.EdgeType = match.Groups[3].Value.Trim();
                    parsedQuery.Properties["aggregate_edges"] = true;
                }
                else if (match.Groups.Count >= 5 && pattern.Contains("avoiding") && pattern.Contains("where"))
                {
                    // Pattern 18 : sum/avg/min/max property of edges avoiding [edge_type] where conditions
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.Properties["avoid_edge_type"] = match.Groups[3].Value.Trim();
                    var conditionsText = match.Groups[4].Value.Trim();
                    ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    parsedQuery.Properties["aggregate_edges"] = true;
                }
                else if (match.Groups.Count >= 4 && pattern.Contains("avoiding"))
                {
                    // Pattern 17 : sum/avg/min/max property of edges avoiding [edge_type]
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.Properties["avoid_edge_type"] = match.Groups[3].Value.Trim();
                    parsedQuery.Properties["aggregate_edges"] = true;
                }
                else if (match.Groups.Count >= 5 && pattern.Contains("max steps") && pattern.Contains("where"))
                {
                    // Pattern 20 : sum/avg/min/max property of edges with max steps [number] where conditions
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.MaxSteps = int.Parse(match.Groups[3].Value);
                    var conditionsText = match.Groups[4].Value.Trim();
                    ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    parsedQuery.Properties["aggregate_edges"] = true;
                }
                else if (match.Groups.Count >= 4 && pattern.Contains("max steps"))
                {
                    // Pattern 19 : sum/avg/min/max property of edges with max steps [number]
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.MaxSteps = int.Parse(match.Groups[3].Value);
                    parsedQuery.Properties["aggregate_edges"] = true;
                }
                else if (match.Groups.Count >= 6 && pattern.Contains("connected to") && pattern.Contains("via") && pattern.Contains("where"))
                {
                    // Pattern 24 : sum/avg/min/max property of edges connected to [label] via [edge_type] where conditions
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.Properties["connected_to_label"] = match.Groups[3].Value.Trim();
                    parsedQuery.EdgeType = match.Groups[4].Value.Trim();
                    var conditionsText = match.Groups[5].Value.Trim();
                    ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    parsedQuery.Properties["aggregate_edges"] = true;
                }
                else if (match.Groups.Count >= 5 && pattern.Contains("connected to") && pattern.Contains("via"))
                {
                    // Pattern 23 : sum/avg/min/max property of edges connected to [label] via [edge_type]
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.Properties["connected_to_label"] = match.Groups[3].Value.Trim();
                    parsedQuery.EdgeType = match.Groups[4].Value.Trim();
                    parsedQuery.Properties["aggregate_edges"] = true;
                }
                else if (match.Groups.Count >= 5 && pattern.Contains("connected to") && pattern.Contains("where"))
                {
                    // Pattern 22 : sum/avg/min/max property of edges connected to [label] where conditions
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.Properties["connected_to_label"] = match.Groups[3].Value.Trim();
                    var conditionsText = match.Groups[4].Value.Trim();
                    ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    parsedQuery.Properties["aggregate_edges"] = true;
                }
                else if (match.Groups.Count >= 4 && pattern.Contains("connected to"))
                {
                    // Pattern 21 : sum/avg/min/max property of edges connected to [label]
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.Properties["connected_to_label"] = match.Groups[3].Value.Trim();
                    parsedQuery.Properties["aggregate_edges"] = true;
                }
                else if (match.Groups.Count >= 4 && pattern.Contains("where"))
                {
                    // Pattern 5/7 : sum/avg/min/max property of edges where conditions
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    var conditionsText = match.Groups[3].Value.Trim();
                    ParseConditionsWithSubQueries(conditionsText, parsedQuery.Conditions, parsedQuery);
                    parsedQuery.Properties["aggregate_edges"] = true;
                }
                else if (match.Groups.Count >= 3)
                {
                    // Pattern 6/8 : sum/avg/min/max property of edges (sans conditions)
                    functionName = match.Groups[1].Value.ToLowerInvariant();
                    parsedQuery.AggregateProperty = match.Groups[2].Value.Trim();
                    parsedQuery.Properties["aggregate_edges"] = true;
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
        
        // Patterns multiples pour supporter différents formats
        var patterns = new[]
        {
            // Pattern 1 : "define variable @name as value"
            @"define\s+variable\s+([@$\w]+)\s+as\s+(.+)",
            // Pattern 2 : "define @name as value"
            @"define\s+([@$\w]+)\s+as\s+(.+)",
            // Pattern 3 : "let @name = value"
            @"let\s+([@$\w]+)\s*=\s*(.+)",
            // Pattern 4 : "set @name = value"
            @"set\s+([@$\w]+)\s*=\s*(.+)",
            // Pattern 5 : "define variable @name as aggregation with property"
            @"define\s+variable\s+([@$\w]+)\s+as\s+(avg|sum|min|max|count)\s+(\w+)\s+property\s+(\w+)\s+where\s+(.+)",
            // Pattern 6 : "define @name as aggregation with property"
            @"define\s+([@$\w]+)\s+as\s+(avg|sum|min|max|count)\s+(\w+)\s+property\s+(\w+)\s+where\s+(.+)",
            // Pattern 7 : "define variable @name as aggregation" (format générique)
            @"define\s+variable\s+([@$\w]+)\s+as\s+(avg|sum|min|max|count)\s+(.+)",
            // Pattern 8 : "define @name as aggregation" (format générique)
            @"define\s+([@$\w]+)\s+as\s+(avg|sum|min|max|count)\s+(.+)",
            // Pattern 9 : "define variable @name as aggregation" (format avec espaces)
            @"define\s+variable\s+([@$\w]+)\s+as\s+(avg|sum|min|max|count)\s+(\w+)\s+(\w+)\s+(\w+)\s+where\s+(.+)",
            // Pattern 10 : "define @name as aggregation" (format avec espaces)
            @"define\s+([@$\w]+)\s+as\s+(avg|sum|min|max|count)\s+(\w+)\s+(\w+)\s+(\w+)\s+where\s+(.+)",
            // Pattern 11 : "define variable @name as aggregation" (format générique flexible)
            @"define\s+variable\s+([@$\w]+)\s+as\s+(avg|sum|min|max|count)\s+(.+)",
            // Pattern 12 : "define @name as aggregation" (format générique flexible)
            @"define\s+([@$\w]+)\s+as\s+(avg|sum|min|max|count)\s+(.+)",
            // Pattern 13 : "define variable @name as aggregation" (format très flexible)
            @"define\s+variable\s+([@$\w]+)\s+as\s+(avg|sum|min|max|count)\s+(.+)",
            // Pattern 14 : "define @name as aggregation" (format très flexible)
            @"define\s+([@$\w]+)\s+as\s+(avg|sum|min|max|count)\s+(.+)",
            // Pattern 15 : "define variable @name as anything" (format ultra flexible)
            @"define\s+variable\s+([@$\w]+)\s+as\s+(.+)",
            // Pattern 16 : "define @name as anything" (format ultra flexible)
            @"define\s+([@$\w]+)\s+as\s+(.+)"
        };
        
        foreach (var pattern in patterns)
        {
            var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            if (match.Success)
            {
                var variableName = match.Groups[1].Value;
                var value = match.Groups[2].Value.Trim();
                
                // Gérer les patterns avec agrégations (patterns 5, 6, 7, 8, 9, 10, 11, 12)
                if (pattern.Contains("(avg|sum|min|max|count)"))
                {
                    var aggregationFunction = match.Groups[2].Value;
                    
                    if (pattern.Contains("property") && match.Groups.Count > 5)
                    {
                        // Patterns 5 et 6 : avec "property" spécifié
                        var label = match.Groups[3].Value;
                        var property = match.Groups[4].Value;
                        var conditions = match.Groups[5].Value;
                        value = $"{aggregationFunction} {label} property {property} where {conditions}";
                    }
                    else if (pattern.Contains("(\\w+)\\s+(\\w+)\\s+(\\w+)\\s+where") && match.Groups.Count > 6)
                    {
                        // Patterns 9 et 10 : format avec espaces séparés
                        var label = match.Groups[3].Value;
                        var property = match.Groups[4].Value;
                        var conditions = match.Groups[6].Value;
                        value = $"{aggregationFunction} {label} property {property} where {conditions}";
                    }
                    else
                    {
                        // Patterns 7, 8, 11, 12 : format générique
                        var aggregationQuery = match.Groups[3].Value.Trim();
                        value = $"{aggregationFunction} {aggregationQuery}";
                    }
                }
                
                if (!variableName.StartsWith("$") && !variableName.StartsWith("@"))
                    variableName = "@" + variableName;
                parsedQuery.VariableName = variableName;
                parsedQuery.VariableValue = value;
                return;
            }
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
                // Pattern 5 : "batch create [count] [label] with [properties]" - NOUVEAU
                @"batch\s+create\s+(\d+)\s+(\w+)\s+with\s+(.+)",
                // Pattern 5a : "batch create [label] with [properties]"
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
                        // Pattern 1 : create batch of 3 companies with name [""Startup1"", ""Startup2"", ""Startup3""]
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
                        // Pattern 5 : batch create avec count ou sans count
                        if (pattern.Contains("batch\\s+create\\s+(\\d+)"))
                        {
                            // Pattern avec count : "batch create 3 persons with properties"
                            ParseBatchCreateWithCountPattern(match, parsedQuery);
                        }
                        else
                        {
                            // Pattern sans count : "batch create persons with properties"
                            ParseBatchCreatePattern(match, parsedQuery);
                        }
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

    private void ParseBatchCreateWithCountPattern(Match match, ParsedQuery parsedQuery)
    {
        var count = int.Parse(match.Groups[1].Value);
        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[2].Value);
        var propertiesText = match.Groups[3].Value.Trim();
        parsedQuery.BatchType = BatchOperationType.Create;
        parsedQuery.BatchSize = count;
        
        // Parser les propriétés
        ParseBatchProperties(propertiesText, parsedQuery.Properties);
        
        // Créer des opérations batch individuelles
        parsedQuery.BatchOperations = new List<ParsedQuery>();
        
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

        // Debug temporaire
        Console.WriteLine($"DEBUG: Parsing conditions: '{conditionsText}'");

        // Nettoyer les conditions
        conditionsText = conditionsText.Trim();
        
        // Vérifier d'abord s'il y a des sous-requêtes imbriquées dans les conditions
        var nestedSubQueryPattern = @"(\w+)\s+([><=!]+)\s+\((.+?)\)";
        var nestedMatch = Regex.Match(conditionsText, nestedSubQueryPattern);
        if (nestedMatch.Success)
        {
            var propertyName = nestedMatch.Groups[1].Value.Trim();
            var comparisonOperator = nestedMatch.Groups[2].Value.Trim();
            var nestedSubQuery = nestedMatch.Groups[3].Value.Trim();
            
            Console.WriteLine($"DEBUG: Found nested subquery: {propertyName} {comparisonOperator} ({nestedSubQuery})");
            
            // Créer une sous-requête pour l'agrégation
            var aggregateSubQuery = ParseSubQueryFromString(nestedSubQuery);
            if (aggregateSubQuery != null)
            {
                // Déterminer si c'est une agrégation ou une sous-requête SELECT
                if (nestedSubQuery.Contains("select", StringComparison.OrdinalIgnoreCase))
                {
                    // Traiter comme une sous-requête SELECT avec agrégation
                    var selectMatch = Regex.Match(nestedSubQuery, @"select\s+(sum|avg|min|max|count)\s+(\w+)\s+from\s+(\w+)(?:\s+where\s+(.+))?", RegexOptions.IgnoreCase);
                    if (selectMatch.Success)
                    {
                        aggregateSubQuery.Type = QueryType.Aggregate;
                        aggregateSubQuery.AggregateFunction = selectMatch.Groups[1].Value.ToLowerInvariant() switch
                        {
                            "sum" => AggregateFunction.Sum,
                            "avg" => AggregateFunction.Avg,
                            "min" => AggregateFunction.Min,
                            "max" => AggregateFunction.Max,
                            "count" => AggregateFunction.Count,
                            _ => AggregateFunction.Avg
                        };
                        aggregateSubQuery.AggregateProperty = selectMatch.Groups[2].Value.Trim();
                        aggregateSubQuery.NodeLabel = NormalizeLabel(selectMatch.Groups[3].Value.Trim());
                        
                        if (selectMatch.Groups.Count > 4 && !string.IsNullOrEmpty(selectMatch.Groups[4].Value))
                        {
                            ParseSimpleConditions(selectMatch.Groups[4].Value.Trim(), aggregateSubQuery.Conditions);
                        }
                        
                        Console.WriteLine($"DEBUG: Parsed as SELECT AGGREGATE - Function: {aggregateSubQuery.AggregateFunction}, Property: {aggregateSubQuery.AggregateProperty}, Label: {aggregateSubQuery.NodeLabel}");
                    }
                    else
                    {
                        // Traiter comme une sous-requête SELECT simple
                        aggregateSubQuery.Type = QueryType.FindNodes;
                        aggregateSubQuery.SubQueryProperty = propertyName;
                        aggregateSubQuery.SubQueryOperator = SubQueryOperator.In;
                        
                        var simpleSelectMatch = Regex.Match(nestedSubQuery, @"select\s+(\w+)\s+from\s+(\w+)(?:\s+where\s+(.+))?", RegexOptions.IgnoreCase);
                        if (simpleSelectMatch.Success)
                        {
                            aggregateSubQuery.SubQueryProperty = simpleSelectMatch.Groups[1].Value.Trim();
                            aggregateSubQuery.NodeLabel = NormalizeLabel(simpleSelectMatch.Groups[2].Value.Trim());
                            
                            if (simpleSelectMatch.Groups.Count > 3 && !string.IsNullOrEmpty(simpleSelectMatch.Groups[3].Value))
                            {
                                ParseSimpleConditions(simpleSelectMatch.Groups[3].Value.Trim(), aggregateSubQuery.Conditions);
                            }
                        }
                        
                        Console.WriteLine($"DEBUG: Parsed as SELECT subquery - Property: {aggregateSubQuery.SubQueryProperty}, Label: {aggregateSubQuery.NodeLabel}");
                    }
                }
                else if (nestedSubQuery.Contains("avg") || nestedSubQuery.Contains("sum") || nestedSubQuery.Contains("min") || 
                         nestedSubQuery.Contains("max") || nestedSubQuery.Contains("count"))
                {
                    // Traiter comme une agrégation directe
                    aggregateSubQuery.Type = QueryType.Aggregate;
                    aggregateSubQuery.AggregateProperty = propertyName;
                    aggregateSubQuery.AggregateFunction = AggregateFunction.Avg; // Par défaut
                    
                    // Déterminer la fonction d'agrégation basée sur le texte
                    if (nestedSubQuery.Contains("avg"))
                        aggregateSubQuery.AggregateFunction = AggregateFunction.Avg;
                    else if (nestedSubQuery.Contains("sum"))
                        aggregateSubQuery.AggregateFunction = AggregateFunction.Sum;
                    else if (nestedSubQuery.Contains("min"))
                        aggregateSubQuery.AggregateFunction = AggregateFunction.Min;
                    else if (nestedSubQuery.Contains("max"))
                        aggregateSubQuery.AggregateFunction = AggregateFunction.Max;
                    else if (nestedSubQuery.Contains("count"))
                        aggregateSubQuery.AggregateFunction = AggregateFunction.Count;
                    
                    // Parser le label et les conditions
                    var aggregateMatch = Regex.Match(nestedSubQuery, @"(?:avg|sum|min|max|count)\s+(\w+)\s+from\s+(\w+)(?:\s+where\s+(.+))?", RegexOptions.IgnoreCase);
                    if (aggregateMatch.Success)
                    {
                        aggregateSubQuery.AggregateProperty = aggregateMatch.Groups[1].Value.Trim();
                        aggregateSubQuery.NodeLabel = NormalizeLabel(aggregateMatch.Groups[2].Value.Trim());
                        
                        if (aggregateMatch.Groups.Count > 3 && !string.IsNullOrEmpty(aggregateMatch.Groups[3].Value))
                        {
                            ParseSimpleConditions(aggregateMatch.Groups[3].Value.Trim(), aggregateSubQuery.Conditions);
                        }
                    }
                    
                    Console.WriteLine($"DEBUG: Parsed as AGGREGATE - Function: {aggregateSubQuery.AggregateFunction}, Property: {aggregateSubQuery.AggregateProperty}, Label: {aggregateSubQuery.NodeLabel}");
                }
                else
                {
                    // Si le parsing échoue, essayer de traiter comme une sous-requête simple
                    Console.WriteLine($"DEBUG: Failed to parse nested subquery, treating as simple subquery");
                    aggregateSubQuery.Type = QueryType.FindNodes;
                    aggregateSubQuery.NodeLabel = "values";
                    aggregateSubQuery.SubQueryProperty = propertyName;
                    aggregateSubQuery.SubQueryOperator = SubQueryOperator.In;
                    aggregateSubQuery.Conditions = new Dictionary<string, object> { { "values", new List<object> { nestedSubQuery } } };
                    
                    Console.WriteLine($"DEBUG: Added simple subquery condition: {propertyName}_{comparisonOperator}_aggregate");
                }
                
                // Stocker la sous-requête avec une clé qui sera reconnue par le moteur
                conditions[$"{propertyName}_{comparisonOperator}_aggregate"] = aggregateSubQuery;
                Console.WriteLine($"DEBUG: Added nested aggregate condition: {propertyName}_{comparisonOperator}_aggregate");
                return;
            }
        }
        
        // Patterns pour les conditions complexes avec sous-requêtes (ordre spécifique pour éviter les conflits)
        var complexPatterns = new[]
        {
                            // Patterns pour les sous-requêtes imbriquées avec parenthèses (plus spécifiques en premier)
                @"(\w+)\s+in\s+\((.+?)\s+where\s+(.+?)\s+([><=!]+)\s+\((.+?)\)\)",
                @"(\w+)\s+exists\s+in\s+\((.+?)\s+where\s+(.+?)\s+([><=!]+)\s+\((.+?)\)\)",
                @"(\w+)\s+any\s+in\s+\((.+?)\s+where\s+(.+?)\s+([><=!]+)\s+\((.+?)\)\)",
                @"(\w+)\s+all\s+in\s+\((.+?)\s+where\s+(.+?)\s+([><=!]+)\s+\((.+?)\)\)",
            
            // Patterns pour les sous-requêtes EXISTS/NOT EXISTS (NOT EXISTS en premier pour éviter les conflits)
            @"(\w+)\s+not\s+exists\s+in\s+\((.+?)\)",
            @"(\w+)\s+exists\s+in\s+\((.+?)\)",
            @"(\w+)\s+not\s+exists\s+where\s+(.+)",
            @"(\w+)\s+exists\s+where\s+(.+)",
            
            // Patterns pour les sous-requêtes IN/NOT IN avec SELECT
            @"(\w+)\s+in\s+select\s+(.+?)\s+from\s+(\w+)(?:\s+where\s+(.+?))?",
            @"(\w+)\s+not\s+in\s+select\s+(.+?)\s+from\s+(\w+)(?:\s+where\s+(.+?))?",
            
            // Patterns pour les agrégations dans les conditions
            @"(\w+)\s+(eq|gt|lt|gte|lte)\s+(sum|avg|min|max|count)\s+\((.+?)\)\s+from\s+(\w+)(?:\s+where\s+(.+?))?",
            @"(\w+)\s+(eq|gt|lt|gte|lte)\s+(sum|avg|min|max|count)\s+(\w+)\s+from\s+(\w+)(?:\s+where\s+(.+?))?",
            
            // Patterns pour les sous-requêtes avec ANY/ALL
            @"(\w+)\s+any\s+in\s+\((.+?)\)",
            @"(\w+)\s+all\s+in\s+\((.+?)\)",
            @"(\w+)\s+any\s+select\s+(.+?)\s+from\s+(\w+)(?:\s+where\s+(.+?))?",
            @"(\w+)\s+all\s+select\s+(.+?)\s+from\s+(\w+)(?:\s+where\s+(.+?))?",
            
            // Patterns génériques IN/NOT IN (en dernier pour éviter les conflits)
            @"(\w+)\s+in\s+\((.+?)\)",
            @"(\w+)\s+not\s+in\s+\((.+?)\)",
            
            // Patterns existants pour la compatibilité
            @"connected\s+to\s+via\s+(\w+)",
            @"connected\s+to\s+(\w+)\s+via\s+(\w+)",
            @"connected\s+to\s+(\w+)\s+""([^""]+)""\s+via\s+(\w+)",
            @"connected\s+to\s+via\s+(\w+)\s+where\s+(.+)",
            @"connected\s+to\s+(\w+)\s+via\s+(\w+)\s+where\s+(.+)",
            @"connected\s+to\s+(\w+)\s+""([^""]+)""\s+via\s+(\w+)\s+where\s+(.+)",
            @"has\s+edge\s+(\w+)\s+to\s+(\w+)",
            @"has\s+edge\s+(\w+)\s+to\s+(\w+)\s+""([^""]+)""",
            @"has\s+edge\s+(\w+)\s+to\s+(\w+)\s+where\s+(.+)",
            @"has\s+edge\s+(\w+)\s+to\s+(\w+)\s+""([^""]+)""\s+where\s+(.+)",
            @"connected\s+via\s+(\w+)",
            @"connected\s+via\s+(\w+)\s+to\s+(\w+)",
            @"connected\s+via\s+(\w+)\s+to\s+(\w+)\s+""([^""]+)""",
            @"connected\s+via\s+(\w+)\s+where\s+(.+)",
            @"connected\s+via\s+(\w+)\s+to\s+(\w+)\s+where\s+(.+)",
            @"connected\s+via\s+(\w+)\s+to\s+(\w+)\s+""([^""]+)""\s+where\s+(.+)"
        };

        // Essayer d'abord les patterns complexes
        foreach (var pattern in complexPatterns)
        {
            var match = Regex.Match(conditionsText, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                Console.WriteLine($"DEBUG: Pattern matched: '{pattern}'");
                Console.WriteLine($"DEBUG: Groups: {string.Join(", ", match.Groups.Cast<Group>().Select(g => g.Value))}");
                
                // Patterns pour EXISTS/NOT EXISTS
                if (pattern == @"(\w+)\s+exists\s+in\s+\((.+?)\)")
                {
                    var propertyName = match.Groups[1].Value.Trim();
                    var subQueryText = match.Groups[2].Value.Trim();
                    
                    Console.WriteLine($"DEBUG: Processing EXISTS IN - Property: {propertyName}, SubQuery: {subQueryText}");
                    
                    var subQuery = ParseSubQueryFromString(subQueryText);
                    if (subQuery != null)
                    {
                        subQuery.SubQueryOperator = SubQueryOperator.Exists;
                        subQuery.SubQueryProperty = null; // Pas de propriété spécifique pour EXISTS
                        subQuery.ParentQuery = parentQuery;
                        subQuery.SubQueryDepth = parentQuery?.SubQueryDepth + 1 ?? 0;
                        
                        conditions[$"{propertyName}_exists"] = subQuery;
                        Console.WriteLine($"DEBUG: Added EXISTS condition for {propertyName}");
                        return;
                    }
                }
                else if (pattern == @"(\w+)\s+not\s+exists\s+in\s+\((.+?)\)")
                {
                    var propertyName = match.Groups[1].Value.Trim();
                    var subQueryText = match.Groups[2].Value.Trim();
                    
                    Console.WriteLine($"DEBUG: Processing NOT EXISTS IN - Property: {propertyName}, SubQuery: {subQueryText}");
                    
                    var subQuery = ParseSubQueryFromString(subQueryText);
                    if (subQuery != null)
                    {
                        subQuery.SubQueryOperator = SubQueryOperator.NotExists;
                        subQuery.SubQueryProperty = null; // Pas de propriété spécifique pour NOT EXISTS
                        subQuery.ParentQuery = parentQuery;
                        subQuery.SubQueryDepth = parentQuery?.SubQueryDepth + 1 ?? 0;
                        
                        conditions[$"{propertyName}_not_exists"] = subQuery;
                        Console.WriteLine($"DEBUG: Added NOT EXISTS condition for {propertyName}");
                        return;
                    }
                }
                else if (pattern == @"(\w+)\s+exists\s+where\s+(.+)")
                {
                    var propertyName = match.Groups[1].Value.Trim();
                    var subConditions = match.Groups[2].Value.Trim();
                    
                    Console.WriteLine($"DEBUG: Processing EXISTS WHERE - Property: {propertyName}, Conditions: {subConditions}");
                    
                    var subQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        NodeLabel = NormalizeLabel(propertyName),
                        SubQueryOperator = SubQueryOperator.Exists,
                        SubQueryProperty = null, // Pas de propriété spécifique pour EXISTS
                        ParentQuery = parentQuery,
                        SubQueryDepth = parentQuery?.SubQueryDepth + 1 ?? 0
                    };
                    
                    ParseSimpleConditions(subConditions, subQuery.Conditions);
                    
                    conditions[$"{propertyName}_exists"] = subQuery;
                    Console.WriteLine($"DEBUG: Added EXISTS WHERE condition for {propertyName}");
                    return;
                }
                else if (pattern == @"(\w+)\s+not\s+exists\s+where\s+(.+)")
                {
                    var propertyName = match.Groups[1].Value.Trim();
                    var subConditions = match.Groups[2].Value.Trim();
                    
                    Console.WriteLine($"DEBUG: Processing NOT EXISTS WHERE - Property: {propertyName}, Conditions: {subConditions}");
                    
                    var subQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        NodeLabel = NormalizeLabel(propertyName),
                        SubQueryOperator = SubQueryOperator.NotExists,
                        SubQueryProperty = null, // Pas de propriété spécifique pour NOT EXISTS
                        ParentQuery = parentQuery,
                        SubQueryDepth = parentQuery?.SubQueryDepth + 1 ?? 0
                    };
                    
                    ParseSimpleConditions(subConditions, subQuery.Conditions);
                    
                    conditions[$"{propertyName}_not_exists"] = subQuery;
                    Console.WriteLine($"DEBUG: Added NOT EXISTS WHERE condition for {propertyName}");
                    return;
                }
                
                // Patterns pour IN/NOT IN avec SELECT
                else if (pattern.Contains("in\\s+select"))
                {
                    var propertyName = match.Groups[1].Value.Trim();
                    var selectProperty = match.Groups[2].Value.Trim();
                    var fromLabel = match.Groups[3].Value.Trim();
                    var whereConditions = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";
                    
                    Console.WriteLine($"DEBUG: Processing IN SELECT - Property: {propertyName}, Select: {selectProperty}, From: {fromLabel}, Where: {whereConditions}");
                    
                    var subQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        NodeLabel = NormalizeLabel(fromLabel),
                        SubQueryProperty = selectProperty,
                        SubQueryOperator = SubQueryOperator.In,
                        ParentQuery = parentQuery,
                        SubQueryDepth = parentQuery?.SubQueryDepth + 1 ?? 0
                    };
                    
                    if (!string.IsNullOrEmpty(whereConditions))
                    {
                        ParseSimpleConditions(whereConditions, subQuery.Conditions);
                    }
                    
                    conditions[$"{propertyName}_in"] = subQuery;
                    Console.WriteLine($"DEBUG: Added IN SELECT condition for {propertyName}");
                    return;
                }
                else if (pattern.Contains("not\\s+in\\s+select"))
                {
                    var propertyName = match.Groups[1].Value.Trim();
                    var selectProperty = match.Groups[2].Value.Trim();
                    var fromLabel = match.Groups[3].Value.Trim();
                    var whereConditions = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";
                    
                    Console.WriteLine($"DEBUG: Processing NOT IN SELECT - Property: {propertyName}, Select: {selectProperty}, From: {fromLabel}, Where: {whereConditions}");
                    
                    var subQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        NodeLabel = NormalizeLabel(fromLabel),
                        SubQueryProperty = selectProperty,
                        SubQueryOperator = SubQueryOperator.NotIn,
                        ParentQuery = parentQuery,
                        SubQueryDepth = parentQuery?.SubQueryDepth + 1 ?? 0
                    };
                    
                    if (!string.IsNullOrEmpty(whereConditions))
                    {
                        ParseSimpleConditions(whereConditions, subQuery.Conditions);
                    }
                    
                    conditions[$"{propertyName}_not_in"] = subQuery;
                    Console.WriteLine($"DEBUG: Added NOT IN SELECT condition for {propertyName}");
                    return;
                }
                
                // Patterns pour les agrégations dans les conditions
                else if (pattern.Contains("\\s+(eq|gt|lt|gte|lte)\\s+(sum|avg|min|max|count)\\s+"))
                {
                    var propertyName = match.Groups[1].Value.Trim();
                    var comparisonOperator = match.Groups[2].Value.Trim();
                    var aggregateFunction = match.Groups[3].Value.Trim();
                    var aggregateProperty = match.Groups[4].Value.Trim();
                    var fromLabel = match.Groups[5].Value.Trim();
                    var whereConditions = match.Groups.Count > 6 ? match.Groups[6].Value.Trim() : "";
                    
                    Console.WriteLine($"DEBUG: Processing AGGREGATE - Property: {propertyName}, Operator: {comparisonOperator}, Function: {aggregateFunction}, Property: {aggregateProperty}, From: {fromLabel}, Where: {whereConditions}");
                    
                    var subQuery = new ParsedQuery
                    {
                        Type = QueryType.Aggregate,
                        NodeLabel = NormalizeLabel(fromLabel),
                        AggregateProperty = aggregateProperty,
                        AggregateFunction = aggregateFunction switch
                        {
                            "sum" => AggregateFunction.Sum,
                            "avg" => AggregateFunction.Avg,
                            "min" => AggregateFunction.Min,
                            "max" => AggregateFunction.Max,
                            "count" => AggregateFunction.Count,
                            _ => AggregateFunction.Avg
                        },
                        ParentQuery = parentQuery,
                        SubQueryDepth = parentQuery?.SubQueryDepth + 1 ?? 0
                    };
                    
                    if (!string.IsNullOrEmpty(whereConditions))
                    {
                        ParseSimpleConditions(whereConditions, subQuery.Conditions);
                    }
                    
                    conditions[$"{propertyName}_{comparisonOperator}_aggregate"] = subQuery;
                    Console.WriteLine($"DEBUG: Added AGGREGATE condition for {propertyName}");
                    return;
                }
                
                // Patterns pour ANY/ALL
                else if (pattern == @"(\w+)\s+any\s+in\s+\((.+?)\)")
                {
                    var propertyName = match.Groups[1].Value.Trim();
                    var subQueryText = match.Groups[2].Value.Trim();
                    
                    Console.WriteLine($"DEBUG: Processing ANY IN - Property: {propertyName}, SubQuery: {subQueryText}");
                    
                    var subQuery = ParseSubQueryFromString(subQueryText);
                    if (subQuery != null)
                    {
                        subQuery.SubQueryOperator = SubQueryOperator.Any;
                        subQuery.SubQueryProperty = propertyName;
                        subQuery.ParentQuery = parentQuery;
                        subQuery.SubQueryDepth = parentQuery?.SubQueryDepth + 1 ?? 0;
                        
                        conditions[$"{propertyName}_any"] = subQuery;
                        Console.WriteLine($"DEBUG: Added ANY condition for {propertyName}");
                        return;
                    }
                }
                else if (pattern == @"(\w+)\s+all\s+in\s+\((.+?)\)")
                {
                    var propertyName = match.Groups[1].Value.Trim();
                    var subQueryText = match.Groups[2].Value.Trim();
                    
                    Console.WriteLine($"DEBUG: Processing ALL IN - Property: {propertyName}, SubQuery: {subQueryText}");
                    
                    var subQuery = ParseSubQueryFromString(subQueryText);
                    if (subQuery != null)
                    {
                        subQuery.SubQueryOperator = SubQueryOperator.All;
                        subQuery.SubQueryProperty = propertyName;
                        subQuery.ParentQuery = parentQuery;
                        subQuery.SubQueryDepth = parentQuery?.SubQueryDepth + 1 ?? 0;
                        
                        conditions[$"{propertyName}_all"] = subQuery;
                        Console.WriteLine($"DEBUG: Added ALL condition for {propertyName}");
                        return;
                    }
                }
                
                // Patterns pour les sous-requêtes imbriquées avec parenthèses
                else if (pattern.Contains(" in (") && pattern.Contains(" where ") && pattern.Contains("([><=!]+)") && pattern.Contains(" ("))
                {
                    var propertyName = match.Groups[1].Value.Trim();
                    var selectProperty = match.Groups[2].Value.Trim();
                    var whereCondition = match.Groups[3].Value.Trim();
                    var comparisonOperator = match.Groups[4].Value.Trim();
                    var nestedSubQuery = match.Groups[5].Value.Trim();
                    
                    Console.WriteLine($"DEBUG: Processing NESTED IN - Property: {propertyName}, Select: {selectProperty}, Where: {whereCondition}, Operator: {comparisonOperator}, Nested: {nestedSubQuery}");
                    
                    // Créer la sous-requête principale
                    var mainSubQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        SubQueryProperty = selectProperty,
                        SubQueryOperator = SubQueryOperator.In,
                        ParentQuery = parentQuery,
                        SubQueryDepth = parentQuery?.SubQueryDepth + 1 ?? 0
                    };
                    
                    // Ajouter la condition WHERE avec la sous-requête imbriquée
                    var fullWhereCondition = $"{whereCondition} {comparisonOperator} ({nestedSubQuery})";
                    ParseConditions(fullWhereCondition, mainSubQuery.Conditions);
                    
                    conditions[$"{propertyName}_nested_in"] = mainSubQuery;
                    Console.WriteLine($"DEBUG: Added NESTED IN condition for {propertyName}");
                    return;
                }
                else if (pattern.Contains(" exists in (") && pattern.Contains(" where ") && pattern.Contains("([><=!]+)") && pattern.Contains(" ("))
                {
                    var propertyName = match.Groups[1].Value.Trim();
                    var selectProperty = match.Groups[2].Value.Trim();
                    var whereCondition = match.Groups[3].Value.Trim();
                    var comparisonOperator = match.Groups[4].Value.Trim();
                    var nestedSubQuery = match.Groups[5].Value.Trim();
                    
                    Console.WriteLine($"DEBUG: Processing NESTED EXISTS - Property: {propertyName}, Select: {selectProperty}, Where: {whereCondition}, Operator: {comparisonOperator}, Nested: {nestedSubQuery}");
                    
                    // Créer la sous-requête principale
                    var mainSubQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        SubQueryProperty = null, // Pas de propriété spécifique pour EXISTS
                        SubQueryOperator = SubQueryOperator.Exists,
                        ParentQuery = parentQuery,
                        SubQueryDepth = parentQuery?.SubQueryDepth + 1 ?? 0
                    };
                    
                    // Ajouter la condition WHERE avec la sous-requête imbriquée
                    var fullWhereCondition = $"{whereCondition} {comparisonOperator} ({nestedSubQuery})";
                    ParseConditions(fullWhereCondition, mainSubQuery.Conditions);
                    
                    conditions[$"{propertyName}_nested_exists"] = mainSubQuery;
                    Console.WriteLine($"DEBUG: Added NESTED EXISTS condition for {propertyName}");
                    return;
                }
                else if (pattern.Contains(" any in (") && pattern.Contains(" where ") && pattern.Contains("([><=!]+)") && pattern.Contains(" ("))
                {
                    var propertyName = match.Groups[1].Value.Trim();
                    var selectProperty = match.Groups[2].Value.Trim();
                    var whereCondition = match.Groups[3].Value.Trim();
                    var comparisonOperator = match.Groups[4].Value.Trim();
                    var nestedSubQuery = match.Groups[5].Value.Trim();
                    
                    Console.WriteLine($"DEBUG: Processing NESTED ANY - Property: {propertyName}, Select: {selectProperty}, Where: {whereCondition}, Operator: {comparisonOperator}, Nested: {nestedSubQuery}");
                    
                    // Créer la sous-requête principale
                    var mainSubQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        SubQueryProperty = selectProperty,
                        SubQueryOperator = SubQueryOperator.Any,
                        ParentQuery = parentQuery,
                        SubQueryDepth = parentQuery?.SubQueryDepth + 1 ?? 0
                    };
                    
                    // Ajouter la condition WHERE avec la sous-requête imbriquée
                    var fullWhereCondition = $"{whereCondition} {comparisonOperator} ({nestedSubQuery})";
                    ParseConditions(fullWhereCondition, mainSubQuery.Conditions);
                    
                    conditions[$"{propertyName}_nested_any"] = mainSubQuery;
                    Console.WriteLine($"DEBUG: Added NESTED ANY condition for {propertyName}");
                    return;
                }
                else if (pattern.Contains(" all in (") && pattern.Contains(" where ") && pattern.Contains("([><=!]+)") && pattern.Contains(" ("))
                {
                    var propertyName = match.Groups[1].Value.Trim();
                    var selectProperty = match.Groups[2].Value.Trim();
                    var whereCondition = match.Groups[3].Value.Trim();
                    var comparisonOperator = match.Groups[4].Value.Trim();
                    var nestedSubQuery = match.Groups[5].Value.Trim();
                    
                    Console.WriteLine($"DEBUG: Processing NESTED ALL - Property: {propertyName}, Select: {selectProperty}, Where: {whereCondition}, Operator: {comparisonOperator}, Nested: {nestedSubQuery}");
                    
                    // Créer la sous-requête principale
                    var mainSubQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        SubQueryProperty = selectProperty,
                        SubQueryOperator = SubQueryOperator.All,
                        ParentQuery = parentQuery,
                        SubQueryDepth = parentQuery?.SubQueryDepth + 1 ?? 0
                    };
                    
                    // Ajouter la condition WHERE avec la sous-requête imbriquée
                    var fullWhereCondition = $"{whereCondition} {comparisonOperator} ({nestedSubQuery})";
                    ParseConditions(fullWhereCondition, mainSubQuery.Conditions);
                    
                    conditions[$"{propertyName}_nested_all"] = mainSubQuery;
                    Console.WriteLine($"DEBUG: Added NESTED ALL condition for {propertyName}");
                    return;
                }
                
                // Patterns existants pour la compatibilité
                else if (pattern.Contains("connected\\s+to\\s+via") && pattern.Contains("where"))
                {
                    // Pattern : "connected to via [edge_type] where [conditions]"
                    var edgeType = match.Groups[1].Value.Trim();
                    var subConditions = match.Groups[2].Value.Trim();
                    
                    Console.WriteLine($"DEBUG: Processing CONNECTED TO VIA - EdgeType: {edgeType}, Conditions: {subConditions}");
                    
                    var subQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        EdgeType = edgeType,
                        Conditions = new Dictionary<string, object>()
                    };
                    
                    ParseConditions(subConditions, subQuery.Conditions);
                    conditions["connected_to_via"] = subQuery;
                    Console.WriteLine($"DEBUG: Added CONNECTED TO VIA condition");
                    return;
                }
                else if (pattern.Contains("connected\\s+to\\s+\\w+\\s+via") && pattern.Contains("where"))
                {
                    // Pattern : "connected to [label] via [edge_type] where [conditions]"
                    var targetLabel = match.Groups[1].Value.Trim();
                    var edgeType = match.Groups[2].Value.Trim();
                    var subConditions = match.Groups[3].Value.Trim();
                    
                    Console.WriteLine($"DEBUG: Processing CONNECTED TO [label] VIA - TargetLabel: {targetLabel}, EdgeType: {edgeType}, Conditions: {subConditions}");
                    
                    var subQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        NodeLabel = targetLabel,
                        EdgeType = edgeType,
                        Conditions = new Dictionary<string, object>()
                    };
                    
                    ParseConditions(subConditions, subQuery.Conditions);
                    conditions["connected_to_via"] = subQuery;
                    Console.WriteLine($"DEBUG: Added CONNECTED TO [label] VIA condition");
                    return;
                }
                else if (pattern.Contains("connected\\s+to\\s+\\w+\\s+\"") && pattern.Contains("where"))
                {
                    // Pattern : "connected to [label] ""name"" via [edge_type] where [conditions]"
                    var targetLabel = match.Groups[1].Value.Trim();
                    var targetName = match.Groups[2].Value.Trim();
                    var edgeType = match.Groups[3].Value.Trim();
                    var subConditions = match.Groups[4].Value.Trim();
                    
                    Console.WriteLine($"DEBUG: Processing CONNECTED TO [label] \"name\" VIA - TargetLabel: {targetLabel}, TargetName: {targetName}, EdgeType: {edgeType}, Conditions: {subConditions}");
                    
                    var subQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        NodeLabel = targetLabel,
                        FromNode = targetName,
                        EdgeType = edgeType,
                        Conditions = new Dictionary<string, object>()
                    };
                    
                    ParseConditions(subConditions, subQuery.Conditions);
                    conditions["connected_to_via"] = subQuery;
                    Console.WriteLine($"DEBUG: Added CONNECTED TO [label] \"name\" VIA condition");
                    return;
                }
                else if (pattern.Contains("has\\s+edge") && pattern.Contains("where"))
                {
                    // Pattern : "has edge [edge_type] to [label] where [conditions]"
                    var edgeType = match.Groups[1].Value.Trim();
                    var targetLabel = match.Groups[2].Value.Trim();
                    var subConditions = match.Groups[3].Value.Trim();
                    
                    Console.WriteLine($"DEBUG: Processing HAS EDGE - EdgeType: {edgeType}, TargetLabel: {targetLabel}, Conditions: {subConditions}");
                    
                    var subQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        NodeLabel = targetLabel,
                        EdgeType = edgeType,
                        Conditions = new Dictionary<string, object>()
                    };
                    
                    ParseConditions(subConditions, subQuery.Conditions);
                    conditions["has_edge"] = subQuery;
                    Console.WriteLine($"DEBUG: Added HAS EDGE condition");
                    return;
                }
                else if (pattern.Contains("connected\\s+via") && pattern.Contains("where"))
                {
                    // Pattern : "connected via [edge_type] where [conditions]"
                    var edgeType = match.Groups[1].Value.Trim();
                    var subConditions = match.Groups[2].Value.Trim();
                    
                    Console.WriteLine($"DEBUG: Processing CONNECTED VIA - EdgeType: {edgeType}, Conditions: {subConditions}");
                    
                    var subQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        EdgeType = edgeType,
                        Conditions = new Dictionary<string, object>()
                    };
                    
                    ParseConditions(subConditions, subQuery.Conditions);
                    conditions["connected_via"] = subQuery;
                    Console.WriteLine($"DEBUG: Added CONNECTED VIA condition");
                    return;
                }
                else if (pattern.Contains("connected\\s+to\\s+via"))
                {
                    // Pattern : "connected to via [edge_type]"
                    var edgeType = match.Groups[1].Value.Trim();
                    conditions["connected_to_via"] = edgeType;
                    Console.WriteLine($"DEBUG: Added CONNECTED TO VIA condition");
                    return;
                }
                else if (pattern.Contains("connected\\s+to\\s+\\w+\\s+via"))
                {
                    // Pattern : "connected to [label] via [edge_type]"
                    var targetLabel = match.Groups[1].Value.Trim();
                    var edgeType = match.Groups[2].Value.Trim();
                    conditions["connected_to_via"] = new { Label = targetLabel, EdgeType = edgeType };
                    Console.WriteLine($"DEBUG: Added CONNECTED TO [label] VIA condition");
                    return;
                }
                else if (pattern.Contains("connected\\s+to\\s+\\w+\\s+\""))
                {
                    // Pattern : "connected to [label] ""name"" via [edge_type]"
                    var targetLabel = match.Groups[1].Value.Trim();
                    var targetName = match.Groups[2].Value.Trim();
                    var edgeType = match.Groups[3].Value.Trim();
                    conditions["connected_to_via"] = new { Label = targetLabel, Name = targetName, EdgeType = edgeType };
                    Console.WriteLine($"DEBUG: Added CONNECTED TO [label] \"name\" VIA condition");
                    return;
                }
                else if (pattern.Contains("has\\s+edge"))
                {
                    // Pattern : "has edge [edge_type] to [label]"
                    var edgeType = match.Groups[1].Value.Trim();
                    var targetLabel = match.Groups[2].Value.Trim();
                    conditions["has_edge"] = new { EdgeType = edgeType, TargetLabel = targetLabel };
                    Console.WriteLine($"DEBUG: Added HAS EDGE condition");
                    return;
                }
                else if (pattern.Contains("connected\\s+via"))
                {
                    // Pattern : "connected via [edge_type]"
                    var edgeType = match.Groups[1].Value.Trim();
                    conditions["connected_via"] = edgeType;
                    Console.WriteLine($"DEBUG: Added CONNECTED VIA condition");
                    return;
                }
                else if (pattern.Contains("in\\s+select"))
                {
                    var propertyName = match.Groups[1].Value.Trim();
                    var selectProperty = match.Groups[2].Value.Trim();
                    var fromLabel = match.Groups[3].Value.Trim();
                    var whereConditions = match.Groups.Count > 4 ? match.Groups[4].Value.Trim() : "";
                    
                    Console.WriteLine($"DEBUG: Processing IN SELECT - Property: {propertyName}, Select: {selectProperty}, From: {fromLabel}, Where: {whereConditions}");
                    
                    // Créer la sous-requête
                    var subQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        NodeLabel = NormalizeLabel(fromLabel),
                        SubQueryProperty = selectProperty,
                        SubQueryOperator = SubQueryOperator.In,
                        ParentQuery = parentQuery,
                        SubQueryDepth = parentQuery?.SubQueryDepth + 1 ?? 0
                    };
                    
                    Console.WriteLine($"DEBUG: Created subquery - Label: {subQuery.NodeLabel}, Property: {subQuery.SubQueryProperty}");
                    
                    // Ajouter les conditions WHERE si présentes
                    if (!string.IsNullOrEmpty(whereConditions))
                    {
                        ParseSimpleConditions(whereConditions, subQuery.Conditions);
                    }
                    
                    conditions[$"{propertyName}_in"] = subQuery;
                    Console.WriteLine($"DEBUG: Added IN SELECT condition for {propertyName}");
                    return;
                }
            }
        }

        Console.WriteLine($"DEBUG: No complex patterns matched, falling back to simple parsing");
        
        // Si aucun pattern complexe ne correspond, utiliser le parsing simple
        ParseSimpleConditions(conditionsText, conditions);
    }

    /// <summary>
    /// Parse une sous-requête à partir d'une chaîne de caractères
    /// </summary>
    private ParsedQuery? ParseSubQueryFromString(string query)
    {
        try
        {
            var parsedQuery = new ParsedQuery();
            ParseSubQuery(query, parsedQuery);
            
            if (parsedQuery.ValidationErrors.Any())
            {
                return null;
            }
            
            return parsedQuery;
        }
        catch
        {
            return null;
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
    /// Parse une sous-requête améliorée avec support complet des agrégations
    /// </summary>
    private void ParseSubQuery(string query, ParsedQuery parsedQuery)
    {
        try
        {
            Console.WriteLine($"DEBUG: ParseSubQuery - Input: '{query}'");
            
            // Nettoyer la requête
            query = query.Trim();
            
            // Patterns pour sous-requêtes avec support amélioré des agrégations
            var patterns = new[]
            {
                // Pattern 0.5: SELECT simple "select property from label where ..."
                @"select\s+(\w+|\*)\s+from\s+(\w+)(?:\s+where\s+(.+))?",
                // Pattern 0: Valeurs simples "(25, 30, 35)" ou "25, 30, 35"
                @"^([^,]+(?:,\s*[^,]+)*)$",
                // Pattern 1: Agrégation simple "avg persons property age where ..."
                @"(sum|avg|min|max|count)\s+(\w+)\s+property\s+(\w+)(?:\s+where\s+(.+))?",
                // Pattern 2: Agrégation avec "from" "avg age from persons where ..."
                @"(sum|avg|min|max|count)\s+(\w+)\s+from\s+(\w+)(?:\s+where\s+(.+))?",
                // Pattern 3: Agrégation avec parenthèses "avg(age) from persons where ..."
                @"(sum|avg|min|max|count)\s*\(\s*(\w+)\s*\)\s+from\s+(\w+)(?:\s+where\s+(.+))?",
                // Pattern 4: SELECT avec agrégation "select avg age from persons where ..."
                @"select\s+(sum|avg|min|max|count)\s+(\w+)\s+from\s+(\w+)(?:\s+where\s+(.+))?",
                // Pattern 5: FIND simple "find persons where ..."
                @"find\s+(?:all\s+)?(\w+)(?:\s+where\s+(.+))?",
                // Pattern 6: COUNT simple "count persons where ..."
                @"count\s+(?:all\s+)?(\w+)(?:\s+where\s+(.+))?",
                // Pattern 7: SELECT simple "select property from label where ..."
                @"select\s+(\w+|\*)\s+from\s+(\w+)(?:\s+where\s+(.+))?"
            };

            foreach (var pattern in patterns)
            {
                var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);
                if (match.Success)
                {
                    Console.WriteLine($"DEBUG: Pattern matched: '{pattern}'");
                    Console.WriteLine($"DEBUG: Groups: {string.Join(", ", match.Groups.Cast<Group>().Select(g => g.Value))}");
                    
                    if (pattern == @"select\s+(\w+|\*)\s+from\s+(\w+)(?:\s+where\s+(.+))?")
                    {
                        // Pattern 0.5: SELECT simple
                        parsedQuery.SubQueryProperty = match.Groups[1].Value;
                        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[2].Value);
                        parsedQuery.Type = QueryType.FindNodes;
                        
                        Console.WriteLine($"DEBUG: Parsed as SELECT - Property: {parsedQuery.SubQueryProperty}, Label: {parsedQuery.NodeLabel}");
                        
                        if (match.Groups.Count > 3 && !string.IsNullOrEmpty(match.Groups[3].Value))
                        {
                            ParseSimpleConditions(match.Groups[3].Value, parsedQuery.Conditions);
                        }
                    }
                    else if (pattern == @"^([^,]+(?:,\s*[^,]+)*)$")
                    {
                        // Pattern 0: Valeurs simples comme "25, 30, 35"
                        var valuesText = match.Groups[1].Value.Trim();
                        var values = valuesText.Split(',').Select(v => ParseDynamicValue(v.Trim())).ToList();
                        
                        parsedQuery.Type = QueryType.FindNodes;
                        parsedQuery.NodeLabel = "values";
                        parsedQuery.SubQueryProperty = "values";
                        
                        // Stocker les valeurs dans les conditions pour les récupérer plus tard
                        parsedQuery.Conditions["values"] = values;
                        // Stocker aussi dans SubQueryProperty pour l'extraction
                        parsedQuery.SubQueryProperty = "values";
                        
                        Console.WriteLine($"DEBUG: Parsed as VALUES - Values: {string.Join(", ", values)}");
                    }
                    else if (pattern.Contains("property") || pattern.Contains("from") || (pattern.Contains("select") && pattern.Contains("(sum|avg|min|max|count)")))
                    {
                        // Patterns 1-4: Agrégations
                        var functionName = match.Groups[1].Value.ToLowerInvariant();
                        var propertyName = match.Groups[2].Value;
                        var labelName = match.Groups[3].Value;
                        var conditionsText = match.Groups.Count > 4 ? match.Groups[4].Value : "";

                        parsedQuery.Type = QueryType.Aggregate;
                        parsedQuery.AggregateFunction = functionName switch
                        {
                            "sum" => AggregateFunction.Sum,
                            "avg" => AggregateFunction.Avg,
                            "min" => AggregateFunction.Min,
                            "max" => AggregateFunction.Max,
                            "count" => AggregateFunction.Count,
                            _ => AggregateFunction.Count
                        };
                        
                        // CORRECTION: Gérer correctement les groupes selon le pattern
                        if (pattern.Contains("property"))
                        {
                            // Pattern: "avg persons property salary" -> Groups[1]=function, Groups[2]=label, Groups[3]=property
                            parsedQuery.AggregateProperty = match.Groups[3].Value;
                            parsedQuery.NodeLabel = NormalizeLabel(match.Groups[2].Value);
                        }
                        else if (pattern.Contains("from"))
                        {
                            // Pattern: "avg salary from persons" -> Groups[1]=function, Groups[2]=property, Groups[3]=label
                            parsedQuery.AggregateProperty = match.Groups[2].Value;
                            parsedQuery.NodeLabel = NormalizeLabel(match.Groups[3].Value);
                        }
                        else
                        {
                            // Pattern: "avg persons salary" -> Groups[1]=function, Groups[2]=label, Groups[3]=property
                            parsedQuery.AggregateProperty = match.Groups[3].Value;
                            parsedQuery.NodeLabel = NormalizeLabel(match.Groups[2].Value);
                        }
                        
                        Console.WriteLine($"DEBUG: Parsed as AGGREGATE - Function: {parsedQuery.AggregateFunction}, Property: {parsedQuery.AggregateProperty}, Label: {parsedQuery.NodeLabel}");
                        
                        if (!string.IsNullOrEmpty(conditionsText))
                        {
                            ParseConditions(conditionsText, parsedQuery.Conditions);
                        }
                    }
                    else if (pattern.Contains("find"))
                    {
                        // Pattern 5: FIND
                        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                        parsedQuery.Type = QueryType.FindNodes;
                        
                        Console.WriteLine($"DEBUG: Parsed as FIND - Label: {parsedQuery.NodeLabel}");
                        
                        if (match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[2].Value))
                        {
                            ParseConditions(match.Groups[2].Value, parsedQuery.Conditions);
                        }
                    }
                    else if (pattern.Contains("count"))
                    {
                        // Pattern 6: COUNT
                        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[1].Value);
                        parsedQuery.Type = QueryType.Count;
                        
                        Console.WriteLine($"DEBUG: Parsed as COUNT - Label: {parsedQuery.NodeLabel}");
                        
                        if (match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[2].Value))
                        {
                            ParseConditions(match.Groups[2].Value, parsedQuery.Conditions);
                        }
                    }
                    else
                    {
                        // Pattern 7: SELECT simple
                        parsedQuery.SubQueryProperty = match.Groups[1].Value;
                        parsedQuery.NodeLabel = NormalizeLabel(match.Groups[2].Value);
                        parsedQuery.Type = QueryType.FindNodes;
                        
                        Console.WriteLine($"DEBUG: Parsed as SELECT - Property: {parsedQuery.SubQueryProperty}, Label: {parsedQuery.NodeLabel}");
                        
                        if (match.Groups.Count > 3 && !string.IsNullOrEmpty(match.Groups[3].Value))
                        {
                            ParseSimpleConditions(match.Groups[3].Value, parsedQuery.Conditions);
                        }
                    }
                    
                    return;
                }
            }

            Console.WriteLine($"DEBUG: No patterns matched for subquery: '{query}'");
            
            // Si aucun pattern ne correspond, essayer un parsing simple
            var simpleMatch = Regex.Match(query, @"(\w+)", RegexOptions.IgnoreCase);
            if (simpleMatch.Success)
            {
                parsedQuery.NodeLabel = NormalizeLabel(simpleMatch.Groups[1].Value);
                parsedQuery.Type = QueryType.FindNodes;
                Console.WriteLine($"DEBUG: Fallback parsing - Label: {parsedQuery.NodeLabel}");
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
        Console.WriteLine($"DEBUG: ParseDynamicProperties - Input: '{propertiesText}'");
        
        // Vérifier si c'est le format "properties {...}" ou "with properties {...}"
        if (propertiesText.StartsWith("properties", StringComparison.OrdinalIgnoreCase))
        {
            var braceMatch = Regex.Match(propertiesText, @"properties\s*\{([^}]+)\}", RegexOptions.IgnoreCase);
            if (braceMatch.Success)
            {
                var propertiesContent = braceMatch.Groups[1].Value.Trim();
                ParsePropertiesFromBraceContent(propertiesContent, properties);
                return;
            }
        }
        else if (propertiesText.StartsWith("with properties", StringComparison.OrdinalIgnoreCase))
        {
            var braceMatch = Regex.Match(propertiesText, @"with\s+properties\s*\{([^}]+)\}", RegexOptions.IgnoreCase);
            if (braceMatch.Success)
            {
                var propertiesContent = braceMatch.Groups[1].Value.Trim();
                ParsePropertiesFromBraceContent(propertiesContent, properties);
                return;
            }
        }
        
        // Approche simple et directe : diviser par " and " et parser chaque partie
        var parts = propertiesText.Split(new[] { " and " }, StringSplitOptions.RemoveEmptyEntries);
        
        foreach (var part in parts)
        {
            var trimmedPart = part.Trim();
            var spaceIndex = trimmedPart.IndexOf(' ');
            
            if (spaceIndex > 0)
            {
                var key = trimmedPart.Substring(0, spaceIndex);
                var value = trimmedPart.Substring(spaceIndex + 1).Trim();
                
                Console.WriteLine($"DEBUG: Parsing part '{trimmedPart}' -> key: '{key}', value: '{value}'");
                
                // Supprimer les guillemets si présents
                if (value.StartsWith("\"") && value.EndsWith("\""))
                {
                    value = value.Substring(1, value.Length - 2);
                }
                
                // Parser la valeur
                try
                {
                    properties[key] = ParseDynamicValue(value);
                    Console.WriteLine($"DEBUG: Added property: {key} = {properties[key]}");
                }
                catch (Exception ex)
                {
                    // En cas d'erreur, utiliser la valeur brute
                    properties[key] = value;
                    Console.WriteLine($"DEBUG: Added property (fallback): {key} = {value}");
                }
            }
        }
        
        Console.WriteLine($"DEBUG: Final properties: {string.Join(", ", properties.Select(kv => $"{kv.Key}={kv.Value}"))}");
    }

    /// <summary>
    /// Parse les propriétés manuellement pour les cas complexes
    /// </summary>
    private void ParsePropertiesManually(string propertiesText, Dictionary<string, object> properties)
    {
        Console.WriteLine($"DEBUG: ParsePropertiesManually - Input: '{propertiesText}'");
        
        // Diviser par " and " pour traiter chaque propriété séparément
        var parts = propertiesText.Split(new[] { " and " }, StringSplitOptions.RemoveEmptyEntries);
        
        Console.WriteLine($"DEBUG: Split parts: {string.Join(" | ", parts)}");
        
        foreach (var part in parts)
        {
            var trimmedPart = part.Trim();
            var spaceIndex = trimmedPart.IndexOf(' ');
            
            Console.WriteLine($"DEBUG: Processing part: '{trimmedPart}', spaceIndex: {spaceIndex}");
            
            if (spaceIndex > 0)
            {
                var key = trimmedPart.Substring(0, spaceIndex);
                var value = trimmedPart.Substring(spaceIndex + 1).Trim();
                
                Console.WriteLine($"DEBUG: Extracted key: '{key}', value: '{value}'");
                
                // Supprimer les guillemets si présents
                if (value.StartsWith("\"") && value.EndsWith("\""))
                {
                    value = value.Substring(1, value.Length - 2);
                }
                
                // Parser la valeur
                try
                {
                    properties[key] = ParseDynamicValue(value);
                    Console.WriteLine($"DEBUG: Added property: {key} = {properties[key]}");
                }
                catch (Exception ex)
                {
                    // En cas d'erreur, utiliser la valeur brute
                    properties[key] = value;
                    Console.WriteLine($"DEBUG: Added property (fallback): {key} = {value}");
                }
            }
        }
        
        // Debug: afficher les propriétés parsées
        Console.WriteLine($"DEBUG: Final parsed properties: {string.Join(", ", properties.Select(kv => $"{kv.Key}={kv.Value}"))}");
    }

    /// <summary>
    /// Parse les propriétés avec une approche plus robuste pour les cas complexes
    /// </summary>
    private void ParsePropertiesRobust(string propertiesText, Dictionary<string, object> properties)
    {
        // Pattern spécifique pour les propriétés avec valeurs numériques et textuelles
        // Exemple: "salary 75000 duration 24 months"
        var pattern = @"(\w+)\s+([^\s]+(?:\s+[^\s]+)*?)(?=\s+\w+\s|$)";
        var matches = Regex.Matches(propertiesText, pattern);
        
        foreach (Match match in matches)
        {
            var key = match.Groups[1].Value;
            var value = match.Groups[2].Value.Trim();
            
            // Nettoyer la valeur des mots-clés réservés
            var reservedWords = new[] { "and", "with", "type", "from", "to" };
            var valueWords = value.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var cleanValueWords = new List<string>();
            
            foreach (var word in valueWords)
            {
                if (!reservedWords.Contains(word.ToLowerInvariant()))
                {
                    cleanValueWords.Add(word);
                }
            }
            
            if (cleanValueWords.Count > 0)
            {
                var cleanValue = string.Join(" ", cleanValueWords);
                properties[key] = ParseDynamicValue(cleanValue);
            }
        }
    }

    /// <summary>
    /// Parse les propriétés avec une approche manuelle pour les cas complexes
    /// </summary>
    private void ParsePropertiesManual(string propertiesText, Dictionary<string, object> properties)
    {
        var words = propertiesText.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var i = 0;
        
        while (i < words.Length)
        {
            var key = words[i];
            i++;
            
            if (i >= words.Length) break;
            
            // Collecter la valeur jusqu'au prochain mot qui ressemble à une clé
            var valueParts = new List<string>();
            
            while (i < words.Length)
            {
                var word = words[i];
                
                // Si le mot suivant ressemble à une clé (pas de chiffres au début), arrêter
                if (char.IsLetter(word[0]) && !char.IsDigit(word[0]) && 
                    !word.Equals("and", StringComparison.OrdinalIgnoreCase) &&
                    !word.Equals("with", StringComparison.OrdinalIgnoreCase) &&
                    !word.Equals("type", StringComparison.OrdinalIgnoreCase))
                {
                    break;
                }
                
                valueParts.Add(word);
                i++;
            }
            
            if (valueParts.Count > 0)
            {
                var value = string.Join(" ", valueParts);
                properties[key] = ParseDynamicValue(value);
            }
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
        
        // Sinon, c'est une chaîne (enlever les guillemets et caractères parasites si présents)
        var cleanedValue = inputValue.Trim('"', '\'', ' ', '\t', '\n', '\r');
        // Enlever aussi les accolades et autres caractères parasites
        cleanedValue = cleanedValue.Trim('{', '}', '[', ']');
        // Enlever les caractères parasites supplémentaires
        cleanedValue = cleanedValue.Trim('"', '\'', ' ', '\t', '\n', '\r');
        // Enlever les caractères parasites à la fin (comme les virgules et guillemets)
        cleanedValue = cleanedValue.TrimEnd(',', '"', '\'', ' ', '\t', '\n', '\r');
        return cleanedValue;
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
        if (string.IsNullOrWhiteSpace(conditionsText))
            return;

        // Vérifier si la condition contient des patterns de sous-requêtes
        var subQueryPatterns = new[]
        {
            "exists in",
            "not exists in", 
            "exists where",
            "not exists where",
            "in select",
            "not in select",
            "any in",
            "all in",
            "gt avg",
            "lt avg",
            "gt max",
            "lt max",
            "gt min",
            "lt min"
        };

        bool hasSubQueryPattern = subQueryPatterns.Any(pattern => 
            conditionsText.Contains(pattern, StringComparison.OrdinalIgnoreCase));

        if (hasSubQueryPattern)
        {
            // Utiliser le parser de sous-requêtes complexes
            ParseConditionsWithSubQueries(conditionsText, conditions, null);
            return;
        }

        // Utiliser le parsing simple des conditions
        ParseSimpleConditions(conditionsText, conditions);
    }

    /// <summary>
    /// Parse les conditions simples sans sous-requêtes (pour éviter la récursion infinie)
    /// </summary>
    private void ParseSimpleConditions(string conditionsText, Dictionary<string, object> conditions)
    {

        // Pattern amélioré pour supporter AND et OR
        // Ex: "age > 25 and name = John or status = active"
        var parts = SplitConditions(conditionsText);
        
        // Compteur pour générer des clés uniques pour les conditions OR
        var orConditionCounter = 0;
        
        foreach (var part in parts)
        {
            // Pattern étendu pour supporter les fonctions de chaînes et opérateurs avancés
            // Correction : inclure les underscores dans les noms d'opérateurs et les guillemets simples
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
                
                // Nettoyer la valeur si c'est une chaîne avec des guillemets superflus
                if (value is string strValue && strValue.Contains("\""))
                {
                    // Nettoyer les guillemets dans les conditions complexes
                    if (strValue.Contains(" and ") || strValue.Contains(" or ") || strValue.Contains(" eq ") || strValue.Contains(" gt ") || strValue.Contains(" lt "))
                    {
                        value = strValue.Replace("\"", "");
                    }
                    // Nettoyer les guillemets doubles
                    else if (strValue.StartsWith("\"\"") && strValue.EndsWith("\"\""))
                    {
                        value = strValue.Substring(2, strValue.Length - 4);
                    }
                    // Nettoyer les guillemets simples
                    else if (strValue.StartsWith("\"") && strValue.EndsWith("\""))
                    {
                        value = strValue.Substring(1, strValue.Length - 2);
                    }
                }
                
                // Nettoyer les "and" en trop dans les valeurs simples
                if (value is string strValue2 && strValue2.Contains(" and ") && !strValue2.Contains("=") && !strValue2.Contains(">") && !strValue2.Contains("<"))
                {
                    value = strValue2.Replace(" and ", "");
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

    /// <summary>
    /// Parse les propriétés depuis le contenu entre accolades
    /// Format : {name: "value", age: 30, department: "IT"}
    /// </summary>
    private void ParsePropertiesFromBraceContent(string content, Dictionary<string, object> properties)
    {
        Console.WriteLine($"DEBUG: ParsePropertiesFromBraceContent - Input: '{content}'");
        
        // Diviser par virgules pour traiter chaque propriété
        var parts = content.Split(',');
        
        foreach (var part in parts)
        {
            var trimmedPart = part.Trim();
            if (string.IsNullOrEmpty(trimmedPart)) continue;
            
            // Chercher le pattern "key: value"
            var colonIndex = trimmedPart.IndexOf(':');
            if (colonIndex > 0)
            {
                var key = trimmedPart.Substring(0, colonIndex).Trim();
                var value = trimmedPart.Substring(colonIndex + 1).Trim();
                
                Console.WriteLine($"DEBUG: Parsing property '{key}' = '{value}'");
                
                // Supprimer les guillemets si présents
                if (value.StartsWith("\"") && value.EndsWith("\""))
                {
                    value = value.Substring(1, value.Length - 2);
                }
                else if (value.StartsWith("'") && value.EndsWith("'"))
                {
                    value = value.Substring(1, value.Length - 2);
                }
                
                // Parser la valeur
                try
                {
                    properties[key] = ParseDynamicValue(value);
                    Console.WriteLine($"DEBUG: Added property: {key} = {properties[key]}");
                }
                catch (Exception ex)
                {
                    // En cas d'erreur, utiliser la valeur brute
                    properties[key] = value;
                    Console.WriteLine($"DEBUG: Added property (fallback): {key} = {value}");
                }
            }
        }
        
        Console.WriteLine($"DEBUG: Final properties from brace: {string.Join(", ", properties.Select(kv => $"{kv.Key}={kv.Value}"))}");
    }

    /// <summary>
    /// Parse une requête de jointure virtuelle
    /// Exemples :
    /// - "join persons with projects via works_on"
    /// - "virtual join persons and projects where department = 'IT'"
    /// - "merge persons with companies on company_id"
    /// </summary>
    private void ParseVirtualJoin(string query, ParsedQuery parsedQuery)
    {
        Console.WriteLine($"DEBUG: ParseVirtualJoin - Query: {query}");
        
        // Patterns pour les jointures virtuelles
        var patterns = new[]
        {
            // Pattern 1: "join persons with projects via works_on"
            @"join\s+(\w+)\s+with\s+(\w+)\s+via\s+(\w+)",
            
            // Pattern 2: "virtual join persons and projects where department = 'IT'"
            @"(?:virtual\s+)?join\s+(\w+)\s+and\s+(\w+)(?:\s+where\s+(.+))?",
            
            // Pattern 3: "merge persons with companies on company_id"
            @"merge\s+(\w+)\s+with\s+(\w+)\s+on\s+(\w+)",
            
            // Pattern 4: "combine persons and projects via works_on where age > 25"
            @"combine\s+(\w+)\s+and\s+(\w+)\s+via\s+(\w+)(?:\s+where\s+(.+))?",
            
            // Pattern 5: "join persons with projects within 3 steps"
            @"join\s+(\w+)\s+with\s+(\w+)\s+within\s+(\d+)\s+steps",
            
            // Pattern 6: "virtual join persons and projects bidirectional"
            @"(?:virtual\s+)?join\s+(\w+)\s+and\s+(\w+)\s+bidirectional"
        };

        foreach (var pattern in patterns)
        {
            var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                Console.WriteLine($"DEBUG: ParseVirtualJoin - Pattern matched: {pattern}");
                
                var virtualJoin = new VirtualJoin();
                
                switch (pattern)
                {
                    case @"join\s+(\w+)\s+with\s+(\w+)\s+via\s+(\w+)":
                        virtualJoin.SourceNodeLabel = NormalizeLabel(match.Groups[1].Value);
                        virtualJoin.TargetNodeLabel = NormalizeLabel(match.Groups[2].Value);
                        virtualJoin.EdgeType = match.Groups[3].Value;
                        break;
                        
                    case @"(?:virtual\s+)?join\s+(\w+)\s+and\s+(\w+)(?:\s+where\s+(.+))?":
                        virtualJoin.SourceNodeLabel = NormalizeLabel(match.Groups[1].Value);
                        virtualJoin.TargetNodeLabel = NormalizeLabel(match.Groups[2].Value);
                        if (match.Groups.Count > 3 && !string.IsNullOrEmpty(match.Groups[3].Value))
                        {
                            ParseConditionsWithSubQueries(match.Groups[3].Value, virtualJoin.JoinConditions, parsedQuery);
                        }
                        break;
                        
                    case @"merge\s+(\w+)\s+with\s+(\w+)\s+on\s+(\w+)":
                        virtualJoin.SourceNodeLabel = NormalizeLabel(match.Groups[1].Value);
                        virtualJoin.TargetNodeLabel = NormalizeLabel(match.Groups[2].Value);
                        virtualJoin.JoinProperty = match.Groups[3].Value;
                        virtualJoin.JoinOperator = "=";
                        break;
                        
                    case @"combine\s+(\w+)\s+and\s+(\w+)\s+via\s+(\w+)(?:\s+where\s+(.+))?":
                        virtualJoin.SourceNodeLabel = NormalizeLabel(match.Groups[1].Value);
                        virtualJoin.TargetNodeLabel = NormalizeLabel(match.Groups[2].Value);
                        virtualJoin.EdgeType = match.Groups[3].Value;
                        if (match.Groups.Count > 4 && !string.IsNullOrEmpty(match.Groups[4].Value))
                        {
                            ParseConditionsWithSubQueries(match.Groups[4].Value, virtualJoin.JoinConditions, parsedQuery);
                        }
                        break;
                        
                    case @"join\s+(\w+)\s+with\s+(\w+)\s+within\s+(\d+)\s+steps":
                        virtualJoin.SourceNodeLabel = NormalizeLabel(match.Groups[1].Value);
                        virtualJoin.TargetNodeLabel = NormalizeLabel(match.Groups[2].Value);
                        virtualJoin.MaxSteps = int.Parse(match.Groups[3].Value);
                        break;
                        
                    case @"(?:virtual\s+)?join\s+(\w+)\s+and\s+(\w+)\s+bidirectional":
                        virtualJoin.SourceNodeLabel = NormalizeLabel(match.Groups[1].Value);
                        virtualJoin.TargetNodeLabel = NormalizeLabel(match.Groups[2].Value);
                        virtualJoin.IsBidirectional = true;
                        break;
                }
                
                // Déterminer le type de jointure par défaut
                parsedQuery.JoinType = "inner";
                
                // Ajouter la jointure virtuelle à la requête
                parsedQuery.VirtualJoins.Add(virtualJoin);
                
                Console.WriteLine($"DEBUG: ParseVirtualJoin - Virtual join created: {virtualJoin.SourceNodeLabel} -> {virtualJoin.TargetNodeLabel}");
                return;
            }
        }
        
        // Si aucun pattern ne correspond, essayer de parser comme une requête de recherche avec jointure
        var fallbackPattern = @"(?:virtual\s+)?join\s+(\w+)(?:\s+where\s+(.+))?";
        var fallbackMatch = Regex.Match(query, fallbackPattern, RegexOptions.IgnoreCase);
        if (fallbackMatch.Success)
        {
            parsedQuery.NodeLabel = NormalizeLabel(fallbackMatch.Groups[1].Value);
            if (fallbackMatch.Groups.Count > 2 && !string.IsNullOrEmpty(fallbackMatch.Groups[2].Value))
            {
                ParseConditionsWithSubQueries(fallbackMatch.Groups[2].Value, parsedQuery.Conditions, parsedQuery);
            }
        }
        else
        {
            throw new ArgumentException($"Format de jointure virtuelle non reconnu : {query}");
        }
    }

    /// <summary>
    /// Parse les requêtes de groupement
    /// Exemples :
    /// - group persons by age
    /// - group persons by age, city
    /// - group persons by age having count > 5
    /// </summary>
    private void ParseGroupBy(string query, ParsedQuery parsedQuery)
    {
        // Pattern pour group by avec conditions WHERE et HAVING
        var groupByPattern = @"group\s+(\w+)\s+by\s+(.+?)(?:\s+where\s+(.+?))?(?:\s+having\s+(.+))?$";
        var match = Regex.Match(query, groupByPattern, RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            parsedQuery.NodeLabel = match.Groups[1].Value;
            
            // Parser les propriétés de groupement
            var groupByProperties = match.Groups[2].Value.Split(',')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrEmpty(p))
                .ToList();
            
            parsedQuery.GroupByProperties.AddRange(groupByProperties);
            
            // Parser les conditions WHERE si présentes
            if (match.Groups.Count > 3 && !string.IsNullOrEmpty(match.Groups[3].Value))
            {
                ParseConditions(match.Groups[3].Value, parsedQuery.Conditions);
            }
            
            // Parser les conditions HAVING si présentes
            if (match.Groups.Count > 4 && !string.IsNullOrEmpty(match.Groups[4].Value))
            {
                ParseConditions(match.Groups[4].Value, parsedQuery.HavingConditions);
            }
        }
    }

    /// <summary>
    /// Parse les requêtes de tri
    /// Exemples :
    /// - order persons by age
    /// - order persons by age desc
    /// - order persons by age, name asc
    /// - sort persons by salary desc, name asc
    /// </summary>
    private void ParseOrderBy(string query, ParsedQuery parsedQuery)
    {
        // Pattern pour order by avec conditions WHERE
        var orderByPattern = @"(?:order|sort)\s+(\w+)\s+by\s+(.+?)(?:\s+where\s+(.+?))?(?:\s+(asc|desc))?$";
        var match = Regex.Match(query, orderByPattern, RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            parsedQuery.NodeLabel = match.Groups[1].Value;
            
            // Parser les clauses de tri
            var orderByClauses = match.Groups[2].Value.Split(',')
                .Select(c => c.Trim())
                .Where(c => !string.IsNullOrEmpty(c))
                .ToList();
            
            foreach (var clause in orderByClauses)
            {
                var orderByClause = new OrderByClause();
                
                // Vérifier si la direction est spécifiée dans la clause
                var directionMatch = Regex.Match(clause, @"(.+?)\s+(asc|desc)$", RegexOptions.IgnoreCase);
                if (directionMatch.Success)
                {
                    orderByClause.Property = directionMatch.Groups[1].Value.Trim();
                    orderByClause.Direction = directionMatch.Groups[2].Value.ToLowerInvariant() == "desc" 
                        ? OrderDirection.Descending 
                        : OrderDirection.Ascending;
                }
                else
                {
                    orderByClause.Property = clause;
                    // Direction par défaut depuis le match principal
                    if (match.Groups.Count > 4 && !string.IsNullOrEmpty(match.Groups[4].Value))
                    {
                        orderByClause.Direction = match.Groups[4].Value.ToLowerInvariant() == "desc" 
                            ? OrderDirection.Descending 
                            : OrderDirection.Ascending;
                    }
                }
                
                parsedQuery.OrderByClauses.Add(orderByClause);
            }
            
            // Parser les conditions WHERE si présentes
            if (match.Groups.Count > 3 && !string.IsNullOrEmpty(match.Groups[3].Value))
            {
                ParseConditions(match.Groups[3].Value, parsedQuery.Conditions);
            }
        }
    }

    /// <summary>
    /// Parse les requêtes HAVING
    /// Exemples :
    /// - having count > 5
    /// - having avg age > 30
    /// </summary>
    private void ParseHaving(string query, ParsedQuery parsedQuery)
    {
        // Pattern pour having
        var havingPattern = @"having\s+(.+)$";
        var match = Regex.Match(query, havingPattern, RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            ParseConditions(match.Groups[1].Value, parsedQuery.HavingConditions);
        }
    }

    /// <summary>
    /// Parse les requêtes de fonctions de fenêtre
    /// Exemples :
    /// - row_number() over (partition by city order by salary desc)
    /// - rank() over (partition by role order by age)
    /// - dense_rank() over (order by salary desc)
    /// </summary>
    private void ParseWindowFunction(string query, ParsedQuery parsedQuery)
    {
        // Pattern pour les fonctions de fenêtre
        var windowPattern = @"(\w+(?:_\w+)?)\s*\(\s*\)\s+over\s*\(\s*(?:partition\s+by\s+([^)]+))?\s*(?:order\s+by\s+([^)]+))?\s*\)";
        var match = Regex.Match(query, windowPattern, RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            // Déterminer le type de fonction de fenêtre
            var functionName = match.Groups[1].Value.ToLowerInvariant();
            parsedQuery.WindowFunctionType = functionName switch
            {
                "row_number" or "rownumber" => WindowFunctionType.RowNumber,
                "rank" => WindowFunctionType.Rank,
                "dense_rank" or "denserank" => WindowFunctionType.DenseRank,
                "percent_rank" or "percentrank" => WindowFunctionType.PercentRank,
                "ntile" => WindowFunctionType.Ntile,
                "lead" => WindowFunctionType.Lead,
                "lag" => WindowFunctionType.Lag,
                "first_value" or "firstvalue" => WindowFunctionType.FirstValue,
                "last_value" or "lastvalue" => WindowFunctionType.LastValue,
                "nth_value" or "nthvalue" => WindowFunctionType.NthValue,
                _ => throw new ArgumentException($"Fonction de fenêtre non reconnue : {functionName}")
            };
            
            // Parser la clause PARTITION BY si présente
            if (match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[2].Value))
            {
                var partitionByProperties = match.Groups[2].Value.Split(',')
                    .Select(p => p.Trim())
                    .Where(p => !string.IsNullOrEmpty(p))
                    .ToList();
                
                parsedQuery.WindowPartitionBy.AddRange(partitionByProperties);
            }
            
            // Parser la clause ORDER BY si présente
            if (match.Groups.Count > 3 && !string.IsNullOrEmpty(match.Groups[3].Value))
            {
                var orderByClauses = match.Groups[3].Value.Split(',')
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToList();
                
                foreach (var clause in orderByClauses)
                {
                    var orderByClause = new OrderByClause();
                    
                    // Vérifier si la direction est spécifiée dans la clause
                    var directionMatch = Regex.Match(clause, @"(.+?)\s+(asc|desc)$", RegexOptions.IgnoreCase);
                    if (directionMatch.Success)
                    {
                        orderByClause.Property = directionMatch.Groups[1].Value.Trim();
                        orderByClause.Direction = directionMatch.Groups[2].Value.ToLower() == "desc" 
                            ? OrderDirection.Descending 
                            : OrderDirection.Ascending;
                    }
                    else
                    {
                        orderByClause.Property = clause;
                        orderByClause.Direction = OrderDirection.Ascending;
                    }
                    
                    parsedQuery.WindowOrderBy.Add(orderByClause);
                }
            }
        }
        else
        {
            throw new ArgumentException($"Format de fonction de fenêtre non reconnu : {query}");
        }
    }

    /// <summary>
    /// Parse les commandes d'affichage des propriétés indexées
    /// Exemples :
    /// - show indexed properties
    /// - show index
    /// </summary>
    private void ParseShowIndexedProperties(string query, ParsedQuery parsedQuery)
    {
        // Cette commande ne nécessite pas de parsing supplémentaire
        // Elle affiche simplement les propriétés indexées automatiquement
    }

    /// <summary>
    /// Parse les commandes d'affichage des statistiques d'index
    /// Exemples :
    /// - show index stats
    /// - index stats
    /// </summary>
    private void ParseShowIndexStats(string query, ParsedQuery parsedQuery)
    {
        // Cette commande ne nécessite pas de parsing supplémentaire
        // Elle affiche simplement les statistiques des index
    }

    /// <summary>
    /// Parse les commandes d'ajout de propriété à l'index
    /// Exemples :
    /// - add index property "experience"
    /// - add index property experience
    /// </summary>
    private void ParseAddIndexProperty(string query, ParsedQuery parsedQuery)
    {
        var pattern = @"add\s+index\s+property\s+[""]?([^""\s]+)[""]?";
        var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);
        
        if (match.Success)
        {
            var propertyName = match.Groups[1].Value.Trim();
            parsedQuery.Properties["property_name"] = propertyName;
        }
        else
        {
            throw new ArgumentException($"Format de commande d'ajout d'index non reconnu : {query}");
        }
    }

        /// <summary>
    /// Parse les commandes de suppression de propriété de l'index
    /// Exemples :
    /// - remove index property "city"
    /// - remove index property city
    /// </summary>
    private void ParseRemoveIndexProperty(string query, ParsedQuery parsedQuery)
    {
        var pattern = @"remove\s+index\s+property\s+[""]?([^""\s]+)[""]?";
        var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);

        if (match.Success)
        {
            var propertyName = match.Groups[1].Value.Trim();
            parsedQuery.Properties["property_name"] = propertyName;
        }
        else
        {
            throw new ArgumentException($"Format de commande de suppression d'index non reconnu : {query}");
        }
    }

    /// <summary>
    /// Parse les commandes d'optimisation de graphes intelligente
    /// Exemples :
    /// - optimize path from Alice to Bob
    /// - dijkstra from Alice to Bob with weight distance
    /// - astar from Alice to Bob
    /// - floyd warshall
    /// - find components
    /// - detect cycles
    /// - calculate diameter
    /// - show performance metrics
    /// </summary>
    private void ParseGraphOptimization(string query, ParsedQuery parsedQuery)
    {
        // Patterns pour les différentes optimisations
        var patterns = new Dictionary<string, (string pattern, string algorithm)>
        {
            // Commandes calculate en premier (plus spécifiques)
            { "calculate_diameter", (@"calculate\s+diameter", "graph_diameter") },
            { "calculate_radius", (@"calculate\s+radius", "graph_radius") },
            { "calculate_centrality", (@"calculate\s+centrality", "closeness_centrality") },
            { "calculate", (@"calculate\s+(diameter|radius|centrality)", "graph_analysis") },
            
            // Autres commandes d'optimisation
            { "dijkstra", (@"dijkstra\s+(?:from\s+)?(\w+)\s+(?:to\s+)?(\w+)(?:\s+with\s+weight\s+(\w+))?", "dijkstra") },
            { "astar", (@"astar\s+(?:from\s+)?(\w+)\s+(?:to\s+)?(\w+)(?:\s+with\s+weight\s+(\w+))?", "astar") },
            { "floyd", (@"floyd\s+warshall", "floyd_warshall") },
            { "components", (@"(?:find\s+)?(?:connected\s+)?components", "connected_components") },
            { "cycles", (@"(?:find\s+)?(?:detect\s+)?cycles", "cycle_detection") },
            { "diameter", (@"(?:calculate\s+)?(?:graph\s+)?diameter", "graph_diameter") },
            { "radius", (@"(?:calculate\s+)?(?:graph\s+)?radius", "graph_radius") },
            { "centrality", (@"(?:calculate\s+)?(?:closeness\s+)?centrality", "closeness_centrality") },
            { "bridges", (@"(?:find\s+)?bridges", "bridges") },
            { "articulation", (@"(?:find\s+)?(?:articulation\s+)?points", "articulation_points") },
            { "performance", (@"(?:show\s+)?performance\s+(?:metrics)?", "performance_metrics") },
            { "optimize", (@"optimize\s+(?:path\s+)?(?:from\s+)?(\w+)\s+(?:to\s+)?(\w+)(?:\s+with\s+algorithm\s+(\w+))?(?:\s+with\s+weight\s+(\w+))?", "intelligent_optimization") },
            { "detect", (@"detect\s+cycles", "cycle_detection") }
        };

        foreach (var (key, (pattern, algorithm)) in patterns)
        {
            var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                parsedQuery.Properties["algorithm"] = algorithm;
                parsedQuery.Properties["original_query"] = query; // Capturer la requête originale
                
                if (match.Groups.Count > 1 && !string.IsNullOrEmpty(match.Groups[1].Value))
                {
                    parsedQuery.FromNode = match.Groups[1].Value;
                }
                
                if (match.Groups.Count > 2 && !string.IsNullOrEmpty(match.Groups[2].Value))
                {
                    parsedQuery.ToNode = match.Groups[2].Value;
                }
                
                if (match.Groups.Count > 3 && !string.IsNullOrEmpty(match.Groups[3].Value))
                {
                    parsedQuery.Properties["algorithm_name"] = match.Groups[3].Value;
                }
                
                if (match.Groups.Count > 4 && !string.IsNullOrEmpty(match.Groups[4].Value))
                {
                    parsedQuery.Properties["weight_property"] = match.Groups[4].Value;
                }
                
                return;
            }
        }

        // Pattern générique pour l'optimisation intelligente
        var genericMatch = Regex.Match(query, @"optimize\s+(.+)", RegexOptions.IgnoreCase);
        if (genericMatch.Success)
        {
            parsedQuery.Properties["algorithm"] = "intelligent_optimization";
            parsedQuery.Properties["optimization_query"] = genericMatch.Groups[1].Value.Trim();
        }
    }
}


