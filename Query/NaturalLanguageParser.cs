using System.Text.RegularExpressions;

namespace GraphQLite.Query;

/// <summary>
/// Parser pour le DSL GraphQLite en langage naturel
/// Exemples de requêtes supportées :
/// - "create person with name John and age 30"
/// - "find all users where age > 25"
/// - "connect user John to company Acme with relationship works_at"
/// - "find path from John to Mary"
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
        { "max", QueryType.Aggregate }
    };

    /// <summary>
    /// Parse une requête en langage naturel
    /// </summary>
    public ParsedQuery Parse(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw new ArgumentException("La requête ne peut pas être vide", nameof(query));

        query = query.Trim().ToLowerInvariant();
        var parsedQuery = new ParsedQuery();

        // Déterminer le type de requête
        parsedQuery.Type = DetermineQueryType(query);

        switch (parsedQuery.Type)
        {
            case QueryType.CreateNode:
                ParseCreateNode(query, parsedQuery);
                break;
            case QueryType.CreateEdge:
                ParseCreateEdge(query, parsedQuery);
                break;
            case QueryType.FindNodes:
                ParseFindNodes(query, parsedQuery);
                break;
            case QueryType.FindPath:
                ParseFindPath(query, parsedQuery);
                break;
            case QueryType.FindWithinSteps:
                ParseFindWithinSteps(query, parsedQuery);
                break;
            case QueryType.UpdateNode:
                ParseUpdateNode(query, parsedQuery);
                break;
            case QueryType.DeleteNode:
                ParseDeleteNode(query, parsedQuery);
                break;
            case QueryType.Count:
                ParseCount(query, parsedQuery);
                break;
            case QueryType.Aggregate:
                ParseAggregate(query, parsedQuery);
                break;
            case QueryType.ShowSchema:
                // Pour les commandes de schéma, aucune autre information n'est nécessaire
                break;
            default:
                throw new NotSupportedException($"Type de requête non supporté : {parsedQuery.Type}");
        }

        return parsedQuery;
    }

    private QueryType DetermineQueryType(string query)
    {
        foreach (var keyword in QueryKeywords)
        {
            if (query.StartsWith(keyword.Key))
            {
                // Cas spéciaux
                if (keyword.Key == "find" && query.Contains("path"))
                    return QueryType.FindPath;
                if (keyword.Key == "find" && query.Contains("from") && query.Contains("over") && query.Contains("steps"))
                    return QueryType.FindWithinSteps;
                
                return keyword.Value;
            }
        }

        throw new ArgumentException($"Type de requête non reconnu dans : {query}");
    }

    private void ParseCreateNode(string query, ParsedQuery parsedQuery)
    {
        // Pattern: "create [label] with [property1] [value1] and [property2] [value2]"
        var match = Regex.Match(query, @"create\s+(\w+)(?:\s+with\s+(.+))?");
        
        if (!match.Success)
            throw new ArgumentException("Format de création de nœud invalide");

        parsedQuery.NodeLabel = match.Groups[1].Value;

        if (match.Groups[2].Success)
        {
            ParseProperties(match.Groups[2].Value, parsedQuery.Properties);
        }
    }

    private void ParseCreateEdge(string query, ParsedQuery parsedQuery)
    {
        // Pattern: "connect [node1] to [node2] with relationship [type]"
        var match = Regex.Match(query, 
            @"(?:connect|link|relate)\s+(\w+)\s+to\s+(\w+)(?:\s+with\s+relationship\s+(\w+))?");
        
        if (!match.Success)
            throw new ArgumentException("Format de création d'arête invalide");

        parsedQuery.FromNode = match.Groups[1].Value;
        parsedQuery.ToNode = match.Groups[2].Value;
        parsedQuery.EdgeType = match.Groups[3].Success ? match.Groups[3].Value : "connected_to";
    }

    private void ParseFindNodes(string query, ParsedQuery parsedQuery)
    {
        // Pattern amélioré pour supporter les requêtes multi-lignes
        // Normaliser la requête en supprimant les retours à la ligne excessifs
        var normalizedQuery = Regex.Replace(query, @"\s+", " ").Trim();
        
        // Pattern étendu: "find [all] [label] [where conditions] [limit N] [offset M]"
        var match = Regex.Match(normalizedQuery, @"find\s+(?:all\s+)?(\w+)(?:\s+where\s+(.+?))?(?:\s+limit\s+(\d+))?(?:\s+offset\s+(\d+))?$");
        
        if (!match.Success)
            throw new ArgumentException("Format de recherche invalide");

        // Gérer le pluriel/singulier automatiquement (amélioration pour les pluriels complexes)
        var label = match.Groups[1].Value;
        
        // Gestion avancée des pluriels
        if (label.EndsWith("ies") && label.Length > 3)
        {
            // companies → company, industries → industry
            label = label[..^3] + "y";
        }
        else if (label.EndsWith("s") && label.Length > 1)
        {
            // persons → person, users → user
            label = label[..^1];
        }
        
        parsedQuery.NodeLabel = label;

        if (match.Groups[2].Success)
        {
            ParseConditions(match.Groups[2].Value, parsedQuery.Conditions);
        }

        if (match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out int limit))
        {
            parsedQuery.Limit = limit;
        }

        if (match.Groups[4].Success && int.TryParse(match.Groups[4].Value, out int offset))
        {
            parsedQuery.Offset = offset;
        }
    }

    private void ParseFindPath(string query, ParsedQuery parsedQuery)
    {
        // Pattern: "find path from [node1] to [node2]"
        var match = Regex.Match(query, @"find\s+path\s+from\s+(\w+)\s+to\s+(\w+)");
        
        if (!match.Success)
            throw new ArgumentException("Format de recherche de chemin invalide");

        parsedQuery.FromNode = match.Groups[1].Value;
        parsedQuery.ToNode = match.Groups[2].Value;
    }

    private void ParseFindWithinSteps(string query, ParsedQuery parsedQuery)
    {
        // Pattern: "find [label] from [node1] to [node2] over [steps] steps"
        var match = Regex.Match(query, @"find\s+(\w+)\s+from\s+(\w+)\s+to\s+(\w+)\s+over\s+(\d+)\s+steps?", RegexOptions.IgnoreCase);
        
        if (!match.Success)
        {
            // Pattern alternatif: "find [label] from [node1] over [steps] steps"  
            match = Regex.Match(query, @"find\s+(\w+)\s+from\s+(\w+)\s+over\s+(\d+)\s+steps?", RegexOptions.IgnoreCase);
            
            if (!match.Success)
                throw new ArgumentException("Format de recherche dans les étapes invalide");

            parsedQuery.NodeLabel = match.Groups[1].Value;
            parsedQuery.FromNode = match.Groups[2].Value;
            parsedQuery.MaxSteps = int.Parse(match.Groups[3].Value);
        }
        else
        {
            parsedQuery.NodeLabel = match.Groups[1].Value;
            parsedQuery.FromNode = match.Groups[2].Value;
            parsedQuery.ToNode = match.Groups[3].Value;
            parsedQuery.MaxSteps = int.Parse(match.Groups[4].Value);
        }
    }

    private void ParseUpdateNode(string query, ParsedQuery parsedQuery)
    {
        // Pattern: "update [label] set [properties] where [conditions]"
        var match = Regex.Match(query, @"update\s+(\w+)\s+set\s+(.+?)(?:\s+where\s+(.+))?$");
        
        if (!match.Success)
            throw new ArgumentException("Format de mise à jour invalide");

        parsedQuery.NodeLabel = match.Groups[1].Value;
        ParseProperties(match.Groups[2].Value, parsedQuery.Properties);

        if (match.Groups[3].Success)
        {
            ParseConditions(match.Groups[3].Value, parsedQuery.Conditions);
        }
    }

    private void ParseDeleteNode(string query, ParsedQuery parsedQuery)
    {
        // Pattern: "delete [label] where [conditions]"
        var match = Regex.Match(query, @"(?:delete|remove)\s+(\w+)(?:\s+where\s+(.+))?$");
        
        if (!match.Success)
            throw new ArgumentException("Format de suppression invalide");

        parsedQuery.NodeLabel = match.Groups[1].Value;

        if (match.Groups[2].Success)
        {
            ParseConditions(match.Groups[2].Value, parsedQuery.Conditions);
        }
    }

    private void ParseCount(string query, ParsedQuery parsedQuery)
    {
        // Pattern étendu: "count [label] [where conditions] [limit N] [offset M]"
        var match = Regex.Match(query, @"count\s+(\w+)(?:\s+where\s+(.+?))?(?:\s+limit\s+(\d+))?(?:\s+offset\s+(\d+))?$");
        
        if (!match.Success)
            throw new ArgumentException("Format de comptage invalide");

        // Gérer le pluriel/singulier automatiquement (amélioration pour les pluriels complexes)
        var label = match.Groups[1].Value;
        
        // Gestion avancée des pluriels
        if (label.EndsWith("ies") && label.Length > 3)
        {
            // companies → company, industries → industry
            label = label[..^3] + "y";
        }
        else if (label.EndsWith("s") && label.Length > 1)
        {
            // persons → person, users → user
            label = label[..^1];
        }
        
        parsedQuery.NodeLabel = label;

        if (match.Groups[2].Success)
        {
            ParseConditions(match.Groups[2].Value, parsedQuery.Conditions);
        }

        if (match.Groups[3].Success && int.TryParse(match.Groups[3].Value, out int limit))
        {
            parsedQuery.Limit = limit;
        }

        if (match.Groups[4].Success && int.TryParse(match.Groups[4].Value, out int offset))
        {
            parsedQuery.Offset = offset;
        }
    }

    private void ParseAggregate(string query, ParsedQuery parsedQuery)
    {
        // Pattern: "sum|avg|min|max [label] property [property_name] [where conditions]"
        var match = Regex.Match(query, @"(sum|avg|min|max)\s+(\w+)\s+property\s+(\w+)(?:\s+where\s+(.+))?$", RegexOptions.IgnoreCase);
        
        if (!match.Success)
            throw new ArgumentException("Format d'agrégation invalide. Utiliser: sum|avg|min|max [label] property [property_name] [where conditions]");

        // Déterminer la fonction d'agrégation
        parsedQuery.AggregateFunction = match.Groups[1].Value.ToLowerInvariant() switch
        {
            "sum" => AggregateFunction.Sum,
            "avg" => AggregateFunction.Avg,
            "min" => AggregateFunction.Min,
            "max" => AggregateFunction.Max,
            _ => throw new ArgumentException($"Fonction d'agrégation non supportée : {match.Groups[1].Value}")
        };

        // Gérer le pluriel/singulier automatiquement pour le label
        var label = match.Groups[2].Value;
        if (label.EndsWith("ies") && label.Length > 3)
        {
            // companies → company, industries → industry
            label = label[..^3] + "y";
        }
        else if (label.EndsWith("s") && label.Length > 1)
        {
            // persons → person, users → user
            label = label[..^1];
        }
        
        parsedQuery.NodeLabel = label;
        parsedQuery.AggregateProperty = match.Groups[3].Value;

        if (match.Groups[4].Success)
        {
            ParseConditions(match.Groups[4].Value, parsedQuery.Conditions);
        }
    }

    private void ParseProperties(string propertiesText, Dictionary<string, object> properties)
    {
        // Pattern: "property1 value1 and property2 value2"
        var matches = Regex.Matches(propertiesText, @"(\w+)\s+([^\s]+)(?:\s+and\s+|$)");
        
        foreach (Match match in matches)
        {
            var key = match.Groups[1].Value;
            var value = ParseValue(match.Groups[2].Value);
            properties[key] = value;
        }
    }

    private void ParseConditions(string conditionsText, Dictionary<string, object> conditions)
    {
        // Pattern amélioré pour supporter AND et OR
        // Ex: "age > 25 and name = John or status = active"
        var parts = SplitConditions(conditionsText);
        
        // CORRECTION FINALE : Détecter si la requête contient des OR
        var hasOrInQuery = parts.Any(p => p.LogicalOperator == LogicalOperator.Or);
        
        foreach (var part in parts)
        {
            var match = Regex.Match(part.Condition, @"(\w+)\s*([><=!]+)\s*([^\s]+)");
            
            if (match.Success)
            {
                var property = match.Groups[1].Value;
                var @operator = match.Groups[2].Value;
                var value = ParseValue(match.Groups[3].Value);
                
                // Normaliser les opérateurs
                var normalizedOperator = @operator switch
                {
                    ">" => "gt",
                    "<" => "lt", 
                    ">=" => "ge",
                    "<=" => "le",
                    "=" => "eq",
                    "!=" => "ne",
                    _ => "eq"
                };
                
                // CORRECTION MAJEURE : Traiter correctement TOUTES les conditions OR
                string conditionKey;
                if (hasOrInQuery)
                {
                    // Dans une requête contenant OR, traiter chaque condition selon sa logique :
                    // - Les conditions explicitement AND restent AND  
                    // - Toutes les autres deviennent OR (y compris la première)
                    if (part.LogicalOperator == LogicalOperator.And)
                    {
                        conditionKey = $"And_{property}_{normalizedOperator}";
                    }
                    else
                    {
                        // Toutes les conditions qui ne sont pas explicitement AND deviennent OR
                        // Cela inclut la première condition (LogicalOperator.None) et les conditions OR explicites
                        conditionKey = $"Or_{property}_{normalizedOperator}";
                    }
                }
                else
                {
                    // Requête purement AND ou condition simple
                    conditionKey = part.LogicalOperator == LogicalOperator.None 
                        ? $"{property}_{normalizedOperator}" 
                        : $"{part.LogicalOperator}_{property}_{normalizedOperator}";
                }
                
                conditions[conditionKey] = value;
            }
        }
    }

    /// <summary>
    /// Sépare les conditions avec leurs opérateurs logiques
    /// </summary>
    private List<ConditionPart> SplitConditions(string conditionsText)
    {
        var parts = new List<ConditionPart>();
        var segments = Regex.Split(conditionsText, @"\s+(and|or)\s+", RegexOptions.IgnoreCase);
        
        LogicalOperator currentOperator = LogicalOperator.None;
        
        for (int i = 0; i < segments.Length; i++)
        {
            if (i % 2 == 0) // Condition
            {
                parts.Add(new ConditionPart
                {
                    Condition = segments[i].Trim(),
                    LogicalOperator = currentOperator
                });
            }
            else // Opérateur logique
            {
                currentOperator = segments[i].ToLowerInvariant() switch
                {
                    "and" => LogicalOperator.And,
                    "or" => LogicalOperator.Or,
                    _ => LogicalOperator.None
                };
            }
        }
        
        return parts;
    }

    /// <summary>
    /// Partie d'une condition avec son opérateur logique
    /// </summary>
    private class ConditionPart
    {
        public string Condition { get; set; } = string.Empty;
        public LogicalOperator LogicalOperator { get; set; }
    }

    /// <summary>
    /// Opérateurs logiques supportés
    /// </summary>
    private enum LogicalOperator
    {
        None,
        And,
        Or
    }

    private object ParseValue(string value)
    {
        // Tenter de parser comme nombre
        if (int.TryParse(value, out int intValue))
            return intValue;
        
        if (double.TryParse(value, out double doubleValue))
            return doubleValue;
        
        // Tenter de parser comme booléen
        if (bool.TryParse(value, out bool boolValue))
            return boolValue;
        
        // Retourner comme string (enlever les guillemets si présents)
        return value.Trim('"', '\'');
    }
}
