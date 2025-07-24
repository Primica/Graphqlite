namespace GraphQLite.Query;

/// <summary>
/// Représente une requête parsée dans le DSL GraphQLite
/// </summary>
public class ParsedQuery
{
    public QueryType Type { get; set; }
    public string? NodeLabel { get; set; }
    public string? EdgeType { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
    public Dictionary<string, object> Conditions { get; set; } = new();
    public string? FromNode { get; set; }
    public string? ToNode { get; set; }
    public int? Limit { get; set; }
    public int? MaxSteps { get; set; }
}

/// <summary>
/// Types de requêtes supportées
/// </summary>
public enum QueryType
{
    CreateNode,
    CreateEdge,
    FindNodes,
    FindEdges,
    FindPath,
    FindWithinSteps,
    UpdateNode,
    UpdateEdge,
    DeleteNode,
    DeleteEdge,
    Count,
    ShowSchema
}
