# Sous-requêtes Complexes dans GraphQLite

GraphQLite supporte maintenant des sous-requêtes complexes avec les opérateurs EXISTS, NOT EXISTS, IN, NOT IN et des agrégations avancées.

## Opérateurs Supportés

### 1. EXISTS et NOT EXISTS

#### Syntaxe EXISTS
```sql
find [label] where [property] exists in (select [property] from [label] where [conditions])
find [label] where [property] exists where [conditions]
```

#### Exemples EXISTS
```sql
-- Trouver les personnes qui travaillent sur des projets actifs
find persons where department exists in (select name from projects where status = 'active')

-- Trouver les personnes qui ont des connexions via des arêtes spécifiques
find persons where name exists where age > 30 and salary > 50000
```

#### Syntaxe NOT EXISTS
```sql
find [label] where [property] not exists in (select [property] from [label] where [conditions])
find [label] where [property] not exists where [conditions]
```

#### Exemples NOT EXISTS
```sql
-- Trouver les personnes qui ne travaillent sur aucun projet
find persons where name not exists in (select from_node from edges where type = 'works_on')

-- Trouver les projets sans employés assignés
find projects where name not exists in (select to_node from edges where type = 'works_on')
```

### 2. IN et NOT IN

#### Syntaxe IN
```sql
find [label] where [property] in (select [property] from [label] where [conditions])
find [label] where [property] in select [property] from [label] where [conditions]
```

#### Exemples IN
```sql
-- Trouver les personnes dont le salaire est dans la liste des budgets de projets
find persons where salary in select budget from projects

-- Trouver les projets avec des budgets dans une plage spécifique
find projects where budget in (select salary from persons where department = 'IT')
```

#### Syntaxe NOT IN
```sql
find [label] where [property] not in (select [property] from [label] where [conditions])
find [label] where [property] not in select [property] from [label] where [conditions]
```

#### Exemples NOT IN
```sql
-- Trouver les personnes qui ne travaillent pas sur des projets avec un budget > 100000
find persons where name not in select from_node from edges where to_node in (select name from projects where budget > 100000)

-- Trouver les départements sans employés seniors
find persons where department not in (select department from persons where age > 35)
```

### 3. Agrégations dans les Conditions

#### Syntaxe des Agrégations
```sql
find [label] where [property] [operator] [aggregate_function] [property] from [label] where [conditions]
```

#### Opérateurs de Comparaison Supportés
- `eq` : égal à
- `gt` : supérieur à
- `lt` : inférieur à
- `gte` : supérieur ou égal à
- `lte` : inférieur ou égal à

#### Fonctions d'Agrégation Supportées
- `sum` : somme
- `avg` : moyenne
- `min` : minimum
- `max` : maximum
- `count` : comptage

#### Exemples d'Agrégations
```sql
-- Trouver les personnes dont le salaire est supérieur à la moyenne
find persons where salary gt avg salary from persons

-- Trouver les projets avec un budget supérieur au salaire maximum
find projects where budget gt max salary from persons

-- Trouver les départements avec plus d'employés que la moyenne
find persons where department count gt avg count department from persons
```

### 4. Opérateurs ANY et ALL

#### Syntaxe ANY
```sql
find [label] where [property] any in (select [property] from [label] where [conditions])
```

#### Exemples ANY
```sql
-- Trouver les personnes dont au moins une propriété correspond aux valeurs de projets
find persons where age any in (select budget from projects)

-- Trouver les projets dont au moins un attribut correspond aux salaires
find projects where budget any in (select salary from persons)
```

#### Syntaxe ALL
```sql
find [label] where [property] all in (select [property] from [label] where [conditions])
```

#### Exemples ALL
```sql
-- Trouver les personnes dont toutes les propriétés numériques sont dans les budgets
find persons where salary all in (select budget from projects)

-- Trouver les projets dont tous les attributs correspondent aux critères
find projects where budget all in (select salary from persons where age > 30)
```

## Sous-requêtes Imbriquées

GraphQLite supporte les sous-requêtes imbriquées pour des requêtes complexes :

```sql
-- Sous-requête imbriquée avec agrégation
find persons where department in (select department from persons where salary > (select avg salary from persons))

-- Sous-requête imbriquée avec conditions multiples
find projects where budget in (select salary from persons where age > (select avg age from persons) and department = 'IT')
```

## Conditions Complexes avec Sous-requêtes

Vous pouvez combiner plusieurs sous-requêtes dans une seule condition :

```sql
-- Multiple agrégations
find persons where salary gt min budget from projects and salary lt max budget from projects

-- Conditions mixtes avec sous-requêtes
find persons where age > 25 and salary in (select budget from projects where status = 'active' and budget > 90000)
```

## Conditions sur les Arêtes avec Sous-requêtes

```sql
-- Trouver les personnes qui travaillent sur des projets avec des conditions spécifiques
find persons where name exists in (select from_node from edges where type = 'works_on' and hours > 30)

-- Trouver les projets avec des employés seniors
find projects where name in (select to_node from edges where from_node in (select name from persons where age > 35))
```

## Bonnes Pratiques

### 1. Performance
- Utilisez des index sur les propriétés fréquemment utilisées dans les sous-requêtes
- Évitez les sous-requêtes très profondes (plus de 3 niveaux)
- Préférez EXISTS à IN pour les grandes tables

### 2. Lisibilité
- Utilisez des alias pour les sous-requêtes complexes
- Structurez vos requêtes avec des commentaires
- Testez les sous-requêtes séparément avant de les intégrer

### 3. Validation
- Vérifiez que les types de données correspondent entre les requêtes principales et les sous-requêtes
- Assurez-vous que les propriétés référencées existent dans les nœuds

## Exemples Complets

### Exemple 1 : Analyse des Employés par Département
```sql
-- Trouver les départements avec des employés bien payés
find persons where department in (select department from persons where salary > (select avg salary from persons))

-- Trouver les employés seniors dans des départements performants
find persons where age > 30 and department exists in (select department from persons where salary gt avg salary from persons)
```

### Exemple 2 : Analyse des Projets
```sql
-- Trouver les projets avec des budgets dans la moyenne des salaires
find projects where budget in (select salary from persons where age between 25 and 40)

-- Trouver les projets actifs avec des employés expérimentés
find projects where status = 'active' and name in (select to_node from edges where from_node in (select name from persons where age > 30))
```

### Exemple 3 : Requêtes Analytiques
```sql
-- Trouver les départements avec des salaires au-dessus de la moyenne
find persons where department in (select department from persons group by department having avg salary > (select avg salary from persons))

-- Trouver les projets avec des budgets supérieurs à la moyenne des budgets
find projects where budget gt avg budget from projects
```

## Limitations Actuelles

1. **Profondeur maximale** : Les sous-requêtes sont limitées à 5 niveaux de profondeur
2. **Performance** : Les sous-requêtes complexes peuvent être lentes sur de grandes bases de données
3. **Types de données** : Certains types complexes ne sont pas encore supportés dans toutes les comparaisons

## Évolutions Futures

- Support des sous-requêtes corrélées
- Optimisation automatique des requêtes
- Support des vues et des requêtes CTE (Common Table Expressions)
- Agrégations plus avancées (percentiles, médianes, etc.) 