using System.CommandLine;
using GraphQLite.Engine;

namespace GraphQLite;

class Program
{
    static async Task<int> Main(string[] args)
    {
        var databaseOption = new Option<string>(
            aliases: new[] { "--database", "--db", "-d" },
            description: "Spécifie le fichier de base de données à utiliser")
        {
            IsRequired = false
        };

        var scriptOption = new Option<string>(
            aliases: new[] { "--script", "-s" },
            description: "Exécute un script de commandes GraphQLite")
        {
            IsRequired = false
        };

        var interactiveOption = new Option<bool>(
            aliases: new[] { "--interactive", "-i" },
            description: "Mode interactif avec autocomplétion")
        {
            IsRequired = false
        };

        var rootCommand = new RootCommand("GraphQLite - Base de données orientée graphe avec DSL en langage naturel")
        {
            databaseOption,
            scriptOption,
            interactiveOption
        };

        rootCommand.SetHandler(async (database, script, interactive) =>
        {
            await HandleCommand(database, script, interactive);
        }, databaseOption, scriptOption, interactiveOption);

        return await rootCommand.InvokeAsync(args);
    }

    static async Task HandleCommand(string? database, string? script, bool interactive)
    {
        // Déterminer le chemin de la base de données
        string dbPath = DetermineDatabasePath(database, script);

        // Si un script est spécifié, l'exécuter et quitter
        if (!string.IsNullOrEmpty(script))
        {
            await ExecuteScriptAndExit(dbPath, script);
            return;
        }

        // Mode interactif avec autocomplétion
        if (interactive || string.IsNullOrEmpty(script))
        {
            await RunInteractiveMode(dbPath);
        }
    }

    static string DetermineDatabasePath(string? database, string? script)
    {
        if (!string.IsNullOrEmpty(database))
        {
            var providedPath = database;
            
            // Si le chemin ne contient pas d'extension, ajouter .gqlite
            if (!Path.HasExtension(providedPath))
            {
                providedPath += ".gqlite";
            }
            
            // Si le chemin n'est pas absolu, le rendre relatif au répertoire courant
            if (!Path.IsPathRooted(providedPath))
            {
                return Path.Combine(Environment.CurrentDirectory, providedPath);
            }
            
            return providedPath;
        }

        // Si un script est spécifié sans DB, créer une DB avec le nom du script
        if (!string.IsNullOrEmpty(script))
        {
            var scriptFileName = Path.GetFileNameWithoutExtension(script);
            return Path.Combine(Environment.CurrentDirectory, $"{scriptFileName}.gqlite");
        }

        // DB par défaut
        return Path.Combine(Environment.CurrentDirectory, "graphqlite.gqlite");
    }

    static async Task ExecuteScriptAndExit(string dbPath, string scriptPath)
    {
        Console.WriteLine("GraphQLite - Mode script");
        Console.WriteLine();

        using var engine = new GraphQLiteEngine(dbPath);
        
        try
        {
            var loadResult = await engine.InitializeAsync();
            
            if (!loadResult.Success)
            {
                Console.WriteLine($"Erreur d'initialisation : {loadResult.Message}");
                Environment.Exit(1);
            }
            
            Console.WriteLine($"Base de données : {dbPath}");
            Console.WriteLine(loadResult.Message);
            Console.WriteLine();

            var scriptEngine = new GraphQLite.Scripting.ScriptEngine(engine);
            
            // Validation du script avant exécution
            Console.WriteLine("Validation de la syntaxe du script...");
            var validationResult = scriptEngine.ValidateScript(scriptPath);
            
            if (!validationResult.IsValid)
            {
                Console.WriteLine("Erreurs de validation détectées :");
                foreach (var error in validationResult.Errors)
                {
                    Console.WriteLine($"  • {error}");
                }
                Environment.Exit(1);
            }
            
            Console.WriteLine($"Script valide ({validationResult.QueryCount} requêtes détectées)");
            Console.WriteLine();

            var scriptResult = await scriptEngine.ExecuteScriptAsync(scriptPath);
            
            if (!scriptResult.Success)
            {
                Console.WriteLine($"Erreur script : {scriptResult.Error}");
                Environment.Exit(1);
            }

            // Code de sortie basé sur le nombre d'erreurs
            var exitCode = scriptResult.ErrorCount > 0 ? 1 : 0;
            Console.WriteLine($"\nScript terminé avec le code de sortie : {exitCode}");
            Environment.Exit(exitCode);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur fatale : {ex.Message}");
            Environment.Exit(1);
        }
    }

