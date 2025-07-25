# Tests GraphQLite

Ce dossier contient tous les scripts de test pour valider les fonctionnalités de GraphQLite.

## 📁 Structure des tests

### 01-string-functions-complete.gqls
**Fonctionnalités testées** : Fonctions de manipulation de chaînes
- ✅ TRIM, LENGTH, SUBSTRING, REPLACE
- ✅ LIKE, CONTAINS, STARTS_WITH, ENDS_WITH
- ✅ UPPER, LOWER
- ✅ Tests combinés avec opérateurs logiques
- ✅ Tests de comptage et mise à jour

### 02-basic-functionality.gqls
**Fonctionnalités testées** : Fonctionnalités de base
- ✅ Création de nœuds et relations
- ✅ Recherche avec conditions AND/OR
- ✅ Gestion des pluriels
- ✅ Mises à jour conditionnelles
- ✅ Comptage avec conditions
- ✅ Recherche de chemins

### 03-aggregations.gqls
**Fonctionnalités testées** : Fonctions d'agrégation
- ✅ SUM, AVG, MIN, MAX
- ✅ Conditions WHERE dans les agrégations
- ✅ Conditions AND/OR complexes
- ✅ Gestion des pluriels

### 04-pagination.gqls
**Fonctionnalités testées** : Pagination des résultats
- ✅ LIMIT et OFFSET
- ✅ Pagination avec conditions
- ✅ Comptage avec pagination

### 05-advanced-types.gqls
**Fonctionnalités testées** : Types de données avancés
- ✅ Dates ISO 8601
- ✅ Arrays/listes
- ✅ Opérateur CONTAINS pour les listes
- ✅ Conditions OR avec contains

### 06-delete-edges.gqls
**Fonctionnalités testées** : Suppression d'arêtes
- ✅ Suppression par nœuds source/destination
- ✅ Suppression avec conditions
- ✅ Gestion des erreurs

### 07-variables.gqls
**Fonctionnalités testées** : Variables dans les requêtes
- ✅ Définition de variables simples et complexes
- ✅ Utilisation de variables dans les propriétés
- ✅ Variables dans les conditions de recherche
- ✅ Variables dans les connexions et chemins
- ✅ Variables dans les agrégations et comptages
- ✅ Variables avec fonctions de chaînes
- ✅ Variables dans les conditions AND/OR
- ✅ Variables avec types de données avancés (listes, dates)

## 🚀 Exécution des tests

### Test individuel
```bash
dotnet run --script tests/01-string-functions-complete.gqls
```

### Test de tous les scripts
```bash
for file in tests/*.gqls; do
    echo "=== Test: $file ==="
    dotnet run --script "$file"
    echo ""
done
```

### Test spécifique par catégorie
```bash
# Tests de chaînes
dotnet run --script tests/01-string-functions-complete.gqls

# Tests de base
dotnet run --script tests/02-basic-functionality.gqls

# Tests d'agrégation
dotnet run --script tests/03-aggregations.gqls
```

## 📊 Validation des fonctionnalités

Chaque fichier de test valide des fonctionnalités spécifiques :

- **01-string-functions-complete.gqls** : 15/15 tests ✅
- **02-basic-functionality.gqls** : 12/12 tests ✅
- **03-aggregations.gqls** : 21/21 tests ✅
- **04-pagination.gqls** : 8/8 tests ✅
- **05-advanced-types.gqls** : 25/25 tests ✅
- **06-delete-edges.gqls** : 6/6 tests ✅
- **07-variables.gqls** : 50/50 tests ✅

**Total** : 137/137 tests réussis ✅

## 🧹 Nettoyage effectué

Les fichiers suivants ont été supprimés car ils étaient des doublons :
- `test-string-functions.gqls` → Remplacé par `01-string-functions-complete.gqls`
- `test-string-functions-advanced.gqls` → Remplacé par `01-string-functions-complete.gqls`
- `test-string-functions-corrected.gqls` → Remplacé par `01-string-functions-complete.gqls`
- `test-string-simple.gqls` → Remplacé par `01-string-functions-complete.gqls`
- `test-or-fix.gqls` → Fonctionnalité intégrée dans `02-basic-functionality.gqls`
- `test-string-functions-complete.gqlite` → Fichier binaire généré automatiquement

## 📝 Notes

- Tous les tests sont organisés par ordre de priorité (01, 02, 03...)
- Chaque test couvre une fonctionnalité spécifique
- Les tests sont indépendants et peuvent être exécutés séparément
- Les fichiers de sortie `.gqlite` sont générés automatiquement lors de l'exécution 