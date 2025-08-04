namespace GraphQLite.Query;

/// <summary>
/// Représente une jointure virtuelle entre deux nœuds
/// </summary>
public class VirtualJoin
{
    public string SourceNodeLabel { get; set; } = string.Empty;
    public string TargetNodeLabel { get; set; } = string.Empty;
    public string? EdgeType { get; set; }
    public string? JoinProperty { get; set; }
    public string? JoinOperator { get; set; } // "=", ">", "<", ">=", "<=", "!="
    public Dictionary<string, object> JoinConditions { get; set; } = new();
    public int? MaxSteps { get; set; }
    public bool IsBidirectional { get; set; } = false;
    public string? ViaNodeLabel { get; set; }
    public string? AvoidEdgeType { get; set; }
}

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
    public int? Offset { get; set; }
    public int? MaxSteps { get; set; }
    
    // Propriétés pour les agrégations
    public AggregateFunction? AggregateFunction { get; set; }
    public string? AggregateProperty { get; set; }
    
    // Propriétés pour les variables
    public Dictionary<string, object> Variables { get; set; } = new();
    public bool IsVariableDefinition { get; set; }
    public string? VariableName { get; set; }
    public object? VariableValue { get; set; }
    
    // Propriétés pour les opérations en lot
    public List<ParsedQuery> BatchOperations { get; set; } = new();
    public BatchOperationType? BatchType { get; set; }
    public string? BatchFile { get; set; }
    public bool IsBatchOperation { get; set; }
    public int? BatchSize { get; set; }
    public bool IsAtomic { get; set; } = false;
    public Dictionary<string, object> BatchMetadata { get; set; } = new();
    
    // Propriétés pour les sous-requêtes
    public List<ParsedQuery> SubQueries { get; set; } = new();
    public SubQueryOperator SubQueryOperator { get; set; }
    public string? SubQueryProperty { get; set; }
    public bool HasSubQueries => SubQueries.Count > 0;
    public ParsedQuery? ParentQuery { get; set; }
    public int SubQueryDepth { get; set; } = 0;
    
    // Propriétés pour les jointures virtuelles
    public List<VirtualJoin> VirtualJoins { get; set; } = new();
    public bool HasVirtualJoins => VirtualJoins.Count > 0;
    public string? JoinType { get; set; } // "inner", "left", "right", "full"
    public string? JoinCondition { get; set; }
    
    // Propriétés pour le groupement et tri
    public List<string> GroupByProperties { get; set; } = new();
    public List<OrderByClause> OrderByClauses { get; set; } = new();
    public Dictionary<string, object> HavingConditions { get; set; } = new();
    public bool HasGroupBy => GroupByProperties.Count > 0;
    public bool HasOrderBy => OrderByClauses.Count > 0;
    public bool HasHaving => HavingConditions.Count > 0;
    
    // Propriétés pour les fonctions de fenêtre
    public WindowFunctionType? WindowFunctionType { get; set; }
    public List<string> WindowPartitionBy { get; set; } = new();
    public List<OrderByClause> WindowOrderBy { get; set; } = new();
    public string? WindowFunctionProperty { get; set; }
    public bool HasWindowFunction => WindowFunctionType.HasValue;
    
    // Propriétés pour la validation et les erreurs
    public List<string> ValidationErrors { get; set; } = new();
    public bool IsValid => !ValidationErrors.Any();
    
    // Propriétés pour les performances
    public DateTime ParsedAt { get; set; } = DateTime.UtcNow;
    public TimeSpan ParseDuration { get; set; }
    public Dictionary<string, object> PerformanceMetrics { get; set; } = new();
    
    /// <summary>
    /// Valide la requête parsée
    /// </summary>
    public void Validate()
    {
        ValidationErrors.Clear();
        
        // Validation du type de requête
        if (Type == QueryType.BatchOperation && BatchType == null)
        {
            ValidationErrors.Add("Type d'opération batch requis pour les requêtes batch");
        }
        
        // Validation des sous-requêtes
        if (HasSubQueries && SubQueryDepth > 5)
        {
            ValidationErrors.Add($"Profondeur de sous-requête trop élevée: {SubQueryDepth} (max: 5)");
        }
        
        // Validation des opérations batch
        if (Type == QueryType.BatchOperation)
        {
            ValidateBatchOperation();
        }
        
        // Validation des sous-requêtes
        foreach (var subQuery in SubQueries)
        {
            subQuery.Validate();
            ValidationErrors.AddRange(subQuery.ValidationErrors.Select(e => $"Sous-requête: {e}"));
        }
        
        // Validation des opérations batch prédéfinies
        foreach (var batchOp in BatchOperations)
        {
            batchOp.Validate();
            ValidationErrors.AddRange(batchOp.ValidationErrors.Select(e => $"Opération batch: {e}"));
        }
    }
    
    private void ValidateBatchOperation()
    {
        if (BatchOperations.Any() && BatchType != null)
        {
            ValidationErrors.Add("Ne peut pas avoir à la fois des opérations batch prédéfinies et un type batch");
        }
        
        if (BatchSize.HasValue && BatchSize.Value <= 0)
        {
            ValidationErrors.Add("La taille du batch doit être positive");
        }
        
        if (IsAtomic && BatchOperations.Count > 1000)
        {
            ValidationErrors.Add("Les opérations atomiques sont limitées à 1000 opérations");
        }
    }
    
    /// <summary>
    /// Clone la requête pour les opérations batch
    /// </summary>
    public ParsedQuery Clone()
    {
        return new ParsedQuery
        {
            Type = Type,
            NodeLabel = NodeLabel,
            EdgeType = EdgeType,
            Properties = new Dictionary<string, object>(Properties),
            Conditions = new Dictionary<string, object>(Conditions),
            FromNode = FromNode,
            ToNode = ToNode,
            Limit = Limit,
            Offset = Offset,
            MaxSteps = MaxSteps,
            AggregateFunction = AggregateFunction,
            AggregateProperty = AggregateProperty,
            Variables = new Dictionary<string, object>(Variables),
            IsVariableDefinition = IsVariableDefinition,
            VariableName = VariableName,
            VariableValue = VariableValue,
            BatchType = BatchType,
            BatchFile = BatchFile,
            IsBatchOperation = IsBatchOperation,
            BatchSize = BatchSize,
            IsAtomic = IsAtomic,
            BatchMetadata = new Dictionary<string, object>(BatchMetadata),
            SubQueryOperator = SubQueryOperator,
            SubQueryProperty = SubQueryProperty,
            ParentQuery = ParentQuery,
            SubQueryDepth = SubQueryDepth
        };
    }
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
    ShowSchema,
    Aggregate,
    DefineVariable,
    BatchOperation,
    SubQuery,
    BulkInsert,
    Transaction,
    VirtualJoin,
    GroupBy,
    OrderBy,
    Having,
    WindowFunction,
    	    ShowIndexedProperties,
	ShowIndexStats,
	AddIndexProperty,
	RemoveIndexProperty,
	GraphOptimization
}

