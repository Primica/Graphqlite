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
                if (_variables.TryGetValue(varName, out var value))
                {
                    return value?.ToString() ?? "";
                }
                return match.Value; // Garde la référence si la variable n'existe pas
            });
        }
        
        return result;
    }
    
    /// <summary>
    /// Remplace les variables dans un dictionnaire de propriétés
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
            
            result[key] = value;
        }
        
        return result;
    }
    
    /// <summary>
    /// Remplace les variables dans un dictionnaire de conditions
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
            
            result[key] = value;
        }
        
        return result;
    }
} 