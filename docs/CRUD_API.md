# API CRUD GraphQLite Unifiée

## 🎯 Vue d'ensemble

L'API CRUD GraphQLite est une interface REST unifiée qui s'appuie **entièrement** sur les moteurs GraphQLiteEngine et ScriptEngine pour fournir toutes les opérations CRUD et fonctionnalités avancées.

### Architecture

```
REST API → GraphQLiteCrudController → GraphQLiteEngine + ScriptEngine → GraphStorage
```

- **Un seul contrôleur** : `GraphQLiteCrudController`
- **Moteurs natifs** : Utilisation directe de GraphQLiteEngine et ScriptEngine
- **Consistance totale** : Même logique métier qu'en CLI
- **Performance optimale** : Cache et optimisations natives

## 📋 Endpoints Disponibles

### 🏥 Santé et Informations

#### GET /api/health
Vérifie la santé des moteurs GraphQLite et ScriptEngine.

**Réponse :**
```json
{
  "success": true,
  "data": {
    "isHealthy": true,
    "graphQLiteEngine": "OK",
    "scriptEngine": "OK",
    "timestamp": "2025-08-08T10:00:00Z"
  }
}
```

#### GET /api/stats
Obtient les statistiques complètes du graphe via GraphQLiteEngine.

**Réponse :**
```json
{
  "success": true,
  "data": {
    "totalNodes": 1250,
    "totalEdges": 3420,
    "nodeTypes": ["Person", "Company", "Project"],
    "indexedProperties": ["name", "age", "industry"]
  }
}
```

### 📝 Opérations de Création (CREATE)

#### POST /api/nodes
Crée un nouveau nœud via GraphQLiteEngine.

**Requête :**
```json
{
  "label": "Person",
  "properties": {
    "name": "John Doe",
    "age": 30,
    "city": "Paris"
  }
}
```

**Réponse :**
```json
{
  "success": true,
  "message": "Nœud créé avec l'ID : abc123",
  "data": {
    "nodeId": "abc123",
    "label": "Person",
    "properties": { "name": "John Doe", "age": 30, "city": "Paris" }
  }
}
```

#### POST /api/edges
Crée une nouvelle arête via GraphQLiteEngine.

**Requête :**
```json
{
  "fromNode": "John Doe",
  "toNode": "TechCorp",
  "edgeType": "works_at",
  "properties": {
    "since": "2023-01-01",
    "role": "Developer"
  }
}
```

#### POST /api/nodes/batch
Crée plusieurs nœuds en batch via ScriptEngine.

**Requête :**
```json
{
  "nodes": [
    {"label": "Employee", "properties": {"name": "Alice", "department": "IT"}},
    {"label": "Employee", "properties": {"name": "Bob", "department": "HR"}}
  ],
  "atomic": false
}
```

**Réponse :**
```json
{
  "success": true,
  "data": {
    "totalOperations": 2,
    "successfulOperations": 2,
    "failedOperations": 0,
    "executionTime": "00:00:01.234"
  }
}
```

### 🔍 Opérations de Lecture (READ)

#### GET /api/nodes
Recherche des nœuds via GraphQLiteEngine.

**Paramètres :**
- `label` : Label des nœuds (optionnel)
- `conditions[property]` : Conditions de filtre
- `limit` : Nombre maximum de résultats
- `offset` : Décalage pour la pagination

**Exemples :**
```
GET /api/nodes?label=Person
GET /api/nodes?label=Company&conditions[industry]=Technology
GET /api/nodes?label=Employee&limit=10&offset=20
```

#### GET /api/edges
Recherche des arêtes via GraphQLiteEngine.

**Paramètres :**
- `fromNode` : Nœud source (optionnel)
- `toNode` : Nœud destination (optionnel)
- `edgeType` : Type d'arête (optionnel)
- `conditions[property]` : Conditions de filtre

#### GET /api/paths
Trouve un chemin entre deux nœuds via GraphQLiteEngine.

**Paramètres :**
- `fromNode` : Nœud de départ (requis)
- `toNode` : Nœud d'arrivée (requis)
- `maxSteps` : Nombre maximum d'étapes
- `algorithm` : Algorithme à utiliser (défaut: dijkstra)

**Exemple :**
```
GET /api/paths?fromNode=John Doe&toNode=API Development&maxSteps=3
```

#### POST /api/query
Exécute une requête en langage naturel via GraphQLiteEngine.

**Requête :**
```json
{
  "query": "find Person where age > 25 and city = Paris",
  "variables": {
    "minAge": "25",
    "targetCity": "Paris"
  },
  "useCache": true
}
```

### ✏️ Opérations de Mise à Jour (UPDATE)

#### PUT /api/nodes
Met à jour des nœuds via GraphQLiteEngine.

**Requête :**
```json
{
  "label": "Person",
  "properties": {
    "age": 31,
    "city": "Lyon"
  },
  "conditions": {
    "name": "John Doe"
  }
}
```

