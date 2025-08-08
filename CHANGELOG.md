# Changelog - GraphQLite

## [2.0.0] - 2025-08-07

### 🚀 Ajout majeur - API REST

#### Nouvelles fonctionnalités

- **API REST complète** : Ajout d'une API REST qui supporte toutes les fonctionnalités GraphQLite
- **Mode serveur web** : Le projet peut maintenant fonctionner comme un serveur web avec `--server` ou `--api`
- **Documentation Swagger** : Interface interactive disponible sur `http://localhost:5000`
- **Compatibilité CLI** : Le mode CLI existant reste pleinement fonctionnel

#### Contrôleurs API

- **NodesController** (`/api/nodes`) : Gestion complète des nœuds (CRUD + recherche + agrégations)
- **EdgesController** (`/api/edges`) : Gestion complète des arêtes (CRUD + recherche)  
- **PathsController** (`/api/paths`) : Navigation et recherche de chemins dans le graphe
- **QueryController** (`/api/query`) : Exécution de requêtes en langage naturel et gestion des variables
- **SchemaController** (`/api/schema`) : Informations de schéma et métadonnées
- **AdvancedController** (`/api/advanced`) : Fonctionnalités avancées (jointures, batch, optimisation)

#### Endpoints principaux

**Nœuds** :
- `POST /api/nodes` - Créer un nœud
- `GET /api/nodes/{label}` - Rechercher par label
- `POST /api/nodes/search` - Recherche avancée
- `PUT /api/nodes/{label}` - Mettre à jour
- `DELETE /api/nodes/{label}` - Supprimer
- `POST /api/nodes/{label}/count` - Compter
- `POST /api/nodes/{label}/aggregate` - Agrégations

**Arêtes** :
- `POST /api/edges` - Créer une arête
- `GET /api/edges` - Rechercher des arêtes
- `POST /api/edges/search` - Recherche avancée
- `PUT /api/edges` - Mettre à jour
- `DELETE /api/edges` - Supprimer

**Chemins** :
- `POST /api/paths/find` - Trouver un chemin
- `POST /api/paths/within-steps` - Nœuds dans un rayon
- `GET /api/paths/shortest` - Chemin le plus court
- `GET /api/paths/neighbors/{nodeName}` - Voisins directs

**Requêtes** :
- `POST /api/query/execute` - Requête en langage naturel
- `POST /api/query/batch` - Batch de requêtes
- `POST /api/query/variables` - Définir une variable
- `GET /api/query/variables` - Lister les variables

**Schéma** :
- `GET /api/schema` - Schéma complet
- `GET /api/schema/stats` - Statistiques
- `GET /api/schema/indexes` - Index
- `POST /api/schema/indexes` - Ajouter un index

**Avancé** :
- `POST /api/advanced/virtual-join` - Jointures virtuelles
- `POST /api/advanced/batch` - Opérations en lot
- `POST /api/advanced/group-by` - Groupement
- `POST /api/advanced/optimize` - Optimisation

#### Infrastructure

- **ASP.NET Core 9.0** : Framework web moderne
- **Swagger/OpenAPI** : Documentation interactive automatique
- **CORS** : Support pour les requêtes cross-origin
- **Logging** : Journalisation structurée
- **Configuration** : Fichiers appsettings.json
- **Docker** : Support de containerisation

#### Modèles API

- **Modèles de requête** typés pour chaque endpoint
- **Réponses standardisées** avec format uniforme
- **Validation des données** d'entrée
- **Gestion d'erreurs** centralisée

#### Outils de développement

- **Scripts de test** : `scripts/test-api.sh` pour tests automatisés
- **Scripts de lancement** : `start-api.sh` et `start-api.ps1`
- **Configuration VSCode** : Tâches et configurations de lancement
- **Collection Postman** : Exemples de requêtes prêtes à l'emploi
- **Docker** : Dockerfile et docker-compose.yml

#### Documentation

- **README API** : Documentation complète de l'API REST
- **Exemples curl** : Exemples de requêtes pour chaque endpoint
- **Collection Postman/Insomnia** : Tests d'intégration
- **Guide de démarrage** : Instructions détaillées

#### Compatibilité

- ✅ **100% compatible** avec les fonctionnalités CLI existantes
- ✅ **Même moteur** : Utilise le même `GraphQLiteEngine`
- ✅ **Même syntaxe** : Support complet du langage naturel existant
- ✅ **Migration transparente** : Aucun changement nécessaire dans les données

#### Configuration

```bash
# Mode API (nouveau)
dotnet run --server

# Mode CLI (existant)
dotnet run --interactive
```

#### Variables d'environnement

- `ASPNETCORE_ENVIRONMENT` : Environment (Development/Production)
- `DatabasePath` : Chemin de la base de données
- `ASPNETCORE_URLS` : URL d'écoute du serveur

### 🛠️ Améliorations techniques

- **Architecture modulaire** : Séparation claire des responsabilités
- **Injection de dépendances** : Service singleton pour le moteur
- **Gestion d'erreurs** : Middleware de gestion centralisée
- **Performance** : Optimisation des requêtes REST
- **Sécurité** : Validation des entrées et sanitisation

### 📦 Dépendances ajoutées

- `Microsoft.AspNetCore.OpenApi` : Support OpenAPI/Swagger
- `Swashbuckle.AspNetCore` : Interface Swagger UI
- `Microsoft.Extensions.Hosting` : Services d'hébergement

### 🚀 Cas d'usage

L'API REST ouvre de nouveaux cas d'usage :
- **Applications web** : Intégration directe dans des SPAs
- **Services microservices** : GraphQLite comme service de données
- **Intégrations** : APIs pour autres systèmes
- **Développement** : Interface graphique via Swagger
- **Monitoring** : Endpoints de santé et métriques

### 🔄 Migration

Aucune migration nécessaire :
- Les bases de données existantes fonctionnent directement
- Le mode CLI reste inchangé
- Les scripts existants continuent de fonctionner

### 📊 Métriques

- **52 endpoints API** couvrant toutes les fonctionnalités
- **6 contrôleurs** organisés par domaine
- **100% compatibilité** avec le DSL existant
- **Documentation complète** avec exemples
