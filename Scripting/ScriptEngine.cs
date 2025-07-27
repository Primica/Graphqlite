using GraphQLite.Engine;
using System.Text;
using System.Text.RegularExpressions;

namespace GraphQLite.Scripting;

/// <summary>
/// Gestionnaire d'exécution de scripts GraphQLite (.gqls)
/// </summary>
public class ScriptEngine
{
    private readonly GraphQLiteEngine _engine;
    private readonly Dictionary<string, string> _variables;

    public ScriptEngine(GraphQLiteEngine engine)
    {
        _engine = engine;
        _variables = new Dictionary<string, string>();
    }

    /// <summary>
    /// Exécute un script depuis un fichier .gqls
    /// </summary>
    public async Task<ScriptResult> ExecuteScriptAsync(string scriptPath)
    {
        if (!File.Exists(scriptPath))
        {
            return new ScriptResult
            {
                Success = false,
                Error = $"Fichier script introuvable : {scriptPath}"
            };
        }

        try
        {
            var content = await File.ReadAllTextAsync(scriptPath);
            var queries = ParseScript(content);
            
            var results = new List<QueryExecutionResult>();
            int successCount = 0;
            int errorCount = 0;

            Console.WriteLine($"Exécution du script : {Path.GetFileName(scriptPath)}");
            Console.WriteLine($"Nombre de requêtes : {queries.Count}");
            Console.WriteLine($"Début : {DateTime.Now:HH:mm:ss}");
            Console.WriteLine();

            for (int i = 0; i < queries.Count; i++)
            {
                var query = queries[i];
                
                if (string.IsNullOrWhiteSpace(query.Content))
                    continue;

                // Support des commandes echo pour l'affichage
                if (query.Content.Trim().StartsWith("echo "))
                {
                    var message = query.Content.Trim().Substring(5).Trim('"');
                    message = ReplaceVariables(message);
                    Console.WriteLine($"[{i + 1}/{queries.Count}] {query.Content}");
                    Console.WriteLine($"  -> {message}");
                    Console.WriteLine();
                    
                    results.Add(new QueryExecutionResult
                    {
                        Query = query.Content,
                        Success = true,
                        Message = message
                    });
                    successCount++;
                    continue;
                }

                // Support des variables avec syntaxe @var = value
                if (query.Content.Trim().StartsWith("@"))
                {
                    var variableMatch = Regex.Match(query.Content.Trim(), @"^@(\w+)\s*=\s*(.+)$");
                    if (variableMatch.Success)
                    {
                        var varName = variableMatch.Groups[1].Value;
                        var varValue = variableMatch.Groups[2].Value.Trim('"', ' ');
                        _variables[varName] = varValue;
                        
                        Console.WriteLine($"[{i + 1}/{queries.Count}] {query.Content}");
                        Console.WriteLine($"  -> Variable définie : {varName} = {varValue}");
                        Console.WriteLine();
                        
                        results.Add(new QueryExecutionResult
                        {
                            Query = query.Content,
                            Success = true,
                            Message = $"Variable définie : {varName} = {varValue}"
                        });
                        successCount++;
                        continue;
                    }
                }

                // Support des commentaires
                if (query.Content.Trim().StartsWith("//") || query.Content.Trim().StartsWith("#"))
                {
                    Console.WriteLine($"[{i + 1}/{queries.Count}] {query.Content}");
                    Console.WriteLine();
                    continue;
                }

                // Remplacer les variables dans la requête
                var processedQuery = ReplaceVariables(query.Content).Replace("\n", " ").Trim();
                Console.WriteLine($"[{i + 1}/{queries.Count}] {query.Content}");

                try
                {
                    var result = await _engine.ExecuteQueryAsync(processedQuery);
                    
                    var executionResult = new QueryExecutionResult
                    {
                        QueryNumber = i + 1,
                        Query = query.Content,
                        Success = result.Success,
                        Message = result.Message,
                        Error = result.Error,
                        LineNumber = query.LineNumber
                    };

                    results.Add(executionResult);

                    if (result.Success)
                    {
                        Console.WriteLine($"  -> {result.Message}");
                        successCount++;
                    }
                    else
                    {
                        Console.WriteLine($"  -> Erreur : {result.Error}");
                        errorCount++;
                    }
                }
                catch (Exception ex)
                {
                    var executionResult = new QueryExecutionResult
                    {
                        QueryNumber = i + 1,
                        Query = query.Content,
                        Success = false,
                        Error = ex.Message,
                        LineNumber = query.LineNumber
                    };

                    results.Add(executionResult);
                    Console.WriteLine($"  -> Exception : {ex.Message}");
                    errorCount++;
                }

                Console.WriteLine();
            }

            var endTime = DateTime.Now;
            Console.WriteLine($"Résumé du script :");
            Console.WriteLine($"  Succès : {successCount}");
            Console.WriteLine($"  Erreurs : {errorCount}");
            Console.WriteLine($"  Taux de réussite : {(queries.Count > 0 ? (successCount * 100.0 / queries.Count):0):F1}%");
            Console.WriteLine($"  Durée : {endTime:HH:mm:ss}");

            return new ScriptResult
            {
                Success = errorCount == 0,
                TotalQueries = queries.Count,
                SuccessCount = successCount,
                ErrorCount = errorCount,
                Results = results,
                Message = $"Script exécuté : {successCount}/{queries.Count} requêtes réussies"
            };
        }
        catch (Exception ex)
        {
            return new ScriptResult
            {
                Success = false,
                Error = $"Erreur lors de l'exécution du script : {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Remplace les variables dans une chaîne de caractères
    /// </summary>
    private string ReplaceVariables(string input)
    {
        var result = input;
        foreach (var variable in _variables)
        {
            // Remplacer les variables dans les guillemets et en dehors
            result = result.Replace($"\"@{variable.Key}\"", $"\"{variable.Value}\"");
            result = result.Replace($"@{variable.Key}", variable.Value);
        }
        return result;
    }

    /// <summary>
    /// Parse un script en séparant les requêtes multi-lignes avec une gestion améliorée
    /// </summary>
    private List<ParsedScriptQuery> ParseScript(string content)
    {
        var queries = new List<ParsedScriptQuery>();
        var lines = content.Split('\n');
        var currentQuery = new StringBuilder();
        int startLine = 1;
        int currentLineNumber = 1;
        bool inMultiLineComment = false;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();
            currentLineNumber++;

            // Gestion des commentaires multi-lignes /* ... */
            if (trimmedLine.Contains("/*"))
            {
                inMultiLineComment = true;
                var beforeComment = trimmedLine.Substring(0, trimmedLine.IndexOf("/*"));
                if (!string.IsNullOrWhiteSpace(beforeComment))
                {
                    currentQuery.AppendLine(beforeComment);
                }
                continue;
            }

            if (trimmedLine.Contains("*/"))
            {
                inMultiLineComment = false;
                var afterComment = trimmedLine.Substring(trimmedLine.IndexOf("*/") + 2);
                if (!string.IsNullOrWhiteSpace(afterComment))
                {
                    currentQuery.AppendLine(afterComment);
                }
                continue;
            }

            if (inMultiLineComment)
            {
                continue;
            }

            // Ignorer les commentaires simples et lignes vides
            if (string.IsNullOrWhiteSpace(trimmedLine) || 
                trimmedLine.StartsWith("//") || 
                trimmedLine.StartsWith("#"))
            {
                continue;
            }

            // Si la ligne se termine par ';', c'est la fin d'une requête
            if (trimmedLine.EndsWith(";"))
            {
                currentQuery.AppendLine(trimmedLine[..^1]); // Enlever le ';'
                
                var queryContent = currentQuery.ToString().Trim();
                if (!string.IsNullOrWhiteSpace(queryContent))
                {
                    queries.Add(new ParsedScriptQuery
                    {
                        Content = queryContent,
                        LineNumber = startLine
                    });
                }

                currentQuery.Clear();
                startLine = currentLineNumber;
            }
            else
            {
                // Continuer à construire la requête multi-ligne
                currentQuery.AppendLine(trimmedLine);
            }
        }

        // Ajouter la dernière requête si elle n'est pas terminée par ';'
        var finalQuery = currentQuery.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(finalQuery))
        {
            queries.Add(new ParsedScriptQuery
            {
                Content = finalQuery,
                LineNumber = startLine
            });
        }

        return queries;
    }

    /// <summary>
    /// Valide la syntaxe d'un script avant exécution
    /// </summary>
    public ScriptValidationResult ValidateScript(string scriptPath)
    {
        if (!File.Exists(scriptPath))
        {
            return new ScriptValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Fichier script introuvable : {scriptPath}" }
            };
        }

        try
        {
            var content = File.ReadAllText(scriptPath);
            var queries = ParseScript(content);
            var errors = new List<string>();

            for (int i = 0; i < queries.Count; i++)
            {
                var query = queries[i];
                
                if (string.IsNullOrWhiteSpace(query.Content))
                    continue;

                // Validation basique de la syntaxe
                if (query.Content.Trim().StartsWith("echo "))
                    continue;

                if (query.Content.Trim().StartsWith("//") || query.Content.Trim().StartsWith("#"))
                    continue;

                // Support des variables
                if (query.Content.Trim().StartsWith("@"))
                {
                    var variableMatch = Regex.Match(query.Content.Trim(), @"^@(\w+)\s*=\s*(.+)$");
                    if (!variableMatch.Success)
                    {
                        errors.Add($"Ligne {query.LineNumber} : Syntaxe de variable invalide");
                    }
                    continue;
                }

                // Ignorer les lignes qui commencent par * (commentaires multi-lignes)
                if (query.Content.Trim().StartsWith("*"))
                {
                    continue;
                }

                // Vérifier que les commandes commencent par des mots-clés valides
                var firstWord = query.Content.Trim().Split(' ')[0].ToLowerInvariant();
                var validCommands = new[] { 
                    "create", "connect", "find", "count", "update", "delete", "show", 
                    "avg", "sum", "min", "max", "aggregate", "batch", "bulk", "import", 
                    "select", "subquery", "let", "set", "var", "define", "add", "get", 
                    "search", "link", "relate", "modify", "remove", "path", "describe", 
                    "schema" 
                };
                
                if (!validCommands.Contains(firstWord))
                {
                    errors.Add($"Ligne {query.LineNumber} : Commande invalide '{firstWord}'");
                }
            }

            return new ScriptValidationResult
            {
                IsValid = errors.Count == 0,
                Errors = errors,
                QueryCount = queries.Count
            };
        }
        catch (Exception ex)
        {
            return new ScriptValidationResult
            {
                IsValid = false,
                Errors = new List<string> { $"Erreur de validation : {ex.Message}" }
            };
        }
    }
}

/// <summary>
/// Requête parsée depuis un script
/// </summary>
public class ParsedScriptQuery
{
    public string Content { get; set; } = string.Empty;
    public int LineNumber { get; set; }
}

/// <summary>
/// Résultat d'exécution d'une requête dans un script
/// </summary>
public class QueryExecutionResult
{
    public int QueryNumber { get; set; }
    public string Query { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
    public int LineNumber { get; set; }
}

/// <summary>
/// Résultat d'exécution d'un script complet
/// </summary>
public class ScriptResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
    public int TotalQueries { get; set; }
    public int SuccessCount { get; set; }
    public int ErrorCount { get; set; }
    public List<QueryExecutionResult> Results { get; set; } = new();
}

/// <summary>
/// Résultat de validation d'un script
/// </summary>
public class ScriptValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public int QueryCount { get; set; }
}