#### PUT /api/edges
Met à jour des arêtes via GraphQLiteEngine.

**Requête :**
```json
{
  "fromNode": "John Doe",
  "toNode": "TechCorp",
  "properties": {
    "role": "Senior Developer",
    "salary": 60000
  },
  "conditions": {
    "role": "Developer"
  }
}
```

### 🗑️ Opérations de Suppression (DELETE)

#### DELETE /api/nodes
Supprime des nœuds via GraphQLiteEngine.

**Requête :**
```json
{
  "label": "Person",
  "conditions": {
    "age": 30
  },
  "force": false
}
```

#### DELETE /api/edges
Supprime des arêtes via GraphQLiteEngine.

**Requête :**
```json
{
  "fromNode": "John Doe",
  "toNode": "TechCorp",
  "conditions": {
    "type": "works_at"
  }
}
```

### 📜 Opérations de Scripts

#### POST /api/scripts/execute
Exécute un script GraphQLite (.gqls) via ScriptEngine.

**Requête :**
```json
{
  "scriptPath": "scripts/setup-database.gqls",
  "variables": {
    "environment": "production",
    "version": "1.0"
  }
}
```

#### POST /api/scripts/execute-content
Exécute un contenu de script inline via ScriptEngine.

**Requête :**
```json
{
  "content": "create \"User\" with name=\"Admin\", role=\"administrator\"\nfind \"User\" where role=\"administrator\"",
  "variables": {
    "adminRole": "administrator"
  }
}
```

### ⚡ Opérations Avancées

#### POST /api/optimize
Optimise le graphe via GraphOptimizationEngine.

**Requête :**
```json
{
  "algorithm": "intelligent_optimization",
  "parameters": {
    "maxIterations": 10,
    "weightProperty": "importance"
  }
}
```

#### POST /api/aggregate
Exécute des fonctions d'agrégation via GraphQLiteEngine.

**Requête :**
```json
{
  "label": "Person",
  "function": "avg",
  "property": "age",
  "conditions": {
    "city": "Paris"
  }
}
```

**Fonctions disponibles :** `count`, `sum`, `avg`, `min`, `max`

## 🔧 Utilisation

### Démarrage du serveur
```bash
# Démarrer l'API
./scripts/start-api.sh

# Ou directement
dotnet run -- --server
```

### Tests complets
```bash
# Tester toute l'API CRUD
./scripts/test-crud-api.sh

# Tests avec serveur automatique
./scripts/run-tests.sh
```

### Swagger/OpenAPI
Une fois le serveur démarré, accédez à la documentation interactive :
- **URL :** http://localhost:5000
- **Documentation :** Interface Swagger complète
- **Test interactif :** Tous les endpoints testables

## 🚀 Avantages de cette Architecture

### 1. **Unification Complète**
- Un seul contrôleur pour toutes les opérations
- API cohérente et prévisible
- Réduction de la complexité

### 2. **Moteurs Natifs**
- GraphQLiteEngine pour toutes les opérations de base
- ScriptEngine pour l'exécution batch
- Même logique métier qu'en CLI

### 3. **Performance Optimale**
- Cache intelligent automatique
- Optimisations GraphQLite natives
- Indexation automatique des propriétés

### 4. **Extensibilité**
- Nouvelles fonctionnalités GraphQLite automatiquement disponibles
- Scripts .gqls réutilisables
- API versioning simple

### 5. **Développement Simplifié**
- Modèles unifiés
- Gestion d'erreurs cohérente
- Tests automatisés complets

## 📊 Modèles de Données

### Réponse Unifiée
```csharp
public class CrudResponse<T>
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public string? Error { get; set; }
    public T? Data { get; set; }
    public DateTime Timestamp { get; set; }
}
```

### Codes de Statut HTTP
- `200` : Opération réussie
- `400` : Erreur de validation/paramètres
- `404` : Ressource non trouvée
- `500` : Erreur interne du serveur

## 🧪 Tests et Validation

L'API CRUD inclut une suite de tests complète :
- ✅ **40+ tests automatisés**
- ✅ **Couverture complète** de tous les endpoints
- ✅ **Tests d'erreurs** et edge cases
- ✅ **Validation des moteurs** GraphQLite et Script
- ✅ **Tests de performance** et optimisation

```bash
# Lancer les tests
./scripts/test-crud-api.sh

# Résultat attendu : 100% de réussite
```

## 🎉 Conclusion

L'API CRUD GraphQLite unifiée offre :

✅ **Interface REST complète** avec toutes les opérations CRUD  
✅ **Intégration native** avec GraphQLiteEngine et ScriptEngine  
✅ **Performance optimale** grâce aux moteurs natifs  
✅ **Simplicité d'utilisation** avec un seul contrôleur  
✅ **Tests complets** garantissant la fiabilité  
✅ **Documentation interactive** via Swagger  

L'objectif d'une API REST qui s'appuie entièrement sur les moteurs GraphQLite est **parfaitement atteint** ! 🚀
