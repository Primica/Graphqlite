# Exemples d'Utilisation de l'API CRUD GraphQLite

## 🏗️ Scénario : Gestion d'une Entreprise Tech

### 1. Création de la Structure de Base

#### Créer des employés
```bash
# Créer le CEO
curl -X POST http://localhost:5000/api/nodes \
  -H "Content-Type: application/json" \
  -d '{
    "label": "Person", 
    "properties": {
      "name": "Sarah Johnson",
      "role": "CEO",
      "department": "Executive",
      "salary": 200000,
      "experience": 15
    }
  }'

# Créer des développeurs en batch
curl -X POST http://localhost:5000/api/nodes/batch \
  -H "Content-Type: application/json" \
  -d '{
    "nodes": [
      {
        "label": "Person",
        "properties": {
          "name": "John Smith",
          "role": "Senior Developer",
          "department": "Engineering",
          "salary": 90000,
          "skills": "React,Node.js,Python"
        }
      },
      {
        "label": "Person", 
        "properties": {
          "name": "Emma Davis",
          "role": "Frontend Developer", 
          "department": "Engineering",
          "salary": 75000,
          "skills": "React,TypeScript,CSS"
        }
      },
      {
        "label": "Person",
        "properties": {
          "name": "Mike Wilson",
          "role": "DevOps Engineer",
          "department": "Infrastructure", 
          "salary": 85000,
          "skills": "Docker,Kubernetes,AWS"
        }
      }
    ]
  }'
```

#### Créer des projets
```bash
# Projet principal
curl -X POST http://localhost:5000/api/nodes \
  -H "Content-Type: application/json" \
  -d '{
    "label": "Project",
    "properties": {
      "name": "GraphQLite API",
      "status": "active", 
      "priority": "high",
      "budget": 500000,
      "deadline": "2024-12-31"
    }
  }'

# Projet secondaire  
curl -X POST http://localhost:5000/api/nodes \
  -H "Content-Type: application/json" \
  -d '{
    "label": "Project",
    "properties": {
      "name": "Mobile App",
      "status": "planning",
      "priority": "medium", 
      "budget": 300000,
      "deadline": "2025-06-30"
    }
  }'
```

### 2. Création des Relations

#### Relations hiérarchiques
```bash
# CEO manage les développeurs
curl -X POST http://localhost:5000/api/edges \
  -H "Content-Type: application/json" \
  -d '{
    "fromNode": "Sarah Johnson",
    "toNode": "John Smith", 
    "edgeType": "manages",
    "properties": {
      "since": "2023-01-01",
      "direct_report": true
    }
  }'

# Développeurs travaillent sur des projets
curl -X POST http://localhost:5000/api/edges \
  -H "Content-Type: application/json" \
  -d '{
    "fromNode": "John Smith",
    "toNode": "GraphQLite API",
    "edgeType": "works_on", 
    "properties": {
      "role": "Lead Developer",
      "allocation": 100,
      "start_date": "2024-01-01"
    }
  }'

curl -X POST http://localhost:5000/api/edges \
  -H "Content-Type: application/json" \
  -d '{
    "fromNode": "Emma Davis", 
    "toNode": "GraphQLite API",
    "edgeType": "works_on",
    "properties": {
      "role": "Frontend Lead",
      "allocation": 80,
      "start_date": "2024-02-01"
    }
  }'
```

### 3. Requêtes et Recherches

#### Recherches simples
```bash
# Tous les développeurs
curl "http://localhost:5000/api/nodes?label=Person&conditions[department]=Engineering"

# Projets actifs
curl "http://localhost:5000/api/nodes?label=Project&conditions[status]=active"

# Employés avec salaire > 80000
curl -X POST http://localhost:5000/api/query \
  -H "Content-Type: application/json" \
  -d '{"query": "find Person where salary > 80000"}'
```

#### Recherche de chemins
```bash
# Chemin de Sarah à un projet
curl "http://localhost:5000/api/paths?fromNode=Sarah Johnson&toNode=GraphQLite API&maxSteps=3"

# Relations entre employés
curl "http://localhost:5000/api/edges?fromNode=Sarah Johnson&edgeType=manages"
```

### 4. Analyses et Agrégations

#### Statistiques salariales
```bash
# Salaire moyen
curl -X POST http://localhost:5000/api/aggregate \
  -H "Content-Type: application/json" \
  -d '{
    "label": "Person",
    "function": "avg", 
    "property": "salary"
  }'

# Salaire maximum
curl -X POST http://localhost:5000/api/aggregate \
  -H "Content-Type: application/json" \
  -d '{
    "label": "Person",
    "function": "max",
    "property": "salary"
  }'

# Nombre de développeurs
curl -X POST http://localhost:5000/api/aggregate \
  -H "Content-Type: application/json" \
  -d '{
    "label": "Person", 
    "function": "count",
    "property": "*",
    "conditions": {"department": "Engineering"}
  }'
```

### 5. Mises à Jour

