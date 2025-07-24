# GraphQLite

Une base de données orientée graphe simple avec un DSL en langage naturel, conçue pour les développeurs qui trouvent Cypher/Gremlin trop complexes.

## 🚀 Caractéristiques

- **DSL en langage naturel** : Requêtes proches de l'anglais courant
- **Stockage local** : Fichiers `.gqlite` en format binaire optimisé
- **Architecture modulaire** : Séparation claire entre modèles, stockage, requêtes et moteur
- **Interface console interactive** : Testez vos requêtes en temps réel
- **Support de scripts** : Exécution de fichiers `.gqls` avec requêtes multi-lignes
- **Conditions multi-critères** : Support des opérateurs logiques AND/OR
- **Visualisation de schéma** : Analyse automatique de la structure des données
- **Gestion flexible des bases** : Sélection de fichiers de base de données via CLI

## 📁 Structure du projet

```
Graphqlite/
├── Models/
│   ├── Node.cs           # Modèle des nœuds du graphe
│   ├── Edge.cs           # Modèle des arêtes (relations)
│   └── Schema.cs         # Structures pour l'analyse de schéma
├── Storage/
│   └── GraphStorage.cs   # Gestionnaire de persistance avec chargement intelligent
├── Query/
│   ├── ParsedQuery.cs    # Structure des requêtes parsées
│   └── NaturalLanguageParser.cs  # Parser DSL avec support multi-conditions
├── Engine/
│   └── GraphQLiteEngine.cs  # Moteur principal avec algorithmes de graphe
├── Scripting/
│   └── ScriptEngine.cs   # Moteur d'exécution de scripts .gqls
└── Program.cs            # Interface CLI avec gestion d'arguments
```

## 🏗️ Installation et exécution

```bash
# Cloner le projet
cd /path/to/Graphqlite

# Restaurer les dépendances
dotnet restore

# Compiler le projet
dotnet build

# Exécuter l'application
dotnet run
```

## 🔧 Options de ligne de commande

```bash
# Mode interactif par défaut
dotnet run

# Spécifier une base de données
dotnet run -- --db myproject
dotnet run -- -d /path/to/database

# Exécuter un script
dotnet run -- --script myscript
dotnet run -- -s /path/to/script.gqls

# Combiner base et script
dotnet run -- --db production --script init

# Afficher l'aide
dotnet run -- --help
```

### Comportement intelligent des scripts
- **Sans DB spécifiée** : `--script example` crée et utilise `example.gqlite`
- **Avec DB spécifiée** : `--db mydb --script example` utilise `mydb.gqlite`

## 📖 Syntaxe du DSL

### Création de nœuds
```gqls
create person with name John and age 30
create company with name Acme and industry tech and employees 500
create product with name iPhone and price 999.99 and available true
```

### Création de relations
```gqls
connect John to Acme with relationship works_at
connect Acme to iPhone with relationship produces
connect John to iPhone with relationship uses
```

### Recherche de nœuds

#### Recherche simple
```gqls
find all persons
find all companies where industry = tech
find persons where age > 25 limit 10
```

#### Conditions multi-critères
```gqls
# Opérateur AND (toutes les conditions doivent être vraies)
find persons where age > 25 and role = developer
find companies where industry = tech and employees > 100

# Opérateur OR (au moins une condition doit être vraie)
find persons where age < 30 or role = manager
find products where price < 100 or available = true

# Conditions mixtes
find persons where age > 18 and role = developer or role = manager
```

### Recherche avec limitation d'étapes
```gqls
# Trouve tous les nœuds du type spécifié dans un rayon limité
find persons from John over 2 steps
find companies from Alice over 3 steps

# Recherche d'un nœud spécifique via un chemin limité
find managers from John to CEO over 4 steps
```

### Recherche de chemins
```gqls
find path from John to Mary
find path from Acme to iPhone
```

### Mise à jour
```gqls
update person set age 31 where name = John
update company set employees 150 where name = Acme
update person set role senior and salary 75000 where age > 30 and experience > 5
```

### Comptage
```gqls
count persons
count persons where age > 18
count companies where industry = tech and employees > 50
```

### Suppression
```gqls
delete person where name = John
delete company where employees < 10
```

### Visualisation du schéma
```gqls
show schema
describe schema
```

