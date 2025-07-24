namespace GraphQLite.Models;

/// <summary>
/// Représente le schéma d'un label de nœud ou d'arête
/// </summary>
public class SchemaInfo
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public Dictionary<string, TypeInfo> Properties { get; set; } = new();
    public DateTime FirstSeen { get; set; }
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Informations sur un type de propriété
/// </summary>
public class TypeInfo
{
    public string Type { get; set; } = string.Empty;
    public int Count { get; set; }
    public object? SampleValue { get; set; }
    public HashSet<object> UniqueValues { get; set; } = new();
}

/// <summary>
/// Représente le schéma complet de la base de données
/// </summary>
public class DatabaseSchema
{
    public Dictionary<string, SchemaInfo> NodeSchemas { get; set; } = new();
    public Dictionary<string, SchemaInfo> EdgeSchemas { get; set; } = new();
    public int TotalNodes { get; set; }
    public int TotalEdges { get; set; }
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
}
