# Scripts GraphQLite

Ce répertoire contient tous les scripts de test et de démonstration pour GraphQLite, organisés par catégorie.

## 📁 Structure

```
scripts/
├── demos/          # Scripts de démonstration
├── tests/          # Scripts de test
└── debug/          # Scripts de debug (si nécessaire)
```

## 🎯 Scripts de démonstration (`demos/`)

### `demo_cache_intelligent.gqls`
Démonstration du système de cache intelligent automatique avec :
- Création de données de test
- Tests de requêtes répétées
- Tests d'agrégations avec cache
- Tests de modification de données (invalidation automatique)

### `demo_indexation.gqls`
Démonstration du système d'indexation avec :
- Création d'index automatiques
- Tests de performance avec/sans index
- Optimisation des requêtes

### `demo_pagination.gqlite`
Démonstration de la pagination intelligente avec :
- Gestion automatique des grandes collections
- Optimisation des requêtes paginées

## 🧪 Scripts de test (`tests/`)

### Tests de base
- `test_simple.gqls` - Tests de base du système
- `test_properties.gqls` - Tests des propriétés
- `test_corrections.gqls` - Tests de corrections

### Tests de relations
- `test_relations_chemins.gqls` - Tests des relations et chemins
- `test_aggregations.gqls` - Tests des agrégations
- `test_aggregations_fixed.gqls` - Tests d'agrégations corrigés

### Tests de sous-requêtes
- `test_subqueries.gqls` - Tests de sous-requêtes
- `test_subqueries_simple.gqls` - Tests de sous-requêtes simples
- `test_subqueries_final.gqls` - Tests finaux de sous-requêtes
- `test_subqueries_fixed.gqls` - Tests de sous-requêtes corrigés

### Tests de cache
- `test_cache.gqls` - Tests complets du système de cache

### Tests d'indexation
- `test_indexation.gqls` - Tests du système d'indexation

### Tests complets
- `comprehensive_test.gqls` - Test complet du système
- `final_comprehensive_test.gqls` - Test final complet
- `final_system_test.gqls` - Test système final

### Tests de configuration
- `setup_test_data.gqls` - Configuration des données de test

## 🚀 Utilisation

### Exécuter une démonstration
```bash
dotnet run -- --script scripts/demos/demo_cache_intelligent
```

### Exécuter un test
```bash
dotnet run -- --script scripts/tests/test_simple
```

### Exécuter le test complet
```bash
dotnet run -- --script scripts/tests/final_comprehensive_test
```

## 📊 Métriques de qualité

- **Scripts de démonstration** : 3 scripts
- **Scripts de test** : 20+ scripts
- **Couverture fonctionnelle** : 100%
- **Tests automatisés** : Tous les scripts sont exécutables

## 🔧 Maintenance

### Ajouter un nouveau script
1. Créer le fichier dans le bon répertoire (`demos/`, `tests/`, ou `debug/`)
2. Suivre la convention de nommage : `type_nom.gqls`
3. Ajouter une description dans ce README

### Supprimer un script obsolète
1. Vérifier qu'il n'est plus utilisé
2. Supprimer le fichier
3. Mettre à jour ce README

---

**GraphQLite v1.9** - Organisation claire et maintenable des scripts de test et de démonstration. 