    static async Task RunInteractiveMode(string dbPath)
    {
        Console.WriteLine("GraphQLite - Base de données orientée graphe");
        Console.WriteLine("DSL en langage naturel avec autocomplétion");
        Console.WriteLine();

        using var engine = new GraphQLiteEngine(dbPath);
        
        try
        {
            var loadResult = await engine.InitializeAsync();
            
            if (loadResult.Success)
            {
                Console.WriteLine($"Fichier : {dbPath}");
                Console.WriteLine(loadResult.Message);
                
                if (!loadResult.IsNewDatabase)
                {
                    Console.WriteLine($"Données existantes chargées avec succès.");
                }
            }
            else
            {
                Console.WriteLine($"Erreur d'initialisation : {loadResult.Message}");
                return;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur d'initialisation : {ex.Message}");
            return;
        }

        Console.WriteLine("\nGraphQLite est prêt. Tapez 'help' pour voir les commandes ou 'exit' pour quitter.");
        Console.WriteLine("Utilisez Tab pour l'autocomplétion et ↑↓ pour naviguer dans l'historique.");
        
        var cli = new InteractiveCLI(engine);
        await cli.RunAsync();
    }
}

public class InteractiveCLI
{
    private readonly GraphQLiteEngine _engine;
    private readonly List<string> _commandHistory = new();
    private int _historyIndex = -1;
    private readonly Dictionary<string, string[]> _completions = new();

    public InteractiveCLI(GraphQLiteEngine engine)
    {
        _engine = engine;
        InitializeCompletions();
    }

    private void InitializeCompletions()
    {
        _completions["commands"] = new[]
        {
            "create", "find", "update", "delete", "connect", "count", "show", "help", "exit", "quit",
            "variables", "clear-variables", "paginate", "cursor", "sum", "avg", "min", "max",
            "group", "order", "sort", "join", "merge", "optimize", "calculate", "floyd", "dijkstra", "astar"
        };

        _completions["node_types"] = new[]
        {
            "person", "persons", "company", "companies", "product", "products", "project", "projects",
            "user", "users", "employee", "employees", "customer", "customers", "order", "orders"
        };

        _completions["edge_types"] = new[]
        {
            "works_for", "knows", "manages", "reports_to", "supervises", "collaborates", "owns",
            "belongs_to", "participates", "contributes", "sponsors", "produces", "uses", "buys"
        };

        _completions["properties"] = new[]
        {
            "name", "age", "salary", "role", "department", "industry", "employees", "price",
            "status", "location", "city", "country", "email", "phone", "website", "budget"
        };

        _completions["operators"] = new[]
        {
            "=", ">", "<", ">=", "<=", "!=", "and", "or", "in", "not in", "exists", "not exists"
        };

        _completions["functions"] = new[]
        {
            "sum", "avg", "min", "max", "count", "row_number", "rank", "dense_rank", "percent_rank",
            "ntile", "lead", "lag", "first_value", "last_value", "nth_value"
        };

        _completions["keywords"] = new[]
        {
            "with", "where", "and", "or", "limit", "offset", "order", "by", "group", "having",
            "from", "to", "via", "avoiding", "within", "steps", "over", "connected", "reachable",
            "neighbors", "adjacent", "path", "shortest", "bidirectional", "all", "any", "exists",
            "not", "in", "as", "variable", "define", "set", "update", "delete", "remove"
        };
    }