## 📝 Scripts (.gqls)

### Format des fichiers script

Les scripts GraphQLite utilisent l'extension `.gqls` et supportent :

- **Requêtes multi-lignes** : Une requête peut s'étendre sur plusieurs lignes
- **Séparateur de requêtes** : Utilisez `;` pour terminer une requête
- **Commentaires** : `#` ou `//` pour les commentaires
- **Conditions complexes** : Support complet des opérateurs AND/OR

### Exemple de script complet

```gqls
# Script d'initialisation d'un réseau social
// Création des utilisateurs de base

create person with name Alice and age 28 and role developer;
create person with name Bob and age 32 and role manager;
create person with name Charlie and age 25 and role designer;

// Création d'entreprises
create company with name TechCorp 
    and industry software 
    and size large 
    and founded 2010;

create company with name StartupInc 
    and industry tech 
    and size small;

// Relations professionnelles
connect Alice to TechCorp with relationship works_at;
connect Bob to TechCorp with relationship manages;
connect Charlie to StartupInc with relationship works_at;

// Relations personnelles
connect Alice to Bob with relationship knows;
connect Bob to Charlie with relationship mentors;

// Requêtes d'analyse
find all persons where age > 25 and role = developer;
find all companies where industry = tech or size = large;

// Recherches de réseau
find persons from Alice over 2 steps;
find path from Alice to Charlie;

// Mise à jour en lot
update person 
set experience senior 
where age > 30 and role = developer;

// Statistiques finales
count persons where age > 25;
show schema;
```

### Exécution de scripts

```bash
# Exécution avec base auto-générée
dotnet run -- --script social-network
# Crée et utilise social-network.gqlite

# Exécution sur base existante
dotnet run -- --db production --script migration
# Exécute migration.gqls sur production.gqlite
```

## 🎯 Concepts clés

### Nœuds (Nodes)
- Entités du graphe avec un **label** et des **propriétés**
- Chaque nœud a un **ID unique** (GUID)
- Support des types de données : `string`, `int`, `double`, `bool`
- **Timestamps automatiques** : `CreatedAt`, `UpdatedAt`

### Arêtes (Edges)
- Relations **directionnelles** entre deux nœuds
- Possèdent un **type de relation** et des **propriétés optionnelles**
- Permettent la **navigation dans le graphe**
- **Validation d'intégrité** : Les nœuds source/destination doivent exister

### DSL (Domain Specific Language)
- **Syntaxe proche de l'anglais naturel**
- **Mots-clés intuitifs** : `create`, `find`, `connect`, `update`, `delete`, `count`
- **Opérateurs de comparaison** : `=`, `!=`, `>`, `<`, `>=`, `<=`
- **Opérateurs logiques** : `and`, `or`
- **Modificateurs** : `all`, `where`, `limit`, `over X steps`

## 🔧 Architecture technique

### Modèles de données
- **Node** : Entité avec ID, label, propriétés et métadonnées temporelles
- **Edge** : Relation avec IDs source/destination, type et propriétés
- **DatabaseSchema** : Structure d'analyse automatique du schéma

### Stockage intelligent
- **GraphStorage** : Persistance thread-safe en fichier JSON
- **Chargement automatique** : Détection et chargement des bases existantes
- **Sauvegarde incrémentale** : Sauvegarde automatique après modifications
- **Gestion d'erreurs** : Validation et récupération des fichiers corrompus

### Moteur de requêtes avancé
- **NaturalLanguageParser** : Parser regex avec support multi-conditions
- **GraphQLiteEngine** : Orchestration avec algorithmes de graphe optimisés
- **Algorithmes BFS** : Recherche de chemins et limitation d'étapes
- **Évaluation logique** : Support complet des expressions AND/OR

### Système de scripts
- **ScriptEngine** : Exécution de fichiers `.gqls` avec gestion d'erreurs
- **Parsing multi-ligne** : Support des requêtes complexes
- **Rapport d'exécution** : Suivi détaillé du succès/échec de chaque requête

## 🌟 Avantages par rapport à Cypher/Gremlin

