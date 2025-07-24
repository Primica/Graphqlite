using GraphQLite.Engine;
using System.Text;

namespace GraphQLite.Scripting;

/// <summary>
/// Gestionnaire d'exécution de scripts GraphQLite (.gqls)
/// </summary>
public class ScriptEngine
{
    private readonly GraphQLiteEngine _engine;

    public ScriptEngine(GraphQLiteEngine engine)
    {
        _engine = engine;
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
            Console.WriteLine();

            for (int i = 0; i < queries.Count; i++)
            {
                var query = queries[i];
                
                if (string.IsNullOrWhiteSpace(query.Content))
                    continue;

                Console.WriteLine($"[{i + 1}/{queries.Count}] {query.Content.Trim()}");

                try
                {
                    var result = await _engine.ExecuteQueryAsync(query.Content);
                    
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

            Console.WriteLine($"Script terminé : {successCount} succès, {errorCount} erreurs");

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
    /// Parse un script en séparant les requêtes multi-lignes
    /// </summary>
    private List<ParsedScriptQuery> ParseScript(string content)
    {
        var queries = new List<ParsedScriptQuery>();
        var lines = content.Split('\n');
        var currentQuery = new StringBuilder();
        int startLine = 1;
        int currentLineNumber = 1;

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Ignorer les commentaires et lignes vides
            if (string.IsNullOrWhiteSpace(trimmedLine) || trimmedLine.StartsWith("//") || trimmedLine.StartsWith("#"))
            {
                currentLineNumber++;
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
                startLine = currentLineNumber + 1;
            }
            else
            {
                // Continuer à construire la requête multi-ligne
                currentQuery.AppendLine(trimmedLine);
            }

            currentLineNumber++;
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
