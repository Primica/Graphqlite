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
        { "adjacent", QueryType.FindWithinSteps }
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
            // Pattern 2 : "create [label] ""name"" [properties]" (AVANT Pattern 1)
            @"create\s+(\w+)\s+""([^""]+)""(?:\s+(.+))?",
            // Pattern 3 : "create [label] [name] [properties]"
            @"create\s+(\w+)\s+([^\s]+(?:\s+[^\s]+)*)(?:\s+(.+))?"
            // Pattern 1 temporairement désactivé : "create [label] with [properties]"
            // @"create\s+(\w+)\s+with\s+(?!"")(.+)",
        };

        for (int patternIndex = 0; patternIndex < patterns.Length; patternIndex++)
        {
            var pattern = patterns[patternIndex];
            var match = Regex.Match(query, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                parsedQuery.NodeLabel = match.Groups[1].Value;
                
                if (patternIndex == 0) // Pattern 2 : avec guillemets
                {
                    parsedQuery.Properties["name"] = match.Groups[2].Value;
                    
                    var propertiesText = match.Groups.Count > 3 ? match.Groups[3].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(propertiesText))
                    {
                        ParseDynamicProperties(propertiesText, parsedQuery.Properties);
                    }
                }
                else if (patternIndex == 1) // Pattern 3 : sans guillemets
                {
                    parsedQuery.Properties["name"] = match.Groups[2].Value;
                    
                    var propertiesText = match.Groups.Count > 3 ? match.Groups[3].Value.Trim() : "";
                    if (!string.IsNullOrEmpty(propertiesText))
                    {
                        ParseDynamicProperties(propertiesText, parsedQuery.Properties);
                    }
                }
                else // Pattern 1 : avec "with"
                {
                    var propertiesText = match.Groups[2].Value.Trim();
                    ParseDynamicProperties(propertiesText, parsedQuery.Properties);
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

        // Nettoyer les conditions
        conditionsText = conditionsText.Trim();
        
        // Patterns pour les conditions complexes avec sous-requêtes
        var complexPatterns = new[]
        {
            // Pattern 1 : "connected to via [edge_type]"
            @"connected\s+to\s+via\s+(\w+)",
            // Pattern 2 : "connected to [label] via [edge_type]"
            @"connected\s+to\s+(\w+)\s+via\s+(\w+)",
            // Pattern 3 : "connected to [label] ""name"" via [edge_type]"
            @"connected\s+to\s+(\w+)\s+""([^""]+)""\s+via\s+(\w+)",
            // Pattern 4 : "connected to via [edge_type] where [conditions]"
            @"connected\s+to\s+via\s+(\w+)\s+where\s+(.+)",
            // Pattern 5 : "connected to [label] via [edge_type] where [conditions]"
            @"connected\s+to\s+(\w+)\s+via\s+(\w+)\s+where\s+(.+)",
            // Pattern 6 : "connected to [label] ""name"" via [edge_type] where [conditions]"
            @"connected\s+to\s+(\w+)\s+""([^""]+)""\s+via\s+(\w+)\s+where\s+(.+)",
            // Pattern 7 : "has edge [edge_type] to [label]"
            @"has\s+edge\s+(\w+)\s+to\s+(\w+)",
            // Pattern 8 : "has edge [edge_type] to [label] ""name""
            @"has\s+edge\s+(\w+)\s+to\s+(\w+)\s+""([^""]+)""",
            // Pattern 9 : "has edge [edge_type] to [label] where [conditions]"
            @"has\s+edge\s+(\w+)\s+to\s+(\w+)\s+where\s+(.+)",
            // Pattern 10 : "has edge [edge_type] to [label] ""name"" where [conditions]"
            @"has\s+edge\s+(\w+)\s+to\s+(\w+)\s+""([^""]+)""\s+where\s+(.+)",
            // Pattern 11 : "connected via [edge_type]"
            @"connected\s+via\s+(\w+)",
            // Pattern 12 : "connected via [edge_type] to [label]"
            @"connected\s+via\s+(\w+)\s+to\s+(\w+)",
            // Pattern 13 : "connected via [edge_type] to [label] ""name""
            @"connected\s+via\s+(\w+)\s+to\s+(\w+)\s+""([^""]+)""",
            // Pattern 14 : "connected via [edge_type] where [conditions]"
            @"connected\s+via\s+(\w+)\s+where\s+(.+)",
            // Pattern 15 : "connected via [edge_type] to [label] where [conditions]"
            @"connected\s+via\s+(\w+)\s+to\s+(\w+)\s+where\s+(.+)",
            // Pattern 16 : "connected via [edge_type] to [label] ""name"" where [conditions]"
            @"connected\s+via\s+(\w+)\s+to\s+(\w+)\s+""([^""]+)""\s+where\s+(.+)"
        };

        // Essayer d'abord les patterns complexes
        foreach (var pattern in complexPatterns)
        {
            var match = Regex.Match(conditionsText, pattern, RegexOptions.IgnoreCase);
            if (match.Success)
            {
                if (pattern.Contains("connected\\s+to\\s+via") && pattern.Contains("where"))
                {
                    // Pattern 4 : "connected to via [edge_type] where [conditions]"
                    var edgeType = match.Groups[1].Value.Trim();
                    var subConditions = match.Groups[2].Value.Trim();
                    
                    var subQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        EdgeType = edgeType,
                        Conditions = new Dictionary<string, object>()
                    };
                    
                    ParseConditions(subConditions, subQuery.Conditions);
                    conditions["connected_to_via"] = subQuery;
                    return;
                }
                else if (pattern.Contains("connected\\s+to\\s+\\w+\\s+via") && pattern.Contains("where"))
                {
                    // Pattern 5 : "connected to [label] via [edge_type] where [conditions]"
                    var targetLabel = match.Groups[1].Value.Trim();
                    var edgeType = match.Groups[2].Value.Trim();
                    var subConditions = match.Groups[3].Value.Trim();
                    
                    var subQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        NodeLabel = targetLabel,
                        EdgeType = edgeType,
                        Conditions = new Dictionary<string, object>()
                    };
                    
                    ParseConditions(subConditions, subQuery.Conditions);
                    conditions["connected_to_via"] = subQuery;
                    return;
                }
                else if (pattern.Contains("connected\\s+to\\s+\\w+\\s+\"") && pattern.Contains("where"))
                {
                    // Pattern 6 : "connected to [label] ""name"" via [edge_type] where [conditions]"
                    var targetLabel = match.Groups[1].Value.Trim();
                    var targetName = match.Groups[2].Value.Trim();
                    var edgeType = match.Groups[3].Value.Trim();
                    var subConditions = match.Groups[4].Value.Trim();
                    
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
                    return;
                }
                else if (pattern.Contains("has\\s+edge") && pattern.Contains("where"))
                {
                    // Pattern 9/10 : "has edge [edge_type] to [label] where [conditions]"
                    var edgeType = match.Groups[1].Value.Trim();
                    var targetLabel = match.Groups[2].Value.Trim();
                    var subConditions = match.Groups[3].Value.Trim();
                    
                    var subQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        NodeLabel = targetLabel,
                        EdgeType = edgeType,
                        Conditions = new Dictionary<string, object>()
                    };
                    
                    ParseConditions(subConditions, subQuery.Conditions);
                    conditions["has_edge"] = subQuery;
                    return;
                }
                else if (pattern.Contains("connected\\s+via") && pattern.Contains("where"))
                {
                    // Pattern 14/15/16 : "connected via [edge_type] where [conditions]"
                    var edgeType = match.Groups[1].Value.Trim();
                    var subConditions = match.Groups[2].Value.Trim();
                    
                    var subQuery = new ParsedQuery
                    {
                        Type = QueryType.FindNodes,
                        EdgeType = edgeType,
                        Conditions = new Dictionary<string, object>()
                    };
                    
                    ParseConditions(subConditions, subQuery.Conditions);
                    conditions["connected_via"] = subQuery;
                    return;
                }
                else if (pattern.Contains("connected\\s+to\\s+via"))
                {
                    // Pattern 1 : "connected to via [edge_type]"
                    var edgeType = match.Groups[1].Value.Trim();
                    conditions["connected_to_via"] = edgeType;
                    return;
                }
                else if (pattern.Contains("connected\\s+to\\s+\\w+\\s+via"))
                {
                    // Pattern 2 : "connected to [label] via [edge_type]"
                    var targetLabel = match.Groups[1].Value.Trim();
                    var edgeType = match.Groups[2].Value.Trim();
                    conditions["connected_to_via"] = new { Label = targetLabel, EdgeType = edgeType };
                    return;
                }
                else if (pattern.Contains("connected\\s+to\\s+\\w+\\s+\""))
                {
                    // Pattern 3 : "connected to [label] ""name"" via [edge_type]"
                    var targetLabel = match.Groups[1].Value.Trim();
                    var targetName = match.Groups[2].Value.Trim();
                    var edgeType = match.Groups[3].Value.Trim();
                    conditions["connected_to_via"] = new { Label = targetLabel, Name = targetName, EdgeType = edgeType };
                    return;
                }
                else if (pattern.Contains("has\\s+edge"))
                {
                    // Pattern 7/8 : "has edge [edge_type] to [label]"
                    var edgeType = match.Groups[1].Value.Trim();
                    var targetLabel = match.Groups[2].Value.Trim();
                    conditions["has_edge"] = new { EdgeType = edgeType, TargetLabel = targetLabel };
                    return;
                }
                else if (pattern.Contains("connected\\s+via"))
                {
                    // Pattern 11/12/13 : "connected via [edge_type]"
                    var edgeType = match.Groups[1].Value.Trim();
                    conditions["connected_via"] = edgeType;
                    return;
                }
            }
        }

        // Si aucun pattern complexe n'est trouvé, traiter comme des conditions normales
        ParseConditions(conditionsText, conditions);
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
            // Nettoyer la requête
            query = query.Trim();
            
            // Patterns pour sous-requêtes avec support amélioré des agrégations
            var patterns = new[]
            {
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
                                if (pattern.Contains("property") || pattern.Contains("from") || (pattern.Contains("select") && pattern.Contains("(sum|avg|min|max|count)")))
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
                        
                        if (match.Groups.Count > 3 && !string.IsNullOrEmpty(match.Groups[3].Value))
                        {
                            ParseConditions(match.Groups[3].Value, parsedQuery.Conditions);
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
        // Essayer d'abord l'approche manuelle qui fonctionne mieux pour les cas complexes
        ParsePropertiesManual(propertiesText, properties);
        
        // Si l'approche manuelle n'a rien trouvé, essayer les autres approches
        if (properties.Count == 0)
        {
            // Pattern amélioré pour supporter les propriétés multiples avec valeurs complexes
            // Gère : property1 value1 property2 "value with spaces" property3 value3
            var matches = Regex.Matches(propertiesText, @"(\w+)\s+([^\s](?:[^a]|a(?!nd\s))*?)(?:\s+and\s|$)", RegexOptions.IgnoreCase);
            
            // Si le pattern complexe échoue, essayer le pattern simple pour les propriétés multiples
            if (matches.Count == 0)
            {
                // Pattern amélioré pour les propriétés multiples : "property1 value1 property2 value2"
                // Ce pattern capture correctement les valeurs avec espaces
                var propertyMatches = Regex.Matches(propertiesText, @"(\w+)\s+([^\s]+(?:\s+[^\s]+)*?)(?=\s+\w+\s|$)");
                
                // Si ce pattern ne fonctionne pas, essayer une approche plus robuste
                if (propertyMatches.Count == 0)
                {
                    // Approche par parsing manuel pour les cas complexes
                    ParsePropertiesManually(propertiesText, properties);
                }
                else
                {
                    foreach (Match match in propertyMatches)
                    {
                        var key = match.Groups[1].Value;
                        var value = ParseDynamicValue(match.Groups[2].Value.Trim());
                        properties[key] = value;
                    }
                }
            }
            else
            {
                foreach (Match match in matches)
                {
                    var key = match.Groups[1].Value;
                    var value = ParseDynamicValue(match.Groups[2].Value.Trim());
                    properties[key] = value;
                }
            }
            
            // Si aucun match, essayer une approche alternative pour les cas complexes
            if (properties.Count == 0)
            {
                ParseDynamicPropertiesAlternative(propertiesText, properties);
            }
            
            // Si toujours aucun résultat, essayer l'approche robuste
            if (properties.Count == 0)
            {
                ParsePropertiesRobust(propertiesText, properties);
            }
        }
    }

    /// <summary>
    /// Parse les propriétés manuellement pour les cas complexes
    /// </summary>
    private void ParsePropertiesManually(string propertiesText, Dictionary<string, object> properties)
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
}

