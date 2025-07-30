namespace GraphQLite.Query;

/// <summary>
/// Gestionnaire de variables pour les requêtes GraphQLite
/// Permet de définir et utiliser des variables dans les requêtes
/// </summary>
public class VariableManager
{
    private readonly Dictionary<string, object> _variables = new();
    
    /// <summary>
    /// Définit une variable avec sa valeur
    /// </summary>
    public void DefineVariable(string name, object value)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Le nom de la variable ne peut pas être vide", nameof(name));
            
        _variables[name] = value;
    }
    
    /// <summary>
    /// Récupère la valeur d'une variable
    /// </summary>
    public object? GetVariable(string name)
    {
        return _variables.TryGetValue(name, out var value) ? value : null;
    }
    
    /// <summary>
    /// Vérifie si une variable existe
    /// </summary>
    public bool HasVariable(string name)
    {
        return _variables.ContainsKey(name);
    }
    
    /// <summary>
    /// Supprime une variable
    /// </summary>
    public void RemoveVariable(string name)
    {
        _variables.Remove(name);
    }
    
    /// <summary>
    /// Supprime toutes les variables
    /// </summary>
    public void ClearVariables()
    {
        _variables.Clear();
    }
    
    /// <summary>
    /// Récupère toutes les variables définies
    /// </summary>
    public Dictionary<string, object> GetAllVariables()
    {
        return new Dictionary<string, object>(_variables);
    }
    
    /// <summary>
    /// Remplace les références de variables dans une chaîne par leurs valeurs
    /// </summary>
    public string ReplaceVariables(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;
            
        var result = text;
        
        // Recherche des patterns comme $variable ou ${variable}
        var patterns = new[]
        {
            @"\$([a-zA-Z_][a-zA-Z0-9_]*)", // $variable
            @"\$\{([a-zA-Z_][a-zA-Z0-9_]*)\}" // ${variable}
        };
        
        foreach (var pattern in patterns)
        {
            result = System.Text.RegularExpressions.Regex.Replace(result, pattern, match =>
            {
                var varName = match.Groups[1].Value;
                
                // Rechercher la variable de manière insensible à la casse
                // Essayer d'abord avec le nom exact, puis avec différentes variations
                var foundVariable = _variables.FirstOrDefault(kvp => 
                    string.Equals(kvp.Key, varName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kvp.Key, "$" + varName, StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(kvp.Key.TrimStart('$'), varName, StringComparison.OrdinalIgnoreCase));
                
                if (foundVariable.Key != null)
                {
                    var value = foundVariable.Value?.ToString() ?? "";
                    
                    // Gestion spéciale pour les variables dans des contextes complexes
                    if (IsComplexContext(text))
                    {
                        value = ProcessComplexContextValue(value, text);
                    }
                    else
                    {
                        // Supprimer les guillemets superflus pour les variables dans les conditions
                        if (value.StartsWith("\"") && value.EndsWith("\""))
                        {
                            value = value.Substring(1, value.Length - 2);
                        }
                        else if (value.StartsWith("'") && value.EndsWith("'"))
                        {
                            value = value.Substring(1, value.Length - 2);
                        }
                        
                        // Nettoyer les guillemets dans les conditions complexes
                        if (value.Contains("\"") && (value.Contains(" and ") || value.Contains(" or ") || value.Contains(" eq ") || value.Contains(" gt ") || value.Contains(" lt ")))
                        {
                            value = value.Replace("\"", "");
                        }
                        
                        // Nettoyer les "and" en trop dans les valeurs
                        if (value.Contains(" and ") && !value.Contains("=") && !value.Contains(">") && !value.Contains("<") && !value.Contains(" eq ") && !value.Contains(" gt ") && !value.Contains(" lt "))
                        {
                            value = value.Replace(" and ", "");
                        }
                        
                        // Nettoyer les espaces superflus
                        value = value.Trim();
                    }
                    
                    return value;
                }
                return match.Value; // Garde la référence si la variable n'existe pas
            });
        }
        
        return result;
    }
    
    /// <summary>
    /// Détermine si le contexte est complexe (relations, conditions avancées, etc.)
    /// </summary>
    private bool IsComplexContext(string text)
    {
        var complexKeywords = new[]
        {
            "connected to", "connected via", "has edge", "via", "avoiding",
            "with max steps", "bidirectional", "where", "and", "or",
            "eq", "gt", "lt", "contains", "starts with", "ends with"
        };
        
        return complexKeywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }
    
    /// <summary>
    /// Traite les valeurs de variables dans des contextes complexes
    /// </summary>
    private string ProcessComplexContextValue(string value, string context)
    {
        // Nettoyer la valeur de base
        value = value.Trim();
        
        // Supprimer les guillemets superflus
        if (value.StartsWith("\"") && value.EndsWith("\""))
        {
            value = value.Substring(1, value.Length - 2);
        }
        else if (value.StartsWith("'") && value.EndsWith("'"))
        {
            value = value.Substring(1, value.Length - 2);
        }
        
        // Gestion spéciale selon le contexte
        if (context.Contains("connected to", StringComparison.OrdinalIgnoreCase) ||
            context.Contains("has edge", StringComparison.OrdinalIgnoreCase))
        {
            // Pour les relations, garder la valeur telle quelle
            return value;
        }
        else if (context.Contains("where", StringComparison.OrdinalIgnoreCase))
        {
            // Pour les conditions WHERE, nettoyer les opérateurs logiques en trop
            if (value.Contains(" and ") && !value.Contains("=") && !value.Contains(">") && !value.Contains("<"))
            {
                value = value.Replace(" and ", " ");
            }
            if (value.Contains(" or ") && !value.Contains("=") && !value.Contains(">") && !value.Contains("<"))
            {
                value = value.Replace(" or ", " ");
            }
        }
        else if (context.Contains("via", StringComparison.OrdinalIgnoreCase) ||
                 context.Contains("avoiding", StringComparison.OrdinalIgnoreCase))
        {
            // Pour les types d'arêtes, garder la valeur simple
            return value.Trim();
        }
        
        return value;
    }
    
    /// <summary>
    /// Remplace les variables dans un dictionnaire de propriétés avec gestion avancée
    /// </summary>
    public Dictionary<string, object> ReplaceVariablesInProperties(Dictionary<string, object> properties)
    {
        var result = new Dictionary<string, object>();
        
        foreach (var kvp in properties)
        {
            var key = ReplaceVariables(kvp.Key);
            var value = kvp.Value;
            
            // Si la valeur est une chaîne, essayer de remplacer les variables
            if (value is string strValue)
            {
                value = ReplaceVariables(strValue);
            }
            else if (value is Dictionary<string, object> dictValue)
            {
                // Récursivement remplacer les variables dans les dictionnaires imbriqués
                value = ReplaceVariablesInProperties(dictValue);
            }
            else if (value is List<object> listValue)
            {
                // Remplacer les variables dans les listes
                var newList = new List<object>();
                foreach (var item in listValue)
                {
                    if (item is string strItem)
                    {
                        newList.Add(ReplaceVariables(strItem));
                    }
                    else if (item is Dictionary<string, object> dictItem)
                    {
                        newList.Add(ReplaceVariablesInProperties(dictItem));
                    }
                    else
                    {
                        newList.Add(item);
                    }
                }
                value = newList;
            }
            
            result[key] = value;
        }
        
        return result;
    }
    
    /// <summary>
    /// Remplace les variables dans un dictionnaire de conditions avec gestion avancée
    /// </summary>
    public Dictionary<string, object> ReplaceVariablesInConditions(Dictionary<string, object> conditions)
    {
        var result = new Dictionary<string, object>();
        
        foreach (var kvp in conditions)
        {
            var key = ReplaceVariables(kvp.Key);
            var value = kvp.Value;
            
            // Si la valeur est une chaîne, essayer de remplacer les variables
            if (value is string strValue)
            {
                value = ReplaceVariables(strValue);
            }
            else if (value is Dictionary<string, object> dictValue)
            {
                // Récursivement remplacer les variables dans les dictionnaires imbriqués
                value = ReplaceVariablesInConditions(dictValue);
            }
            else if (value is List<object> listValue)
            {
                // Remplacer les variables dans les listes
                var newList = new List<object>();
                foreach (var item in listValue)
                {
                    if (item is string strItem)
                    {
                        newList.Add(ReplaceVariables(strItem));
                    }
                    else if (item is Dictionary<string, object> dictItem)
                    {
                        newList.Add(ReplaceVariablesInConditions(dictItem));
                    }
                    else
                    {
                        newList.Add(item);
                    }
                }
                value = newList;
            }
            
            result[key] = value;
        }
        
        return result;
    }
    
    /// <summary>
    /// Remplace les variables dans une requête ParsedQuery
    /// </summary>
    public void ReplaceVariablesInParsedQuery(ParsedQuery query)
    {
        if (query == null) return;
        
        // Remplacer les variables dans les propriétés de base
        query.Properties = ReplaceVariablesInProperties(query.Properties);
        
        // Remplacer les variables dans les nœuds source et destination
        if (!string.IsNullOrEmpty(query.FromNode))
        {
            query.FromNode = ReplaceVariables(query.FromNode);
        }
        
        if (!string.IsNullOrEmpty(query.ToNode))
        {
            query.ToNode = ReplaceVariables(query.ToNode);
        }
        
        // Remplacer les variables dans le type d'arête
        if (!string.IsNullOrEmpty(query.EdgeType))
        {
            query.EdgeType = ReplaceVariables(query.EdgeType);
        }
        
        // Remplacer les variables dans le label de nœud
        if (!string.IsNullOrEmpty(query.NodeLabel))
        {
            query.NodeLabel = ReplaceVariables(query.NodeLabel);
        }
        
        // Remplacer les variables dans les conditions
        query.Conditions = ReplaceVariablesInConditions(query.Conditions);
        
        // Remplacer les variables dans les propriétés d'agrégation
        if (!string.IsNullOrEmpty(query.AggregateProperty))
        {
            query.AggregateProperty = ReplaceVariables(query.AggregateProperty);
        }
        
        // Remplacer les variables dans les sous-requêtes
        foreach (var subQuery in query.SubQueries)
        {
            ReplaceVariablesInParsedQuery(subQuery);
        }
        
        // Remplacer les variables dans les opérations batch
        if (query.BatchOperations != null)
        {
            foreach (var batchOp in query.BatchOperations)
            {
                ReplaceVariablesInParsedQuery(batchOp);
            }
        }
    }
} 