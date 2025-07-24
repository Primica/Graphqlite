using GraphQLite.Engine;

namespace GraphQLite;

class Program
{
    static async Task Main(string[] args)
    {
        Console.WriteLine("GraphQLite - Base de données orientée graphe");
        Console.WriteLine("DSL en langage naturel");
        Console.WriteLine();

        // Analyser les arguments de ligne de commande
        string dbPath = ParseCommandLineArgs(args);

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
        
        while (true)
        {
            Console.Write("\nGraphQLite> ");
            var input = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(input))
                continue;

            if (input.Trim().ToLowerInvariant() is "exit" or "quit" or "q")
            {
                Console.WriteLine("Au revoir !");
                break;
            }

            if (input.Trim().ToLowerInvariant() == "help")
            {
                ShowExamples();
                continue;
            }

            // Exécuter la requête
            try
            {
                var result = await engine.ExecuteQueryAsync(input);
                DisplayResult(result);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erreur : {ex.Message}");
            }
        }
    }

    static string ParseCommandLineArgs(string[] args)
    {
        string? dbPath = null;
        string? scriptPath = null;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i].ToLowerInvariant())
            {
                case "--db":
                case "-d":
                    if (i + 1 < args.Length)
                    {
                        var providedPath = args[i + 1];
                        
                        // Si le chemin ne contient pas d'extension, ajouter .gqlite
                        if (!Path.HasExtension(providedPath))
                        {
                            providedPath += ".gqlite";
                        }
                        
                        // Si le chemin n'est pas absolu, le rendre relatif au répertoire courant
                        if (!Path.IsPathRooted(providedPath))
                        {
                            dbPath = Path.Combine(Environment.CurrentDirectory, providedPath);
                        }
                        else
                        {
                            dbPath = providedPath;
                        }
                        
                        i++; // Ignorer le prochain argument car c'est la valeur
                    }
                    else
                    {
                        Console.WriteLine("Erreur : --db/-d nécessite un nom de fichier");
                        Environment.Exit(1);
                    }
                    break;

                case "--script":
                case "-s":
                    if (i + 1 < args.Length)
                    {
                        scriptPath = args[i + 1];
                        
                        // Ajouter extension .gqls si absente
                        if (!Path.HasExtension(scriptPath))
                        {
                            scriptPath += ".gqls";
                        }
                        
                        if (!Path.IsPathRooted(scriptPath))
                        {
                            scriptPath = Path.Combine(Environment.CurrentDirectory, scriptPath);
                        }

                        i++; // Ignorer le prochain argument car c'est la valeur
                    }
                    else
                    {
                        Console.WriteLine("Erreur : --script/-s nécessite un nom de fichier");
                        Environment.Exit(1);
                    }
                    break;
                    
                case "--help":
                case "-h":
                    ShowUsage();
                    Environment.Exit(0);
                    break;
                    
                default:
                    Console.WriteLine($"Argument inconnu : {args[i]}");
                    ShowUsage();
                    Environment.Exit(1);
                    break;
            }
        }

        // Si un script est spécifié, l'exécuter
        if (!string.IsNullOrEmpty(scriptPath))
        {
            // Si aucune DB n'est spécifiée, créer une DB avec le nom du script
            if (string.IsNullOrEmpty(dbPath))
            {
                var scriptFileName = Path.GetFileNameWithoutExtension(scriptPath);
                dbPath = Path.Combine(Environment.CurrentDirectory, $"{scriptFileName}.gqlite");
            }
            
            ExecuteScriptAndExit(dbPath, scriptPath).Wait();
        }

        // Si aucune DB n'est spécifiée et pas de script, utiliser la DB par défaut
        return dbPath ?? Path.Combine(Environment.CurrentDirectory, "graphqlite.gqlite");
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
            var scriptResult = await scriptEngine.ExecuteScriptAsync(scriptPath);
            
            if (!scriptResult.Success)
            {
                Console.WriteLine($"Erreur script : {scriptResult.Error}");
                Environment.Exit(1);
            }

            Environment.Exit(scriptResult.ErrorCount > 0 ? 1 : 0);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erreur : {ex.Message}");
            Environment.Exit(1);
        }
    }

    static void ShowUsage()
    {
        Console.WriteLine("Usage : GraphQLite [options]");
        Console.WriteLine();
        Console.WriteLine("Options :");
        Console.WriteLine("  --db, -d <fichier>      Spécifie le fichier de base de données à utiliser");
        Console.WriteLine("                          (extension .gqlite ajoutée automatiquement si absente)");
        Console.WriteLine("  --script, -s <fichier>  Exécute un script de commandes GraphQLite");
        Console.WriteLine("                          (extension .gqls ajoutée automatiquement si absente)");
        Console.WriteLine("                          Si aucune DB n'est spécifiée, crée <script>.gqlite");
        Console.WriteLine("  --help, -h              Affiche cette aide");
        Console.WriteLine();
        Console.WriteLine("Exemples :");
        Console.WriteLine("  GraphQLite                        # Mode interactif avec graphqlite.gqlite");
        Console.WriteLine("  GraphQLite --db mydb              # Mode interactif avec mydb.gqlite");
        Console.WriteLine("  GraphQLite --script example       # Exécute example.gqls -> example.gqlite");
        Console.WriteLine("  GraphQLite --script setup         # Exécute setup.gqls -> setup.gqlite");
        Console.WriteLine("  GraphQLite --db prod --script init # Exécute init.gqls -> prod.gqlite");
        Console.WriteLine("  GraphQLite -s /path/to/script      # Exécute le script à cet emplacement");
    }

    static void ShowExamples()
    {
        Console.WriteLine("\nCommandes disponibles :");
        Console.WriteLine("  create person with name John and age 30");
        Console.WriteLine("  connect John to Acme with relationship works_at");
        Console.WriteLine("  find all persons where age > 25");
        Console.WriteLine("  find path from John to Mary");
        Console.WriteLine("  find persons from John over 2 steps");
        Console.WriteLine("  update person set age 31 where name John");
        Console.WriteLine("  count persons where age > 18");
        Console.WriteLine("  delete person where name John");
        Console.WriteLine("  show schema");
        Console.WriteLine("\nTapez 'help' pour revoir ces commandes.");
    }

    static void DisplayResult(QueryResult result)
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

    static void DisplayData(object data)
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

    static void DisplaySchema(GraphQLite.Models.DatabaseSchema schema)
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