#### Augmentation de salaire
```bash
# Augmenter John Smith
curl -X PUT http://localhost:5000/api/nodes \
  -H "Content-Type: application/json" \
  -d '{
    "label": "Person",
    "properties": {
      "salary": 95000,
      "role": "Principal Developer"
    },
    "conditions": {
      "name": "John Smith"
    }
  }'

# Mettre à jour le statut d'un projet
curl -X PUT http://localhost:5000/api/nodes \
  -H "Content-Type: application/json" \
  -d '{
    "label": "Project", 
    "properties": {
      "status": "completed",
      "completion_date": "2024-11-30"
    },
    "conditions": {
      "name": "GraphQLite API"
    }
  }'
```

### 6. Scripts Avancés

#### Script de reporting
```bash
# Créer un script de rapport
cat > company_report.gqls << 'EOF'
// Rapport d'entreprise automatique
echo "=== RAPPORT D'ENTREPRISE ==="

echo "1. Statistiques générales"
count Person
count Project

echo "2. Répartition par département" 
find Person where department="Engineering"
find Person where department="Executive"

echo "3. Projets en cours"
find Project where status="active"

echo "4. Analyse salariale"
avg salary from Person
max salary from Person
min salary from Person

echo "5. Relations managériales"
find edges with type="manages"
EOF

# Exécuter le script
curl -X POST http://localhost:5000/api/scripts/execute \
  -H "Content-Type: application/json" \
  -d '{"scriptPath": "company_report.gqls"}'
```

### 7. Optimisations

#### Optimiser le graphe des relations
```bash
curl -X POST http://localhost:5000/api/optimize \
  -H "Content-Type: application/json" \
  -d '{
    "algorithm": "intelligent_optimization",
    "parameters": {
      "maxIterations": 10,
      "focusArea": "relationships"
    }
  }'
```

## 🔍 Requêtes Complexes en Langage Naturel

### Analytics avancées
```bash
# Trouver les employés les mieux payés par département
curl -X POST http://localhost:5000/api/query \
  -H "Content-Type: application/json" \
  -d '{"query": "find Person where salary = max(salary) group by department"}'

# Projets avec budget élevé et délai serré
curl -X POST http://localhost:5000/api/query \
  -H "Content-Type: application/json" \
  -d '{"query": "find Project where budget > 400000 and deadline < 2025-01-01"}'

# Employés travaillant sur multiple projets
curl -X POST http://localhost:5000/api/query \
  -H "Content-Type: application/json" \
  -d '{"query": "find Person having count(works_on) > 1"}'
```

### Analyses de réseau
```bash
# Centralité des employés (qui a le plus de connexions)
curl -X POST http://localhost:5000/api/query \
  -H "Content-Type: application/json" \
  -d '{"query": "calculate centrality for Person nodes"}'

# Détecter les goulots d'étranglement
curl -X POST http://localhost:5000/api/query \
  -H "Content-Type: application/json" \
  -d '{"query": "find bridges in graph"}'
```

## 📊 Dashboard API Example

### Script pour tableau de bord
```javascript
// Exemple d'intégration JavaScript
const API_BASE = 'http://localhost:5000/api';

async function getDashboardData() {
    try {
        // Statistiques générales
        const stats = await fetch(`${API_BASE}/stats`).then(r => r.json());
        
        // Nombre d'employés par département
        const engineeringCount = await fetch(`${API_BASE}/aggregate`, {
            method: 'POST',
            headers: {'Content-Type': 'application/json'},
            body: JSON.stringify({
                label: 'Person',
                function: 'count', 
                property: '*',
                conditions: {department: 'Engineering'}
            })
        }).then(r => r.json());
        
        // Salaire moyen
        const avgSalary = await fetch(`${API_BASE}/aggregate`, {
            method: 'POST',
            headers: {'Content-Type': 'application/json'},
            body: JSON.stringify({
                label: 'Person',
                function: 'avg',
                property: 'salary'
            })
        }).then(r => r.json());
        
        // Projets actifs
        const activeProjects = await fetch(
            `${API_BASE}/nodes?label=Project&conditions[status]=active`
        ).then(r => r.json());
        
        return {
            totalNodes: stats.data.totalNodes,
            totalEdges: stats.data.totalEdges,
            engineeringTeamSize: engineeringCount.data,
            averageSalary: avgSalary.data,
            activeProjects: activeProjects.data.length
        };
        
    } catch (error) {
        console.error('Erreur dashboard:', error);
    }
}
```

## 🚀 Avantages de cette Approche

1. **API Unifiée** : Un seul endpoint pour chaque type d'opération
2. **Moteurs Natifs** : Utilisation directe de GraphQLiteEngine et ScriptEngine
3. **Langage Naturel** : Requêtes expressives et intuitives
4. **Performance** : Cache automatique et optimisations intégrées
5. **Scripts Réutilisables** : Logique métier dans des fichiers .gqls
6. **Scalabilité** : Architecture conçue pour les gros volumes

Cette API CRUD offre une interface puissante et flexible pour tous les besoins de gestion de données graphe ! 🎉