/// <summary>
/// Types d'opérations en lot supportées
/// </summary>
public enum BatchOperationType
{
    Create,
    Update,
    Delete,
    Upsert,
    Mixed,
    Atomic,
    Parallel
}

/// <summary>
/// Fonctions d'agrégation supportées
/// </summary>
public enum AggregateFunction
{
    Sum,
    Avg,
    Min,
    Max,
    Count,
    StdDev,
    Variance,
    Median
}

/// <summary>
/// Opérateurs pour les sous-requêtes
/// </summary>
public enum SubQueryOperator
{
    In,
    NotIn,
    Exists,
    NotExists,
    Contains,
    NotContains,
    Any,
    All,
    None
}

/// <summary>
/// Représente une clause ORDER BY
/// </summary>
public class OrderByClause
{
    public string Property { get; set; } = string.Empty;
    public OrderDirection Direction { get; set; } = OrderDirection.Ascending;
}

/// <summary>
/// Direction de tri
/// </summary>
public enum OrderDirection
{
    Ascending,
    Descending
}

/// <summary>
/// Types de fonctions de fenêtre
/// </summary>
public enum WindowFunctionType
{
    RowNumber,
    Rank,
    DenseRank,
    PercentRank,
    Ntile,
    Lead,
    Lag,
    FirstValue,
    LastValue,
    NthValue
}
