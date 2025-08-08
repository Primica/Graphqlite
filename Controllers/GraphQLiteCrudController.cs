using Microsoft.AspNetCore.Mvc;
using GraphQLite.Engine;
using GraphQLite.Scripting;
using GraphQLite.Models;
using GraphQLite.Models.Api;
using GraphQLite.Query;
using System.Text;

namespace GraphQLite.Controllers;

/// <summary>
/// Contrôleur CRUD unifié utilisant GraphQLiteEngine et ScriptEngine
/// Toutes les opérations passent par les moteurs natifs
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class GraphQLiteCrudController : ControllerBase
{
    private readonly GraphQLiteEngine _engine;
    private readonly ScriptEngine _scriptEngine;
    private readonly ILogger<GraphQLiteCrudController> _logger;

    public GraphQLiteCrudController(
        GraphQLiteEngine engine, 
        ScriptEngine scriptEngine, 
        ILogger<GraphQLiteCrudController> logger)
    {
        _engine = engine;
        _scriptEngine = scriptEngine;
        _logger = logger;
    }

    #region Health & Info

    /// <summary>
    /// Vérifie la santé des moteurs GraphQLite
    /// </summary>
    [HttpGet("health")]
    public async Task<ActionResult<CrudResponse<HealthStatus>>> GetHealth()
    {
        try
        {
            // Test des moteurs
            var testResult = await _engine.ExecuteQueryAsync("show schema");
            
            return Ok(new CrudResponse<HealthStatus>
            {
                Success = true,
                Data = new HealthStatus
                {
                    IsHealthy = testResult.Success,
                    GraphQLiteEngine = testResult.Success ? "OK" : "ERROR",
                    ScriptEngine = "OK",
                    Timestamp = DateTime.UtcNow
                }
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors du check de santé");
            return StatusCode(500, new CrudResponse<HealthStatus>
            {
                Success = false,
                Error = "Erreur interne du serveur",
                Data = new HealthStatus { IsHealthy = false, Timestamp = DateTime.UtcNow }
            });
        }
    }

    /// <summary>
    /// Obtient les statistiques complètes du graphe
    /// </summary>
    [HttpGet("stats")]
    public async Task<ActionResult<CrudResponse<object>>> GetStats()
    {
        try
        {
            var result = await _engine.ExecuteQueryAsync("show schema stats");
            
            return Ok(new CrudResponse<object>
            {
                Success = result.Success,
                Data = result.Data,
                Message = result.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la récupération des statistiques");
            return StatusCode(500, new CrudResponse<object>
            {
                Success = false,
                Error = "Erreur interne du serveur"
            });
        }
    }

    #endregion

    #region Create Operations

    /// <summary>
    /// Crée un nouveau nœud via GraphQLiteEngine
    /// </summary>
    [HttpPost("nodes")]
    public async Task<ActionResult<CrudResponse<object>>> CreateNode([FromBody] CreateNodeRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Label))
            {
                return BadRequest(new CrudResponse<object>
                {
                    Success = false,
                    Error = "Le label du nœud est requis"
                });
            }

            // Construire la requête GraphQLite native
            var query = BuildCreateNodeQuery(request.Label, request.Properties);
            
            _logger.LogInformation("Création de nœud: {Query}", query);
            var result = await _engine.ExecuteQueryAsync(query);

            if (result.Success)
            {
                return Ok(new CrudResponse<object>
                {
                    Success = true,
                    Message = result.Message,
                    Data = result.Data
                });
            }
            else
            {
                return BadRequest(new CrudResponse<object>
                {
                    Success = false,
                    Error = result.Error
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création du nœud");
            return StatusCode(500, new CrudResponse<object>
            {
                Success = false,
                Error = "Erreur interne du serveur"
            });
        }
    }

    /// <summary>
    /// Crée une nouvelle arête via GraphQLiteEngine
    /// </summary>
    [HttpPost("edges")]
    public async Task<ActionResult<CrudResponse<object>>> CreateEdge([FromBody] CreateEdgeRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.FromNode) || string.IsNullOrEmpty(request.ToNode))
            {
                return BadRequest(new CrudResponse<object>
                {
                    Success = false,
                    Error = "Les nœuds source et destination sont requis"
                });
            }

            // Construire la requête GraphQLite native
            var query = BuildCreateEdgeQuery(request.FromNode, request.ToNode, request.EdgeType, request.Properties);
            
            _logger.LogInformation("Création d'arête: {Query}", query);
            var result = await _engine.ExecuteQueryAsync(query);

            if (result.Success)
            {
                return Ok(new CrudResponse<object>
                {
                    Success = true,
                    Message = result.Message,
                    Data = result.Data
                });
            }
            else
            {
                return BadRequest(new CrudResponse<object>
                {
                    Success = false,
                    Error = result.Error
                });
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création de l'arête");
            return StatusCode(500, new CrudResponse<object>
            {
                Success = false,
                Error = "Erreur interne du serveur"
            });
        }
    }

    /// <summary>
    /// Crée plusieurs nœuds en batch via ScriptEngine
    /// </summary>
    [HttpPost("nodes/batch")]
    public async Task<ActionResult<CrudResponse<BatchResult>>> CreateNodesBatch([FromBody] CreateNodesBatchRequest request)
    {
        try
        {
            if (!request.Nodes?.Any() == true)
            {
                return BadRequest(new CrudResponse<BatchResult>
                {
                    Success = false,
                    Error = "Aucun nœud à créer"
                });
            }

            // Construire un script GraphQLite pour le batch
            var script = BuildBatchCreateScript(request.Nodes);
            
            _logger.LogInformation("Création batch de {Count} nœuds", request.Nodes.Count);
            
            // Créer un fichier temporaire et exécuter via ScriptEngine
            var result = await ExecuteScriptContent(script);

            var batchResult = new BatchResult
            {
                TotalOperations = request.Nodes.Count,
                SuccessfulOperations = result.Success ? request.Nodes.Count : 0,
                FailedOperations = result.Success ? 0 : request.Nodes.Count,
                ExecutionTime = TimeSpan.Zero, // ScriptEngine ne retourne pas le temps
                Details = result.Message
            };

            return Ok(new CrudResponse<BatchResult>
            {
                Success = result.Success,
                Message = result.Message,
                Data = batchResult
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la création batch de nœuds");
            return StatusCode(500, new CrudResponse<BatchResult>
            {
                Success = false,
                Error = "Erreur interne du serveur"
            });
        }
    }

    #endregion

    #region Read Operations

    /// <summary>
    /// Recherche des nœuds via GraphQLiteEngine
    /// </summary>
    [HttpGet("nodes")]
    public async Task<ActionResult<CrudResponse<object>>> FindNodes([FromQuery] FindNodesRequest request)
    {
        try
        {
            // Construire la requête GraphQLite native
            var query = BuildFindNodesQuery(request.Label, request.Conditions, request.Limit, request.Offset);
            
            _logger.LogInformation("Recherche de nœuds: {Query}", query);
            var result = await _engine.ExecuteQueryAsync(query);

            return Ok(new CrudResponse<object>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data,
                Error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la recherche de nœuds");
            return StatusCode(500, new CrudResponse<object>
            {
                Success = false,
                Error = "Erreur interne du serveur"
            });
        }
    }

    /// <summary>
    /// Recherche des arêtes via GraphQLiteEngine
    /// </summary>
    [HttpGet("edges")]
    public async Task<ActionResult<CrudResponse<object>>> FindEdges([FromQuery] FindEdgesRequest request)
    {
        try
        {
            // Construire la requête GraphQLite native
            var query = BuildFindEdgesQuery(request.FromNode, request.ToNode, request.EdgeType, request.Conditions);
            
            _logger.LogInformation("Recherche d'arêtes: {Query}", query);
            var result = await _engine.ExecuteQueryAsync(query);

            return Ok(new CrudResponse<object>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data,
                Error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la recherche d'arêtes");
            return StatusCode(500, new CrudResponse<object>
            {
                Success = false,
                Error = "Erreur interne du serveur"
            });
        }
    }

    /// <summary>
    /// Trouve un chemin entre deux nœuds via GraphQLiteEngine
    /// </summary>
    [HttpGet("paths")]
    public async Task<ActionResult<CrudResponse<object>>> FindPath([FromQuery] FindPathRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.FromNode) || string.IsNullOrEmpty(request.ToNode))
            {
                return BadRequest(new CrudResponse<object>
                {
                    Success = false,
                    Error = "Les nœuds source et destination sont requis"
                });
            }

            // Construire la requête GraphQLite native
            var query = BuildFindPathQuery(request.FromNode, request.ToNode, request.MaxSteps);
            
            _logger.LogInformation("Recherche de chemin: {Query}", query);
            var result = await _engine.ExecuteQueryAsync(query);

            return Ok(new CrudResponse<object>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data,
                Error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la recherche de chemin");
            return StatusCode(500, new CrudResponse<object>
            {
                Success = false,
                Error = "Erreur interne du serveur"
            });
        }
    }

    /// <summary>
    /// Exécute une requête en langage naturel GraphQLite
    /// </summary>
    [HttpPost("query")]
    public async Task<ActionResult<CrudResponse<object>>> ExecuteNaturalLanguageQuery([FromBody] NaturalLanguageQueryRequest request)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(request.Query))
            {
                return BadRequest(new CrudResponse<object>
                {
                    Success = false,
                    Error = "La requête ne peut pas être vide"
                });
            }

            _logger.LogInformation("Exécution de requête naturelle: {Query}", request.Query);
            var result = await _engine.ExecuteQueryAsync(request.Query);

            return Ok(new CrudResponse<object>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data,
                Error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'exécution de la requête");
            return StatusCode(500, new CrudResponse<object>
            {
                Success = false,
                Error = "Erreur interne du serveur"
            });
        }
    }

    #endregion

    #region Update Operations

    /// <summary>
    /// Met à jour des nœuds via GraphQLiteEngine
    /// </summary>
    [HttpPut("nodes")]
    public async Task<ActionResult<CrudResponse<object>>> UpdateNodes([FromBody] UpdateNodesRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Label))
            {
                return BadRequest(new CrudResponse<object>
                {
                    Success = false,
                    Error = "Le label des nœuds est requis"
                });
            }

            // Construire la requête GraphQLite native
            var query = BuildUpdateNodesQuery(request.Label, request.Properties, request.Conditions);
            
            _logger.LogInformation("Mise à jour de nœuds: {Query}", query);
            var result = await _engine.ExecuteQueryAsync(query);

            return Ok(new CrudResponse<object>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data,
                Error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la mise à jour des nœuds");
            return StatusCode(500, new CrudResponse<object>
            {
                Success = false,
                Error = "Erreur interne du serveur"
            });
        }
    }

    /// <summary>
    /// Met à jour des arêtes via GraphQLiteEngine
    /// </summary>
    [HttpPut("edges")]
    public async Task<ActionResult<CrudResponse<object>>> UpdateEdges([FromBody] UpdateEdgesRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.FromNode) || string.IsNullOrEmpty(request.ToNode))
            {
                return BadRequest(new CrudResponse<object>
                {
                    Success = false,
                    Error = "Les nœuds source et destination sont requis"
                });
            }

            // Construire la requête GraphQLite native
            var query = BuildUpdateEdgesQuery(request.FromNode, request.ToNode, request.Properties, request.Conditions);
            
            _logger.LogInformation("Mise à jour d'arêtes: {Query}", query);
            var result = await _engine.ExecuteQueryAsync(query);

            return Ok(new CrudResponse<object>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data,
                Error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la mise à jour des arêtes");
            return StatusCode(500, new CrudResponse<object>
            {
                Success = false,
                Error = "Erreur interne du serveur"
            });
        }
    }

    #endregion

    #region Delete Operations

    /// <summary>
    /// Supprime des nœuds via GraphQLiteEngine
    /// </summary>
    [HttpDelete("nodes")]
    public async Task<ActionResult<CrudResponse<object>>> DeleteNodes([FromBody] DeleteNodesRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Label))
            {
                return BadRequest(new CrudResponse<object>
                {
                    Success = false,
                    Error = "Le label des nœuds est requis"
                });
            }

            // Construire la requête GraphQLite native
            var query = BuildDeleteNodesQuery(request.Label, request.Conditions);
            
            _logger.LogInformation("Suppression de nœuds: {Query}", query);
            var result = await _engine.ExecuteQueryAsync(query);

            return Ok(new CrudResponse<object>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data,
                Error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la suppression des nœuds");
            return StatusCode(500, new CrudResponse<object>
            {
                Success = false,
                Error = "Erreur interne du serveur"
            });
        }
    }

    /// <summary>
    /// Supprime des arêtes via GraphQLiteEngine
    /// </summary>
    [HttpDelete("edges")]
    public async Task<ActionResult<CrudResponse<object>>> DeleteEdges([FromBody] DeleteEdgesRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.FromNode) || string.IsNullOrEmpty(request.ToNode))
            {
                return BadRequest(new CrudResponse<object>
                {
                    Success = false,
                    Error = "Les nœuds source et destination sont requis"
                });
            }

            // Construire la requête GraphQLite native
            var query = BuildDeleteEdgesQuery(request.FromNode, request.ToNode, request.Conditions);
            
            _logger.LogInformation("Suppression d'arêtes: {Query}", query);
            var result = await _engine.ExecuteQueryAsync(query);

            return Ok(new CrudResponse<object>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data,
                Error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de la suppression des arêtes");
            return StatusCode(500, new CrudResponse<object>
            {
                Success = false,
                Error = "Erreur interne du serveur"
            });
        }
    }

    #endregion

    #region Script Operations

    /// <summary>
    /// Exécute un script GraphQLite via ScriptEngine
    /// </summary>
    [HttpPost("scripts/execute")]
    public async Task<ActionResult<CrudResponse<ScriptExecutionResult>>> ExecuteScript([FromBody] ExecuteScriptRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.ScriptPath))
            {
                return BadRequest(new CrudResponse<ScriptExecutionResult>
                {
                    Success = false,
                    Error = "Le chemin du script est requis"
                });
            }

            // Vérifier si le fichier existe
            var fullPath = Path.IsPathRooted(request.ScriptPath) 
                ? request.ScriptPath 
                : Path.Combine(Directory.GetCurrentDirectory(), request.ScriptPath);

            if (!System.IO.File.Exists(fullPath))
            {
                return NotFound(new CrudResponse<ScriptExecutionResult>
                {
                    Success = false,
                    Error = $"Fichier script introuvable : {request.ScriptPath}"
                });
            }

            _logger.LogInformation("Exécution de script: {ScriptPath}", request.ScriptPath);
            var result = await _scriptEngine.ExecuteScriptAsync(fullPath);

            var apiResult = new ScriptExecutionResult
            {
                Success = result.Success,
                TotalQueries = result.TotalQueries,
                SuccessfulQueries = result.SuccessCount,
                FailedQueries = result.ErrorCount,
                ExecutionTime = TimeSpan.Zero, // ScriptResult n'expose pas le temps
                Results = result.Results?.Select(r => new QueryExecutionSummary
                {
                    Query = r.Query,
                    Success = r.Success,
                    Message = r.Message,
                    Error = r.Error,
                    ExecutionTime = TimeSpan.Zero
                }).ToList() ?? new List<QueryExecutionSummary>(),
                Error = result.Error
            };

            return Ok(new CrudResponse<ScriptExecutionResult>
            {
                Success = result.Success,
                Message = result.Message,
                Data = apiResult
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'exécution du script");
            return StatusCode(500, new CrudResponse<ScriptExecutionResult>
            {
                Success = false,
                Error = "Erreur interne du serveur"
            });
        }
    }

    /// <summary>
    /// Exécute un contenu de script GraphQLite via ScriptEngine
    /// </summary>
    [HttpPost("scripts/execute-content")]
    public async Task<ActionResult<CrudResponse<ScriptExecutionResult>>> ExecuteScriptContent([FromBody] ExecuteScriptContentRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Content))
            {
                return BadRequest(new CrudResponse<ScriptExecutionResult>
                {
                    Success = false,
                    Error = "Le contenu du script est requis"
                });
            }

            _logger.LogInformation("Exécution de script inline");
            var result = await ExecuteScriptContent(request.Content);

            var apiResult = new ScriptExecutionResult
            {
                Success = result.Success,
                TotalQueries = 0, // Non disponible dans cette implémentation
                SuccessfulQueries = result.Success ? 1 : 0,
                FailedQueries = result.Success ? 0 : 1,
                ExecutionTime = TimeSpan.Zero,
                Results = new List<QueryExecutionSummary>(),
                Error = result.Error
            };

            return Ok(new CrudResponse<ScriptExecutionResult>
            {
                Success = result.Success,
                Message = result.Message,
                Data = apiResult
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'exécution du script inline");
            return StatusCode(500, new CrudResponse<ScriptExecutionResult>
            {
                Success = false,
                Error = "Erreur interne du serveur"
            });
        }
    }

    #endregion

    #region Advanced Operations

    /// <summary>
    /// Optimise le graphe via GraphOptimizationEngine
    /// </summary>
    [HttpPost("optimize")]
    public async Task<ActionResult<CrudResponse<object>>> OptimizeGraph([FromBody] OptimizeGraphRequest request)
    {
        try
        {
            // Construire la requête d'optimisation GraphQLite
            var query = BuildOptimizeQuery(request.Algorithm, request.Parameters);
            
            _logger.LogInformation("Optimisation du graphe: {Query}", query);
            var result = await _engine.ExecuteQueryAsync(query);

            return Ok(new CrudResponse<object>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data,
                Error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'optimisation du graphe");
            return StatusCode(500, new CrudResponse<object>
            {
                Success = false,
                Error = "Erreur interne du serveur"
            });
        }
    }

    /// <summary>
    /// Exécute une agrégation via GraphQLiteEngine
    /// </summary>
    [HttpPost("aggregate")]
    public async Task<ActionResult<CrudResponse<object>>> ExecuteAggregation([FromBody] AggregationRequest request)
    {
        try
        {
            // Construire la requête d'agrégation GraphQLite
            var query = BuildAggregationQuery(request.Label, request.Function, request.Property, request.Conditions);
            
            _logger.LogInformation("Exécution d'agrégation: {Query}", query);
            var result = await _engine.ExecuteQueryAsync(query);

            return Ok(new CrudResponse<object>
            {
                Success = result.Success,
                Message = result.Message,
                Data = result.Data,
                Error = result.Error
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erreur lors de l'agrégation");
            return StatusCode(500, new CrudResponse<object>
            {
                Success = false,
                Error = "Erreur interne du serveur"
            });
        }
    }

    #endregion

    #region Helper Methods

    private string BuildCreateNodeQuery(string label, Dictionary<string, object>? properties = null)
    {
        var query = $"create \"{CleanNodeName(label)}\"";
        
        if (properties?.Any() == true)
        {
            var props = string.Join(", ", properties.Select(kv => $"{kv.Key}=\"{kv.Value}\""));
            query += $" with {props}";
        }
        
        return query;
    }

    private string BuildCreateEdgeQuery(string fromNode, string toNode, string? edgeType, Dictionary<string, object>? properties = null)
    {
        var cleanFromNode = CleanNodeName(fromNode);
        var cleanToNode = CleanNodeName(toNode);
        var type = string.IsNullOrEmpty(edgeType) ? "connected" : edgeType;
        
        var query = $"connect \"{cleanFromNode}\" to \"{cleanToNode}\" with type=\"{type}\"";
        
        if (properties?.Any() == true)
        {
            var props = string.Join(", ", properties.Select(kv => $"{kv.Key}=\"{kv.Value}\""));
            query += $", {props}";
        }
        
        return query;
    }

    private string BuildFindNodesQuery(string? label, Dictionary<string, object>? conditions = null, int? limit = null, int? offset = null)
    {
        var query = "find";
        
        if (!string.IsNullOrEmpty(label))
        {
            query += $" \"{CleanNodeName(label)}\"";
        }
        else
        {
            query += " nodes";
        }
        
        if (conditions?.Any() == true)
        {
            var conditionsStr = string.Join(" and ", conditions.Select(kv => $"{kv.Key}=\"{kv.Value}\""));
            query += $" where {conditionsStr}";
        }
        
        if (limit.HasValue)
        {
            query += $" limit {limit.Value}";
        }
        
        return query;
    }

    private string BuildFindEdgesQuery(string? fromNode, string? toNode, string? edgeType, Dictionary<string, object>? conditions = null)
    {
        var query = "find edges";
        
        if (!string.IsNullOrEmpty(fromNode) && !string.IsNullOrEmpty(toNode))
        {
            query += $" from \"{CleanNodeName(fromNode)}\" to \"{CleanNodeName(toNode)}\"";
        }
        else if (!string.IsNullOrEmpty(fromNode))
        {
            query += $" from \"{CleanNodeName(fromNode)}\"";
        }
        else if (!string.IsNullOrEmpty(toNode))
        {
            query += $" to \"{CleanNodeName(toNode)}\"";
        }
        
        if (!string.IsNullOrEmpty(edgeType))
        {
            query += $" with type=\"{edgeType}\"";
        }
        
        if (conditions?.Any() == true)
        {
            var conditionsStr = string.Join(" and ", conditions.Select(kv => $"{kv.Key}=\"{kv.Value}\""));
            query += $" where {conditionsStr}";
        }
        
        return query;
    }

    private string BuildFindPathQuery(string fromNode, string toNode, int? maxSteps = null)
    {
        var cleanFromNode = CleanNodeName(fromNode);
        var cleanToNode = CleanNodeName(toNode);
        
        var query = $"find path from \"{cleanFromNode}\" to \"{cleanToNode}\"";
        
        if (maxSteps.HasValue)
        {
            query += $" within {maxSteps.Value} steps";
        }
        
        return query;
    }

    private string BuildUpdateNodesQuery(string label, Dictionary<string, object> properties, Dictionary<string, object>? conditions = null)
    {
        var query = $"update \"{CleanNodeName(label)}\"";
        
        if (properties?.Any() == true)
        {
            var props = string.Join(", ", properties.Select(kv => $"{kv.Key}=\"{kv.Value}\""));
            query += $" set {props}";
        }
        
        if (conditions?.Any() == true)
        {
            var conditionsStr = string.Join(" and ", conditions.Select(kv => $"{kv.Key}=\"{kv.Value}\""));
            query += $" where {conditionsStr}";
        }
        
        return query;
    }

    private string BuildUpdateEdgesQuery(string fromNode, string toNode, Dictionary<string, object> properties, Dictionary<string, object>? conditions = null)
    {
        var cleanFromNode = CleanNodeName(fromNode);
        var cleanToNode = CleanNodeName(toNode);
        
        var query = $"update edge from \"{cleanFromNode}\" to \"{cleanToNode}\"";
        
        if (properties?.Any() == true)
        {
            var props = string.Join(", ", properties.Select(kv => $"{kv.Key}=\"{kv.Value}\""));
            query += $" set {props}";
        }
        
        if (conditions?.Any() == true)
        {
            var conditionsStr = string.Join(" and ", conditions.Select(kv => $"{kv.Key}=\"{kv.Value}\""));
            query += $" where {conditionsStr}";
        }
        
        return query;
    }

    private string BuildDeleteNodesQuery(string label, Dictionary<string, object>? conditions = null)
    {
        var query = $"delete \"{CleanNodeName(label)}\"";
        
        if (conditions?.Any() == true)
        {
            var conditionsStr = string.Join(" and ", conditions.Select(kv => $"{kv.Key}=\"{kv.Value}\""));
            query += $" where {conditionsStr}";
        }
        
        return query;
    }

    private string BuildDeleteEdgesQuery(string fromNode, string toNode, Dictionary<string, object>? conditions = null)
    {
        var cleanFromNode = CleanNodeName(fromNode);
        var cleanToNode = CleanNodeName(toNode);
        
        var query = $"delete edge from \"{cleanFromNode}\" to \"{cleanToNode}\"";
        
        if (conditions?.Any() == true)
        {
            var conditionsStr = string.Join(" and ", conditions.Select(kv => $"{kv.Key}=\"{kv.Value}\""));
            query += $" where {conditionsStr}";
        }
        
        return query;
    }

    private string BuildBatchCreateScript(List<CreateNodeRequest> nodes)
    {
        var script = new StringBuilder();
        script.AppendLine("// Script de création batch généré automatiquement");
        
        foreach (var node in nodes)
        {
            var nodeQuery = BuildCreateNodeQuery(node.Label, node.Properties);
            script.AppendLine(nodeQuery);
        }
        
        return script.ToString();
    }

    private string BuildOptimizeQuery(string? algorithm = null, Dictionary<string, object>? parameters = null)
    {
        var query = "optimize";
        
        if (!string.IsNullOrEmpty(algorithm))
        {
            query += $" {algorithm}";
        }
        
        if (parameters?.Any() == true)
        {
            var props = string.Join(", ", parameters.Select(kv => $"{kv.Key}={kv.Value}"));
            query += $" with {props}";
        }
        
        return query;
    }

    private string BuildAggregationQuery(string label, string function, string property, Dictionary<string, object>? conditions = null)
    {
        var query = $"{function} {property} from \"{CleanNodeName(label)}\"";
        
        if (conditions?.Any() == true)
        {
            var conditionsStr = string.Join(" and ", conditions.Select(kv => $"{kv.Key}=\"{kv.Value}\""));
            query += $" where {conditionsStr}";
        }
        
        return query;
    }

    private string CleanNodeName(string nodeName)
    {
        return nodeName?.Trim('"', ' ') ?? "";
    }

    private async Task<QueryResult> ExecuteScriptContent(string content)
    {
        try
        {
            var tempPath = Path.GetTempFileName();
            tempPath = Path.ChangeExtension(tempPath, ".gqls");

            try
            {
                await System.IO.File.WriteAllTextAsync(tempPath, content);
                var result = await _scriptEngine.ExecuteScriptAsync(tempPath);

                return new QueryResult
                {
                    Success = result.Success,
                    Message = result.Message,
                    Error = result.Error
                };
            }
            finally
            {
                if (System.IO.File.Exists(tempPath))
                    System.IO.File.Delete(tempPath);
            }
        }
        catch (Exception ex)
        {
            return new QueryResult
            {
                Success = false,
                Error = $"Erreur lors de l'exécution du script: {ex.Message}"
            };
        }
    }

    #endregion
}
