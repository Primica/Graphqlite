# GraphQLite REST API

Cette API REST fournit un accès complet à toutes les fonctionnalités de GraphQLite via des endpoints HTTP standardisés.

## Démarrage

### Mode Serveur API

```bash
dotnet run --server
# ou
dotnet run --api
```

L'API sera disponible sur `http://localhost:5000` avec la documentation Swagger à la racine.

### Mode CLI (existant)

```bash
dotnet run --database mydb.gqlite --interactive
```

## Endpoints API

### 🏥 Santé
- `GET /health` - Vérification de l'état du service

### 📊 Nœuds (`/api/nodes`)
- `POST /api/nodes` - Créer un nœud
- `GET /api/nodes/{label}` - Rechercher des nœuds par label
- `POST /api/nodes/search` - Recherche avancée avec conditions
- `PUT /api/nodes/{label}` - Mettre à jour des nœuds
- `DELETE /api/nodes/{label}` - Supprimer des nœuds
- `POST /api/nodes/{label}/count` - Compter les nœuds
- `POST /api/nodes/{label}/aggregate` - Agrégations sur les nœuds

### 🔗 Arêtes (`/api/edges`)
- `POST /api/edges` - Créer une arête
- `GET /api/edges` - Rechercher des arêtes
- `POST /api/edges/search` - Recherche avancée d'arêtes
- `PUT /api/edges` - Mettre à jour des arêtes
- `DELETE /api/edges` - Supprimer des arêtes
- `POST /api/edges/count` - Compter les arêtes

### 🛤️ Chemins (`/api/paths`)
- `POST /api/paths/find` - Trouver un chemin entre deux nœuds
- `POST /api/paths/within-steps` - Nœuds dans un rayon d'étapes
- `GET /api/paths/shortest` - Chemin le plus court
- `GET /api/paths/neighbors/{nodeName}` - Voisins directs
- `GET /api/paths/distance` - Distance entre nœuds

### 💬 Requêtes (`/api/query`)
- `POST /api/query/execute` - Exécuter une requête en langage naturel
- `POST /api/query/batch` - Exécuter plusieurs requêtes
- `POST /api/query/variables` - Définir une variable
- `GET /api/query/variables` - Lister les variables
- `GET /api/query/variables/{name}` - Récupérer une variable
- `DELETE /api/query/variables/{name}` - Supprimer une variable
- `DELETE /api/query/variables` - Vider toutes les variables
- `POST /api/query/validate` - Valider une requête

### 📋 Schéma (`/api/schema`)
- `GET /api/schema` - Schéma complet de la base
- `GET /api/schema/stats` - Statistiques générales
- `GET /api/schema/indexes` - Propriétés indexées
- `GET /api/schema/indexes/stats` - Statistiques des index
- `POST /api/schema/indexes` - Ajouter un index
- `DELETE /api/schema/indexes` - Supprimer un index
- `GET /api/schema/nodes/labels` - Labels des nœuds
- `GET /api/schema/edges/types` - Types d'arêtes
- `POST /api/schema/backup` - Sauvegarder la base

### 🚀 Fonctionnalités Avancées (`/api/advanced`)
- `POST /api/advanced/virtual-join` - Jointures virtuelles
- `POST /api/advanced/batch` - Opérations en lot
- `POST /api/advanced/group-by` - Groupement de données
- `POST /api/advanced/order-by` - Tri de données
- `POST /api/advanced/optimize` - Optimisation du graphe
- `GET /api/advanced/pagination/{label}` - Pagination intelligente
- `GET /api/advanced/analyze-relations` - Analyse des relations

## Exemples d'utilisation

### Créer un nœud

```bash
curl -X POST http://localhost:5000/api/nodes \
  -H "Content-Type: application/json" \
  -d '{
    "label": "person",
    "properties": {
      "name": "Alice",
      "age": 30,
      "city": "Paris"
    }
  }'
```

### Créer une arête

```bash
curl -X POST http://localhost:5000/api/edges \
  -H "Content-Type: application/json" \
  -d '{
    "fromNode": "Alice",
    "toNode": "Bob",
    "edgeType": "knows",
    "properties": {
      "since": "2020",
      "strength": "strong"
    }
  }'
```

### Rechercher des nœuds

```bash
curl -X POST http://localhost:5000/api/nodes/search \
  -H "Content-Type: application/json" \
  -d '{
    "label": "person",
    "conditions": {
      "city": "Paris",
      "age_gt": "25"
    },
    "limit": 10
  }'
```

### Trouver un chemin

```bash
curl -X POST http://localhost:5000/api/paths/find \
  -H "Content-Type: application/json" \
  -d '{
    "fromNode": "Alice",
    "toNode": "Charlie",
    "maxSteps": 5,
    "isBidirectional": true
  }'
```

### Exécuter une requête en langage naturel

```bash
curl -X POST http://localhost:5000/api/query/execute \
  -H "Content-Type: application/json" \
  -d '{
    "query": "find person where name: \"Alice\""
  }'
```

### Définir une variable

```bash
curl -X POST http://localhost:5000/api/query/variables \
  -H "Content-Type: application/json" \
  -d '{
    "name": "city_filter",
    "value": "Paris"
  }'
```

### Jointure virtuelle

```bash
curl -X POST http://localhost:5000/api/advanced/virtual-join \
  -H "Content-Type: application/json" \
  -d '{
    "sourceLabel": "person",
    "targetLabel": "company",
    "edgeType": "works_at",
    "maxSteps": 2,
    "isBidirectional": false
  }'
```

## Structure des Réponses

Toutes les réponses suivent ce format standardisé :

```json
{
  "success": true,
  "message": "Message descriptif",
  "data": { /* Données de réponse */ },
  "error": null,
  "timestamp": "2023-12-07T10:30:00Z"
}
```

## Codes de Statut HTTP

- `200 OK` - Succès
- `400 Bad Request` - Erreur dans la requête
- `404 Not Found` - Ressource non trouvée
- `500 Internal Server Error` - Erreur interne

## Documentation Interactive

Accédez à `http://localhost:5000` pour consulter la documentation Swagger interactive avec tous les détails des endpoints, modèles de données et exemples.

## Configuration

Le fichier `appsettings.json` permet de configurer :
- Le chemin de la base de données
- Les niveaux de log
- Le port d'écoute

## Compatibilité

L'API REST est entièrement compatible avec le mode CLI existant. Vous pouvez utiliser les deux modes selon vos besoins :
- API REST pour intégrations web et applications
- CLI pour scripts et utilisation interactive

## CORS

L'API supporte CORS pour permettre les requêtes cross-origin depuis des applications web.
