# Système d'Indexation - GraphQLite

## Vue d'ensemble

Le système d'indexation de GraphQLite permet d'accélérer les requêtes sur les propriétés fréquemment utilisées en maintenant automatiquement des index en mémoire pour ces propriétés.

## Propriétés indexées automatiquement

Les propriétés suivantes sont automatiquement indexées :

- `name` - Nom des nœuds
- `department` - Département des personnes
- `role` - Rôle des personnes
- `salary` - Salaire des personnes
- `age` - Âge des personnes
- `industry` - Industrie des entreprises
- `status` - Statut des projets
- `location` - Localisation
- `city` - Ville

## Commandes d'indexation

### Afficher les propriétés indexées

```gqls
show indexed properties;
```

### Afficher les statistiques d'index

```gqls
show index stats;
```

### Ajouter une propriété à l'index

```gqls
add index property "experience";
```

### Supprimer une propriété de l'index

```gqls
remove index property "city";
```

## Fonctionnement technique

### Structure des index

Les index sont organisés en trois niveaux :
1. **Label** (ex: "person", "company")
2. **Propriété** (ex: "name", "department")
3. **Valeur** → **Ensemble d'IDs de nœuds**

### Mise à jour automatique

- **Création de nœud** : Les propriétés indexées sont automatiquement ajoutées aux index
- **Mise à jour de nœud** : Les anciennes valeurs sont supprimées et les nouvelles ajoutées
- **Suppression de nœud** : Le nœud est supprimé de tous les index

### Recherche optimisée

Quand une requête utilise une propriété indexée, le système :
1. Vérifie si la propriété est indexée
2. Récupère directement les IDs des nœuds correspondants
3. Évite le scan complet de tous les nœuds

## Avantages

### Performance
- **Recherche O(1)** au lieu de O(n) pour les propriétés indexées
- **Accélération significative** pour les grandes bases de données
- **Mémoire optimisée** avec des structures de données concurrentes

### Flexibilité
- **Index dynamiques** : Ajout/suppression de propriétés à la volée
- **Reconstruction automatique** lors du chargement de la base
- **Compatibilité** avec toutes les fonctionnalités existantes

### Transparence
- **Aucune modification** des requêtes existantes
- **Fonctionnement automatique** sans intervention utilisateur
- **Rétrocompatibilité** complète

## Exemples d'utilisation

### Recherche simple indexée

```gqls
# Recherche rapide par nom (indexée)
find all persons where name = "Alice";

# Recherche rapide par département (indexée)
find all persons where department = "Engineering";
```

### Recherche avec comparaison

```gqls
# Recherche rapide par salaire (indexée)
find all persons where salary > 70000;

# Recherche rapide par âge (indexée)
find all persons where age > 30;
```

### Recherche multiple

```gqls
# Recherche rapide avec plusieurs conditions indexées
find all persons where department = "Engineering" and role = "developer";
```

### Agrégation sur propriétés indexées

```gqls
# Agrégation rapide sur propriété indexée
avg persons property salary where department = "Engineering";
```

## Gestion des index

### Ajout d'une nouvelle propriété

```gqls
# Ajouter "experience" à l'index automatique
add index property "experience";

# Créer un nœud avec la propriété indexée
create person with name "John" and experience 5;

# Recherche rapide sur la nouvelle propriété
find all persons where experience > 3;
```

### Suppression d'une propriété

```gqls
# Supprimer "city" de l'index automatique
remove index property "city";
```

## Statistiques et monitoring

### Affichage des propriétés indexées

```gqls
show indexed properties;
```

Résultat :
```
Propriétés indexées automatiquement :
- name
- department
- role
- salary
- age
- industry
- status
- location
- city
- experience
```

### Affichage des statistiques

```gqls
show index stats;
```

Résultat :
```
Statistiques des index :
- Total d'index : 4
- Propriétés indexées automatiquement : 10
- Statistiques par label :
  * person : 5 propriétés indexées, 6 nœuds indexés
  * company : 3 propriétés indexées, 2 nœuds indexés
```

## Implémentation technique

### Classes principales

- **`IndexManager`** : Gestionnaire principal des index
- **`GraphStorage`** : Intégration avec le stockage
- **`GraphQLiteEngine`** : Intégration avec le moteur de requêtes

### Structures de données

```csharp
// Index par label et propriété
ConcurrentDictionary<string, ConcurrentDictionary<string, ConcurrentDictionary<object, HashSet<Guid>>>>

// Statistiques d'utilisation
ConcurrentDictionary<string, ConcurrentDictionary<string, int>>
```

### Thread-safety

- **ConcurrentDictionary** pour la thread-safety
- **Locks** pour les opérations critiques
- **Support multi-thread** complet

## Bonnes pratiques

### Propriétés à indexer

Indexez les propriétés qui sont :
- **Fréquemment utilisées** dans les clauses WHERE
- **Utilisées pour les jointures** ou relations
- **Utilisées pour le tri** ou l'agrégation
- **Cardinalité élevée** (beaucoup de valeurs différentes)

### Propriétés à ne pas indexer

Évitez d'indexer :
- **Propriétés rares** ou peu utilisées
- **Propriétés avec cardinalité très faible** (peu de valeurs)
- **Propriétés très volumineuses** (textes longs, objets complexes)

### Monitoring

Surveillez régulièrement :
- **Taille des index** en mémoire
- **Performance des requêtes** avec/sans index
- **Utilisation des propriétés** indexées

## Limitations actuelles

- **Index en mémoire uniquement** (pas de persistance)
- **Pas d'index composites** (multi-colonnes)
- **Pas d'index partiels** ou conditionnels
- **Pas d'optimisation automatique** des requêtes

## Évolutions futures

- **Persistance des index** sur disque
- **Index composites** pour plusieurs propriétés
- **Index conditionnels** basés sur des filtres
- **Optimisation automatique** des requêtes
- **Statistiques détaillées** d'utilisation
- **Compression des index** pour économiser la mémoire 