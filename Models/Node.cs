namespace GraphQLite.Models;

/// <summary>
/// Représente un nœud dans le graphe
/// </summary>
public class Node
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = string.Empty;
    public Dictionary<string, object> Properties { get; set; } = new();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Node() { }

    public Node(string label, Dictionary<string, object>? properties = null)
    {
        Label = label;
        Properties = properties ?? new Dictionary<string, object>();
    }

    /// <summary>
    /// Ajoute ou met à jour une propriété du nœud
    /// </summary>
    public void SetProperty(string key, object value)
    {
        Properties[key] = value;
        UpdatedAt = DateTime.UtcNow;
    }

    /// <summary>
    /// Obtient une propriété du nœud
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
    /// Vérifie si le nœud possède une propriété
    /// </summary>
    public bool HasProperty(string key) => Properties.ContainsKey(key);

    public override string ToString()
    {
        var props = string.Join(", ", Properties.Select(kv => $"{kv.Key}: {kv.Value}"));
        return $"Node({Label}) [{props}]";
    }
}
