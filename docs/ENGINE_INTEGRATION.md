# Intégration des Moteurs GraphQLite et ScriptEngine

Ce document explique comment l'API REST GraphQLite s'appuie sur les moteurs GraphQLite et ScriptEngine pour fournir des fonctionnalités complètes.

## Architecture des Moteurs

### 1. GraphQLiteEngine
Le moteur principal de GraphQLite qui orchestre toutes les opérations de base de données.

**Composants intégrés :**
- `GraphStorage` : Stockage et persistance des données
- `NaturalLanguageParser` : Analyse des requêtes en langage naturel  
- `VariableManager` : Gestion des variables de requête
- `QueryCacheManager` : Cache intelligent des requêtes
- `GraphOptimizationEngine` : Optimisation des graphes
- `IntelligentPagination` : Pagination intelligente

**Usage dans l'API :**
```csharp
// Enregistré comme service singleton dans Program.cs
builder.Services.AddSingleton<GraphQLiteEngine>(provider =>
    new GraphQLiteEngine(databasePath));

// Utilisé dans tous les contrôleurs
private readonly GraphQLiteEngine _engine;
var result = await _engine.ExecuteQueryAsync(query);
```

### 2. ScriptEngine
Moteur d'exécution des scripts GraphQLite (.gqls) qui permet l'exécution batch de requêtes.

**Fonctionnalités :**
- Analyse et parsing de scripts .gqls
- Exécution séquentielle de requêtes
- Gestion des variables de script
- Reporting détaillé des résultats

**Usage dans l'API :**
```csharp
// Enregistré comme service scoped dans Program.cs
builder.Services.AddScoped<ScriptEngine>(provider =>
{
    var engine = provider.GetRequiredService<GraphQLiteEngine>();
    return new ScriptEngine(engine);
});

// Utilisé dans ScriptController
private readonly ScriptEngine _scriptEngine;
var result = await _scriptEngine.ExecuteScriptAsync(scriptPath);
```

## Contrôleurs API et Intégration des Moteurs

### NodesController
- **Moteur utilisé** : GraphQLiteEngine
- **Fonctionnalités** : CRUD sur les nœuds, recherche, agrégations
- **Méthodes clés** :
  - `ExecuteQueryAsync()` pour toutes les opérations sur les nœuds
  - Utilise le parser de langage naturel intégré

### EdgesController  
- **Moteur utilisé** : GraphQLiteEngine
- **Fonctionnalités** : Gestion des arêtes et relations
- **Méthodes clés** :
  - Construction dynamique de requêtes de création d'arêtes
  - Nettoyage des noms de nœuds (gestion des guillemets)

### PathsController
- **Moteur utilisé** : GraphQLiteEngine
- **Fonctionnalités** : Navigation dans le graphe, recherche de chemins
- **Méthodes clés** :
  - Requêtes de traversal de graphe
  - Algorithmes de plus court chemin

### QueryController
- **Moteur utilisé** : GraphQLiteEngine + VariableManager
- **Fonctionnalités** : Exécution directe de requêtes naturelles
- **Méthodes clés** :
  - `ExecuteQueryAsync()` pour l'exécution directe
  - Gestion des variables via le VariableManager

### SchemaController
- **Moteur utilisé** : GraphQLiteEngine + GraphStorage
- **Fonctionnalités** : Métadonnées et statistiques
- **Méthodes clés** :
  - Accès direct aux statistiques du storage
  - Gestion des index via IndexManager

### AdvancedController
- **Moteur utilisé** : GraphQLiteEngine + GraphOptimizationEngine
- **Fonctionnalités** : Jointures virtuelles, optimisations
- **Méthodes clés** :
  - Optimisation via GraphOptimizationEngine
  - Opérations batch avancées

### ScriptController (Nouveau)
- **Moteur utilisé** : ScriptEngine + GraphQLiteEngine
- **Fonctionnalités** : Exécution de scripts .gqls
- **Méthodes clés** :
  - `ExecuteScriptAsync()` pour l'exécution de fichiers
  - Gestion de scripts inline via contenu textuel

## Endpoints d'Intégration des Moteurs

### Moteur GraphQLite Direct
```
POST /api/query/execute
POST /api/nodes
POST /api/edges  
POST /api/paths/find
POST /api/advanced/optimize
```

### Moteur ScriptEngine
```
POST /api/script/execute
POST /api/script/execute-content  
GET  /api/script/list
GET  /api/script/content
```

## Flux d'Exécution Typique

### 1. Requête Simple (via QueryController)
```
Requête API → QueryController → GraphQLiteEngine → NaturalLanguageParser → GraphStorage → Résultat
```

### 2. Exécution de Script (via ScriptController)
```
Script API → ScriptController → ScriptEngine → GraphQLiteEngine (multiple) → Résultats agrégés
```

### 3. Opération Complexe (via AdvancedController)
```
API → AdvancedController → GraphOptimizationEngine → GraphQLiteEngine → Storage → Résultat optimisé
```

## Avantages de cette Architecture

1. **Réutilisation** : L'API REST utilise exactement les mêmes moteurs que le CLI
2. **Consistance** : Même logique métier dans tous les modes d'accès
3. **Performance** : Cache et optimisations partagées
4. **Maintenabilité** : Une seule implémentation des algorithmes core
5. **Testabilité** : Tests unitaires partagés entre CLI et API

## Configuration et Initialisation

```csharp
// Program.cs - Configuration des services
var databasePath = builder.Configuration["DatabasePath"] ?? "graphqlite.gqlite";

// Moteur principal (Singleton)
builder.Services.AddSingleton<GraphQLiteEngine>(provider =>
    new GraphQLiteEngine(databasePath));

// Moteur de script (Scoped)
builder.Services.AddScoped<ScriptEngine>(provider =>
{
    var engine = provider.GetRequiredService<GraphQLiteEngine>();
    return new ScriptEngine(engine);
});

// Initialisation au démarrage
var engine = app.Services.GetRequiredService<GraphQLiteEngine>();
await engine.InitializeAsync();
```

## Tests et Validation

### Script de Test Complet
```bash
# Test de tous les moteurs
./scripts/run-tests.sh

# Test spécifique au ScriptEngine  
./scripts/test-script-api.sh
```

### Endpoints de Santé
```
GET /health - Santé générale du service
GET /api/schema/stats - Statistiques des moteurs
```

Cette architecture garantit que l'API REST bénéficie de toute la puissance et l'intelligence des moteurs GraphQLite tout en offrant une interface moderne et accessible.