    public async Task RunAsync()
    {
        while (true)
        {
            Console.Write("\nGraphQLite> ");
            
            var input = await ReadLineWithCompletionAsync();
            
            // Si l'entrée est null (fin de flux), sortir
            if (input == null)
            {
                break;
            }
            
            if (string.IsNullOrWhiteSpace(input))
                continue;

            // Ajouter à l'historique
            if (!string.IsNullOrWhiteSpace(input))
            {
                _commandHistory.Add(input);
                _historyIndex = _commandHistory.Count;
            }

            if (input.Trim().ToLowerInvariant() is "exit" or "quit" or "q")
            {
                Console.WriteLine("Au revoir !");
                break;
            }

            if (input.Trim().ToLowerInvariant() == "help")
            {
                ShowHelp();
                continue;
            }

            if (input.Trim().ToLowerInvariant() == "variables")
            {
                ShowVariables();
                continue;
            }

            if (input.Trim().ToLowerInvariant() == "clear-variables")
            {
                _engine.ClearVariables();
                Console.WriteLine("Toutes les variables ont été supprimées.");
                continue;
            }

            if (input.Trim().ToLowerInvariant() == "clear")
            {
                Console.Clear();
                continue;
            }

            if (input.Trim().ToLowerInvariant() == "history")
            {
                ShowHistory();
                continue;
            }

            // Exécuter la requête
            try
            {
                var result = await _engine.ExecuteQueryAsync(input);
                DisplayResult(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur : {ex.Message}");
            }

            // Si l'entrée est redirigée, sortir après avoir traité la commande
            if (Console.IsInputRedirected)
            {
                break;
            }
        }
    }

    private async Task<string> ReadLineWithCompletionAsync()
    {
        // Vérifier si la console est interactive
        if (!Console.IsInputRedirected && !Console.IsOutputRedirected)
        {
            var input = "";
            var cursorPosition = 0;
            var suggestions = new List<string>();
            var suggestionIndex = -1;

            while (true)
            {
                var key = Console.ReadKey(true);

                switch (key.Key)
                {
                    case ConsoleKey.Enter:
                        Console.WriteLine();
                        return input;

                    case ConsoleKey.Escape:
                        Console.WriteLine();
                        return "";

                    case ConsoleKey.Backspace:
                        if (cursorPosition > 0)
                        {
                            input = input.Remove(cursorPosition - 1, 1);
                            cursorPosition--;
                            RedrawLine(input, cursorPosition);
                        }
                        break;

                    case ConsoleKey.Delete:
                        if (cursorPosition < input.Length)
                        {
                            input = input.Remove(cursorPosition, 1);
                            RedrawLine(input, cursorPosition);
                        }
                        break;

                    case ConsoleKey.LeftArrow:
                        if (cursorPosition > 0)
                        {
                            cursorPosition--;
                            SetCursorPosition(cursorPosition);
                        }
                        break;

                    case ConsoleKey.RightArrow:
                        if (cursorPosition < input.Length)
                        {
                            cursorPosition++;
                            SetCursorPosition(cursorPosition);
                        }
                        break;

                    case ConsoleKey.UpArrow:
                        if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                        {
                            // Navigation dans l'historique
                            if (_historyIndex > 0)
                            {
                                _historyIndex--;
                                input = _commandHistory[_historyIndex];
                                cursorPosition = input.Length;
                                RedrawLine(input, cursorPosition);
                            }
                        }
                        else if (suggestions.Count > 0)
                        {
                            // Navigation dans les suggestions
                            suggestionIndex = (suggestionIndex - 1 + suggestions.Count) % suggestions.Count;
                            ShowSuggestions(suggestions, suggestionIndex);
                        }
                        break;

                    case ConsoleKey.DownArrow:
                        if (key.Modifiers.HasFlag(ConsoleModifiers.Control))
                        {
                            // Navigation dans l'historique
                            if (_historyIndex < _commandHistory.Count - 1)
                            {
                                _historyIndex++;
                                input = _commandHistory[_historyIndex];
                                cursorPosition = input.Length;
                                RedrawLine(input, cursorPosition);
                            }
                        }
                        else if (suggestions.Count > 0)
                        {
                            // Navigation dans les suggestions
                            suggestionIndex = (suggestionIndex + 1) % suggestions.Count;
                            ShowSuggestions(suggestions, suggestionIndex);
                        }
                        break;

                    case ConsoleKey.Tab:
                        if (suggestions.Count > 0)
                        {
                            // Appliquer la suggestion sélectionnée
                            var selectedSuggestion = suggestions[suggestionIndex >= 0 ? suggestionIndex : 0];
                            var lastWord = GetLastWord(input, cursorPosition);
                            
                            // Vérifier que la suggestion commence bien par le dernier mot
                            if (selectedSuggestion.ToLowerInvariant().StartsWith(lastWord.ToLowerInvariant()))
                            {
                                var replacement = selectedSuggestion.Substring(Math.Min(lastWord.Length, selectedSuggestion.Length));
                                
                                input = input.Insert(cursorPosition, replacement);
                                cursorPosition += replacement.Length;
                                RedrawLine(input, cursorPosition);
                            }
                        }
                        else
                        {
                            // Générer les suggestions
                            suggestions = GenerateSuggestions(input, cursorPosition);
                            if (suggestions.Count > 0)
                            {
                                suggestionIndex = 0;
                                ShowSuggestions(suggestions, suggestionIndex);
                            }
                        }
                        break;

                    default:
                        if (key.KeyChar >= 32 && key.KeyChar <= 126)
                        {
                            input = input.Insert(cursorPosition, key.KeyChar.ToString());
                            cursorPosition++;
                            RedrawLine(input, cursorPosition);
                        }
                        break;
                }
            }
        }
        else
        {
            // Mode non-interactif (redirection d'entrée)
            var input = Console.ReadLine();
            return input; // Retourner null si fin de flux
        }
    }

    private void RedrawLine(string input, int cursorPosition)
    {
        var currentLine = Console.CursorTop;
        Console.SetCursorPosition(0, currentLine);
        Console.Write("GraphQLite> " + input);
        Console.Write(new string(' ', Console.WindowWidth - Console.CursorLeft));
        SetCursorPosition(cursorPosition);
    }

    private void SetCursorPosition(int position)
    {
        Console.SetCursorPosition("GraphQLite> ".Length + position, Console.CursorTop);
    }

    private string GetLastWord(string input, int cursorPosition)
    {
        var beforeCursor = input.Substring(0, cursorPosition);
        var words = beforeCursor.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return words.Length > 0 ? words[words.Length - 1] : "";
    }

    private List<string> GenerateSuggestions(string input, int cursorPosition)
    {
        var suggestions = new List<string>();
        var beforeCursor = input.Substring(0, cursorPosition);
        var lastWord = GetLastWord(input, cursorPosition).ToLowerInvariant();

        if (string.IsNullOrEmpty(lastWord))
            return suggestions;

        // Suggestions basées sur le contexte
        foreach (var completion in _completions)
        {
            foreach (var item in completion.Value)
            {
                if (item.ToLowerInvariant().StartsWith(lastWord))
                {
                    suggestions.Add(item);
                }
            }
        }

        // Suggestions contextuelles basées sur la position dans la commande
        var words = beforeCursor.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (words.Length == 0)
        {
            suggestions.AddRange(_completions["commands"]);
        }
        else if (words.Length == 1)
        {
            var command = words[0].ToLowerInvariant();
            switch (command)
            {
                case "create":
                case "find":
                case "update":
                case "delete":
                case "count":
                    suggestions.AddRange(_completions["node_types"]);
                    break;
                case "connect":
                    suggestions.AddRange(_completions["node_types"]);
                    break;
                case "sum":
                case "avg":
                case "min":
                case "max":
                    suggestions.AddRange(_completions["properties"]);
                    break;
            }
        }

        return suggestions.Distinct().Take(10).ToList();
    }

    private void ShowSuggestions(List<string> suggestions, int selectedIndex)
    {
        if (suggestions.Count == 0) return;

        var currentLine = Console.CursorTop;
        Console.WriteLine();
        
        for (int i = 0; i < suggestions.Count; i++)
        {
            if (i == selectedIndex)
            {
                Console.BackgroundColor = ConsoleColor.Blue;
                Console.ForegroundColor = ConsoleColor.White;
            }
            else
            {
                Console.BackgroundColor = ConsoleColor.Black;
                Console.ForegroundColor = ConsoleColor.Gray;
            }
            
            Console.WriteLine($"  {suggestions[i]}");
        }
        
        Console.ResetColor();
        Console.SetCursorPosition(0, currentLine);
    }

    private void ShowHelp()
    {
        Console.WriteLine("\n=== GraphQLite - Aide ===\n");
        Console.WriteLine("Commandes de base :");
        Console.WriteLine("  create person with name John and age 30");
        Console.WriteLine("  find all persons where age > 25");
        Console.WriteLine("  connect John to Acme with relationship works_at");
        Console.WriteLine("  update person set age 31 where name John");
        Console.WriteLine("  count persons where age > 18");
        Console.WriteLine("  delete person where name John");
        Console.WriteLine("  show schema");
        Console.WriteLine("\nCommandes avancées :");
        Console.WriteLine("  find path from John to Mary");
        Console.WriteLine("  find persons from John over 2 steps");
        Console.WriteLine("  sum salary of persons where age > 30");
        Console.WriteLine("  group persons by city having count > 2");
        Console.WriteLine("  join persons with companies via works_for");
        Console.WriteLine("\nCommandes système :");
        Console.WriteLine("  help          - Afficher cette aide");
        Console.WriteLine("  variables     - Afficher les variables définies");
        Console.WriteLine("  clear-variables - Supprimer toutes les variables");
        Console.WriteLine("  history       - Afficher l'historique des commandes");
        Console.WriteLine("  clear         - Effacer l'écran");
        Console.WriteLine("  exit/quit     - Quitter");
        Console.WriteLine("\nRaccourcis clavier :");
        Console.WriteLine("  Tab           - Autocomplétion");
        Console.WriteLine("  ↑↓            - Navigation dans les suggestions");
        Console.WriteLine("  Ctrl+↑↓       - Navigation dans l'historique");
        Console.WriteLine("  Échap         - Annuler la saisie");
    }

    private void ShowVariables()
    {
        var variables = _engine.GetVariables();
        
        if (variables.Count == 0)
        {
            Console.WriteLine("Aucune variable définie.");
            return;
        }
        
        Console.WriteLine("Variables définies :");
        foreach (var variable in variables)
        {
            Console.WriteLine($"  {variable.Key} = {variable.Value}");
        }
    }

    private void ShowHistory()
    {
        if (_commandHistory.Count == 0)
        {
            Console.WriteLine("Aucune commande dans l'historique.");
            return;
        }

        Console.WriteLine("Historique des commandes :");
        for (int i = 0; i < _commandHistory.Count; i++)
        {
            Console.WriteLine($"  {i + 1}: {_commandHistory[i]}");
        }
    }

    private void DisplayResult(QueryResult result)
    {
        if (result.Success)
        {
            Console.WriteLine(result.Message);
            
            if (result.Data != null)
            {
                DisplayData(result.Data);
            }
        }
        else
        {
            Console.WriteLine($"Erreur : {result.Error}");
        }
    }

    private void DisplayData(object data)
    {
        switch (data)
        {
            case GraphQLite.Models.DatabaseSchema schema:
                DisplaySchema(schema);
                break;
                
            case IEnumerable<GraphQLite.Models.Node> nodes:
                Console.WriteLine("\nNœuds trouvés :");
                foreach (var node in nodes)
                {
                    Console.WriteLine($"  {node}");
                }
                break;
                
            case IEnumerable<GraphQLite.Models.Edge> edges:
                Console.WriteLine("\nArêtes trouvées :");
                foreach (var edge in edges)
                {
                    Console.WriteLine($"  {edge}");
                }
                break;
                
            case int count:
                Console.WriteLine($"Résultat : {count}");
                break;
                
            default:
                Console.WriteLine($"Données : {data}");
                break;
        }
    }

    private void DisplaySchema(GraphQLite.Models.DatabaseSchema schema)
    {
        Console.WriteLine($"\nSchéma généré le {schema.GeneratedAt:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Total : {schema.TotalNodes} nœuds, {schema.TotalEdges} arêtes\n");

        // Afficher les nœuds
        if (schema.NodeSchemas.Any())
        {
            Console.WriteLine("NŒUDS :");
            foreach (var nodeSchema in schema.NodeSchemas.Values.OrderBy(n => n.Label))
            {
                Console.WriteLine($"  {nodeSchema.Label} ({nodeSchema.Count} instances)");
                Console.WriteLine($"    Créé: {nodeSchema.FirstSeen:yyyy-MM-dd}, Modifié: {nodeSchema.LastUpdated:yyyy-MM-dd}");
                
                if (nodeSchema.Properties.Any())
                {
                    Console.WriteLine("    Propriétés :");
                    foreach (var prop in nodeSchema.Properties.OrderBy(p => p.Key))
                    {
                        var uniqueCount = prop.Value.UniqueValues.Count;
                        var sampleValues = uniqueCount > 0 
                            ? $" (ex: {string.Join(", ", prop.Value.UniqueValues.Take(3))})"
                            : "";
                        
                        Console.WriteLine($"      {prop.Key}: {prop.Value.Type} ({prop.Value.Count}/{nodeSchema.Count}){sampleValues}");
                    }
                }
                Console.WriteLine();
            }
        }

        // Afficher les arêtes
        if (schema.EdgeSchemas.Any())
        {
            Console.WriteLine("ARÊTES :");
            foreach (var edgeSchema in schema.EdgeSchemas.Values.OrderBy(e => e.Label))
            {
                Console.WriteLine($"  {edgeSchema.Label} ({edgeSchema.Count} relations)");
                Console.WriteLine($"    Créé: {edgeSchema.FirstSeen:yyyy-MM-dd}, Modifié: {edgeSchema.LastUpdated:yyyy-MM-dd}");
                
                if (edgeSchema.Properties.Any())
                {
                    Console.WriteLine("    Propriétés :");
                    foreach (var prop in edgeSchema.Properties.OrderBy(p => p.Key))
                    {
                        var uniqueCount = prop.Value.UniqueValues.Count;
                        var sampleValues = uniqueCount > 0 
                            ? $" (ex: {string.Join(", ", prop.Value.UniqueValues.Take(3))})"
                            : "";
                        
                        Console.WriteLine($"      {prop.Key}: {prop.Value.Type} ({prop.Value.Count}/{edgeSchema.Count}){sampleValues}");
                    }
                }
                Console.WriteLine();
            }
        }

        if (!schema.NodeSchemas.Any() && !schema.EdgeSchemas.Any())
        {
            Console.WriteLine("  La base de données est vide. Créez des nœuds et des arêtes pour voir le schéma.");
        }
    }
}
