# Jointures Virtuelles dans GraphQLite

## Vue d'ensemble

Les jointures virtuelles permettent de connecter des nœuds de différents types en utilisant des relations existantes ou des propriétés communes, sans créer de nouvelles arêtes physiques dans le graphe.

## Syntaxe

### 1. Jointure via type d'arête

```gqls
join persons with projects via works_on
```

Cette commande trouve tous les projets connectés aux personnes via des arêtes de type `works_on`.

### 2. Jointure avec conditions

```gqls
virtual join persons and projects where department = 'IT'
```

Cette commande joint les personnes et les projets en appliquant des conditions sur les propriétés.

### 3. Jointure sur propriété commune

```gqls
merge persons with companies on company_id
```

Cette commande joint les personnes et les entreprises en utilisant la propriété `company_id` comme clé de jointure.

### 4. Jointure avec rayon de pas

```gqls
join persons with projects within 2 steps
```

Cette commande trouve tous les projets accessibles depuis les personnes en traversant jusqu'à 2 arêtes.

### 5. Jointure bidirectionnelle

```gqls
virtual join persons and companies bidirectional
```

Cette commande effectue une jointure bidirectionnelle entre les personnes et les entreprises.

### 6. Jointure avec agrégation

```gqls
join persons with projects via works_on where budget > 40000
```

Cette commande joint les personnes et les projets en appliquant des conditions sur les propriétés des projets.

## Types de jointures supportés

### Jointure INNER (par défaut)
- Retourne seulement les paires de nœuds qui ont une relation
- Syntaxe : `join`, `virtual join`, `merge`

### Jointure bidirectionnelle
- Explore les relations dans les deux sens
- Syntaxe : `bidirectional`

## Opérateurs de comparaison

Les jointures virtuelles supportent les opérateurs suivants pour les conditions :

- `=` : Égalité
- `>` : Supérieur à
- `<` : Inférieur à
- `>=` : Supérieur ou égal à
- `<=` : Inférieur ou égal à
- `!=` : Différent de

## Exemples d'utilisation

### Exemple 1 : Jointure simple
```gqls
# Créer des données
create person "Alice" with properties {name: "Alice", department: "IT"};
create company "TechCorp" with properties {name: "TechCorp", industry: "Technology"};

# Créer une relation
create edge from "alice" to "techcorp" with type works_at;

# Jointure virtuelle
virtual join persons and companies;
```

### Exemple 2 : Jointure avec conditions
```gqls
# Jointure avec filtrage
virtual join persons and projects where department = 'IT' and budget > 50000;
```

### Exemple 3 : Jointure sur propriété commune
```gqls
# Créer des données avec propriété commune
create person "Bob" with properties {name: "Bob", company_id: 1};
create company "DataSoft" with properties {name: "DataSoft", company_id: 1};

# Jointure sur propriété commune
merge persons with companies on company_id;
```

## Résultats

Les jointures virtuelles retournent des objets contenant :

- `source_node` : Le nœud source
- `target_node` : Le nœud cible
- `source_label` : Le label du nœud source
- `target_label` : Le label du nœud cible
- `join_type` : Le type de jointure utilisé
- `source_[property]` : Les propriétés du nœud source
- `target_[property]` : Les propriétés du nœud cible

## Limitations actuelles

1. **Noms de nœuds avec espaces** : Les nœuds avec des noms contenant des espaces peuvent causer des problèmes lors de la création d'arêtes
2. **Conditions complexes** : Les conditions avec plusieurs opérateurs logiques peuvent nécessiter une syntaxe spécifique
3. **Performance** : Les jointures sur de grands graphes peuvent être lentes

## Bonnes pratiques

1. **Utilisez des noms de nœuds simples** : Évitez les espaces dans les noms de nœuds
2. **Définissez des propriétés communes** : Utilisez des identifiants communs pour faciliter les jointures
3. **Optimisez les conditions** : Utilisez des conditions spécifiques pour améliorer les performances
4. **Testez avec de petits ensembles** : Validez vos jointures sur de petits ensembles de données avant de les appliquer à de grands graphes 