namespace GraphQLite.Models;

/// <summary>
/// Représente une arête (relation) entre deux nœuds dans le graphe
/// </summary>
public class Edge
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid FromNodeId { get; set; }
    public Guid ToNodeId { get; set; }
    public string RelationType { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Edge() { }

    public Edge(Guid fromNodeId, Guid toNodeId, string relationType, Dictionary<string, object>? properties = null)
    {
        FromNodeId = fromNodeId;
        ToNodeId = toNodeId;
        RelationType = relationType;
        Properties = properties ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// Ajoute ou met à jour une propriété de l'arête
    /// </summary>
    public void SetProperty(string key, object value)
    {
        Properties[key] = value;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Obtient une propriété de l'arête
    /// </summary>
    public T? GetProperty<T>(string key)
    {
        if (Properties.TryGetValue(key, out var value) && value is T typedValue)
        {
            return typedValue;
        }
        return default(T);
    }

    /// <summary>
    /// Vérifie si l'arête possède une propriété
    /// </summary>
    public bool HasProperty(string key) => Properties.ContainsKey(key);

    /// <summary>
    /// Vérifie si cette arête connecte les nœuds spécifiés (dans n'importe quel sens)
    /// </summary>
    public bool Connects(Guid nodeId1, Guid nodeId2)
    {
        return (FromNodeId == nodeId1 && ToNodeId == nodeId2) ||
               (FromNodeId == nodeId2 && ToNodeId == nodeId1);
    }

    /// <summary>
    /// Obtient l'autre nœud connecté par cette arête
    /// </summary>
    public Guid GetOtherNode(Guid nodeId)
    {
        if (FromNodeId == nodeId) return ToNodeId;
        if (ToNodeId == nodeId) return FromNodeId;
        throw new ArgumentException("L'ID de nœud fourni n'est pas connecté par cette arête", nameof(nodeId));
    }

    public override string ToString()
    {
        var props = Properties.Any() ? $" [{string.Join(", ", Properties.Select(kv => $"{kv.Key}: {kv.Value}"))}]" : "";
        return $"Edge({FromNodeId} --{RelationType}--> {ToNodeId}){props}";
    }
}