1. **Simplicité** : Syntaxe proche du langage naturel, apprentissage intuitif
2. **Courbe d'apprentissage douce** : Pas de syntaxe complexe à mémoriser
3. **Léger et autonome** : Aucun serveur externe requis
4. **Déploiement simple** : Un seul exécutable .NET
5. **Scripts intégrés** : Automatisation native avec `.gqls`
6. **Multi-conditions** : Logique complexe sans syntaxe obscure
7. **Schéma automatique** : Analyse et visualisation intégrées

## 🚀 Cas d'usage pratiques

### Développement et prototypage
```bash
# Création rapide d'un prototype
dotnet run -- --script prototype-social
# Analyse immédiate
dotnet run -- --db prototype-social
GraphQLite> show schema
```

### Tests et validation
```bash
# Script de test avec validation
dotnet run -- --script test-cases
# Base dédiée aux tests
dotnet run -- --db test-data --script validation
```

### Migration et setup
```bash
# Setup initial d'un projet
dotnet run -- --script setup-ecommerce
# Migration vers production
dotnet run -- --db production --script migration-v2
```

### Analyse de données
```bash
# Mode interactif pour exploration
dotnet run -- --db analytics
GraphQLite> find all users where activity > 100 and region = europe
GraphQLite> find path from user123 to purchase456
```

## 📊 Exemple de session interactive

```bash
$ dotnet run -- --db demo

GraphQLite - Base de données orientée graphe
DSL en langage naturel

Fichier : /Users/developer/demo.gqlite
Nouvelle base de données créée

GraphQLite est prêt. Tapez 'help' pour voir les commandes ou 'exit' pour quitter.

GraphQLite> create person with name Alice and age 28 and role developer
Nœud créé avec l'ID : 550e8400-e29b-41d4-a716-446655440000

GraphQLite> create company with name TechCorp and industry software
Nœud créé avec l'ID : 550e8400-e29b-41d4-a716-446655440001

GraphQLite> connect Alice to TechCorp with relationship works_at
Arête créée avec l'ID : 550e8400-e29b-41d4-a716-446655440002

GraphQLite> find all persons where age > 25 and role = developer
1 nœud(s) trouvé(s)

Nœuds trouvés :
  Node(person) [name: Alice, age: 28, role: developer]

GraphQLite> show schema

Schéma généré le 2025-07-24 15:30:22
Total : 2 nœuds, 1 arêtes

NŒUDS :
  person (1 instances)
    Créé: 2025-07-24, Modifié: 2025-07-24
    Propriétés :
      age: Int32 (1/1) (ex: 28)
      name: String (1/1) (ex: Alice)
      role: String (1/1) (ex: developer)

  company (1 instances)
    Créé: 2025-07-24, Modifié: 2025-07-24
    Propriétés :
      industry: String (1/1) (ex: software)
      name: String (1/1) (ex: TechCorp)

ARÊTES :
  works_at (1 relations)
    Créé: 2025-07-24, Modifié: 2025-07-24

GraphQLite> exit
Au revoir !
```

## 📝 Roadmap et extensions possibles

### Fonctionnalités avancées
- **Agrégations** : `sum`, `avg`, `min`, `max` sur les propriétés
- **Jointures complexes** : Relations multi-niveaux
- **Index et optimisations** : Performance sur grandes données
- **Transactions** : Support ACID pour les opérations critiques

### Intégrations
- **API REST** : Interface HTTP pour applications web
- **Export/Import** : CSV, JSON, GraphML
- **Visualisation** : Génération de graphiques SVG/PNG
- **Connecteurs** : Import depuis SQL, Neo4j, etc.

### Outils de développement
- **Extension VS Code** : Coloration syntaxique pour `.gqls`
- **Debugger** : Exécution pas à pas des scripts
- **Profiler** : Analyse de performance des requêtes
- **Tests unitaires** : Framework de test intégré

## 🤝 Contribution

GraphQLite est conçu comme une base solide et extensible. Domaines de contribution :

- **Parser DSL** : Nouvelles syntaxes et mots-clés
- **Algorithmes de graphe** : Optimisations et nouveaux parcours
- **Formats de stockage** : Compression, chiffrement
- **Interface utilisateur** : GUI, web interface
- **Documentation** : Tutoriels et guides avancés

## 📄 Licence

Projet open source conçu pour simplifier l'usage des bases de données orientées graphe.

---

**GraphQLite** - Parce que les graphes ne devraient pas être compliqués.
