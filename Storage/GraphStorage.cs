using GraphQLite.Models;
using System.Text;

namespace GraphQLite.Storage;

/// <summary>
/// Gestionnaire de stockage pour la base de données GraphQLite
/// Gère la sérialisation/désérialisation vers un fichier binaire .gqlite
/// </summary>
public class GraphStorage
{
    private readonly string _filePath;
    private readonly Dictionary<Guid, Node> _nodes;
    private readonly Dictionary<Guid, Edge> _edges;
    private readonly object _lockObject = new();

    // Signature du fichier binaire pour validation
    private static readonly byte[] FileSignature = Encoding.UTF8.GetBytes("GQLITE");
    private const byte FormatVersion = 1;

    public GraphStorage(string filePath)
    {
        _filePath = filePath;
        _nodes = new Dictionary<Guid, Node>();
        _edges = new Dictionary<Guid, Edge>();
    }

    /// <summary>
    /// Charge la base de données depuis le fichier binaire
    /// </summary>
    public Task<LoadResult> LoadAsync()
    {
        if (!File.Exists(_filePath))
        {
            return Task.FromResult(new LoadResult { Success = true, IsNewDatabase = true, Message = "Nouvelle base de données créée" });
        }

        try
        {
            lock (_lockObject)
            {
                using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read);
                using var reader = new BinaryReader(stream);

                // Vérifier la signature du fichier
                var signature = reader.ReadBytes(FileSignature.Length);
                if (!signature.SequenceEqual(FileSignature))
                {
                    return Task.FromResult(new LoadResult { Success = false, Message = "Format de fichier invalide" });
                }

                // Vérifier la version
                var version = reader.ReadByte();
                if (version != FormatVersion)
                {
                    return Task.FromResult(new LoadResult { Success = false, Message = $"Version de fichier non supportée: {version}" });
                }

                _nodes.Clear();
                _edges.Clear();

                // Lire les nœuds
                var nodeCount = reader.ReadInt32();
                for (int i = 0; i < nodeCount; i++)
                {
                    var node = ReadNode(reader);
                    _nodes[node.Id] = node;
                }

                // Lire les arêtes
                var edgeCount = reader.ReadInt32();
                for (int i = 0; i < edgeCount; i++)
                {
                    var edge = ReadEdge(reader);
                    _edges[edge.Id] = edge;
                }

                return Task.FromResult(new LoadResult 
                { 
                    Success = true, 
                    IsNewDatabase = false, 
                    Message = $"Base de données chargée : {_nodes.Count} nœuds, {_edges.Count} arêtes"
                });
            }
        }
        catch (Exception ex)
        {
            return Task.FromResult(new LoadResult { Success = false, Message = $"Erreur lors du chargement : {ex.Message}" });
        }
    }

    /// <summary>
    /// Sauvegarde la base de données dans le fichier binaire
    /// </summary>
    public Task SaveAsync()
    {
        lock (_lockObject)
        {
            using var stream = new FileStream(_filePath, FileMode.Create, FileAccess.Write);
            using var writer = new BinaryWriter(stream);

            // Écrire la signature et version
            writer.Write(FileSignature);
            writer.Write(FormatVersion);

            // Écrire les nœuds
            writer.Write(_nodes.Count);
            foreach (var node in _nodes.Values)
            {
                WriteNode(writer, node);
            }

            // Écrire les arêtes
            writer.Write(_edges.Count);
            foreach (var edge in _edges.Values)
            {
                WriteEdge(writer, edge);
            }
        }
        
        return Task.CompletedTask;
    }

    /// <summary>
    /// Lit un nœud depuis le stream binaire
    /// </summary>
    private Node ReadNode(BinaryReader reader)
    {
        var id = new Guid(reader.ReadBytes(16));
        var label = reader.ReadString();
        var createdAt = DateTime.FromBinary(reader.ReadInt64());
        var updatedAt = DateTime.FromBinary(reader.ReadInt64());

        // Lire les propriétés
        var propertyCount = reader.ReadInt32();
        var properties = new Dictionary<string, object>();
        
        for (int i = 0; i < propertyCount; i++)
        {
            var key = reader.ReadString();
            var value = ReadPropertyValue(reader);
            properties[key] = value;
        }

        return new Node
        {
            Id = id,
            Label = label,
            Properties = properties,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    /// <summary>
    /// Écrit un nœud dans le stream binaire
    /// </summary>
    private void WriteNode(BinaryWriter writer, Node node)
    {
        writer.Write(node.Id.ToByteArray());
        writer.Write(node.Label);
        writer.Write(node.CreatedAt.ToBinary());
        writer.Write(node.UpdatedAt.ToBinary());

        // Écrire les propriétés
        writer.Write(node.Properties.Count);
        foreach (var property in node.Properties)
        {
            writer.Write(property.Key);
            WritePropertyValue(writer, property.Value);
        }
    }

    /// <summary>
    /// Lit une arête depuis le stream binaire
    /// </summary>
    private Edge ReadEdge(BinaryReader reader)
    {
        var id = new Guid(reader.ReadBytes(16));
        var fromNodeId = new Guid(reader.ReadBytes(16));
        var toNodeId = new Guid(reader.ReadBytes(16));
        var relationType = reader.ReadString();
        var createdAt = DateTime.FromBinary(reader.ReadInt64());
        var updatedAt = DateTime.FromBinary(reader.ReadInt64());

        // Lire les propriétés
        var propertyCount = reader.ReadInt32();
        var properties = new Dictionary<string, object>();
        
        for (int i = 0; i < propertyCount; i++)
        {
            var key = reader.ReadString();
            var value = ReadPropertyValue(reader);
            properties[key] = value;
        }

        return new Edge
        {
            Id = id,
            FromNodeId = fromNodeId,
            ToNodeId = toNodeId,
            RelationType = relationType,
            Properties = properties,
            CreatedAt = createdAt,
            UpdatedAt = updatedAt
        };
    }

    /// <summary>
    /// Écrit une arête dans le stream binaire
    /// </summary>
    private void WriteEdge(BinaryWriter writer, Edge edge)
    {
        writer.Write(edge.Id.ToByteArray());
        writer.Write(edge.FromNodeId.ToByteArray());
        writer.Write(edge.ToNodeId.ToByteArray());
        writer.Write(edge.RelationType);
        writer.Write(edge.CreatedAt.ToBinary());
        writer.Write(edge.UpdatedAt.ToBinary());

        // Écrire les propriétés
        writer.Write(edge.Properties.Count);
        foreach (var property in edge.Properties)
        {
            writer.Write(property.Key);
            WritePropertyValue(writer, property.Value);
        }
    }

    /// <summary>
    /// Lit une valeur de propriété typée depuis le stream binaire
    /// </summary>
    private object ReadPropertyValue(BinaryReader reader)
    {
        var typeCode = reader.ReadByte();
        return typeCode switch
        {
            0 => reader.ReadString(),              // String
            1 => reader.ReadInt32(),               // Int32
            2 => reader.ReadInt64(),               // Int64
            3 => reader.ReadDouble(),              // Double
            4 => reader.ReadBoolean(),             // Boolean
            5 => DateTime.FromBinary(reader.ReadInt64()), // DateTime
            _ => throw new InvalidDataException($"Type de propriété non supporté: {typeCode}")
        };
    }

    /// <summary>
    /// Écrit une valeur de propriété typée dans le stream binaire
    /// </summary>
    private void WritePropertyValue(BinaryWriter writer, object value)
    {
        switch (value)
        {
            case string s:
                writer.Write((byte)0);
                writer.Write(s);
                break;
            case int i:
                writer.Write((byte)1);
                writer.Write(i);
                break;
            case long l:
                writer.Write((byte)2);
                writer.Write(l);
                break;
            case double d:
                writer.Write((byte)3);
                writer.Write(d);
                break;
            case bool b:
                writer.Write((byte)4);
                writer.Write(b);
                break;
            case DateTime dt:
                writer.Write((byte)5);
                writer.Write(dt.ToBinary());
                break;
            default:
                // Convertir en string par défaut
                writer.Write((byte)0);
                writer.Write(value.ToString() ?? "");
                break;
        }
    }

    /// <summary>
    /// Structure pour la sérialisation des données du graphe
    /// </summary>
    private class GraphData
    {
        public List<Node> Nodes { get; set; } = new();
        public List<Edge> Edges { get; set; } = new();
    }

    /// <summary>
    /// Ajoute un nœud à la base de données
    /// </summary>
    public void AddNode(Node node)
    {
        lock (_lockObject)
        {
            _nodes[node.Id] = node;
        }
    }

    /// <summary>
    /// Ajoute une arête à la base de données
    /// </summary>
    public void AddEdge(Edge edge)
    {
        lock (_lockObject)
        {
            // Vérifier que les nœuds source et destination existent
            if (!_nodes.ContainsKey(edge.FromNodeId) || !_nodes.ContainsKey(edge.ToNodeId))
            {
                throw new InvalidOperationException("Les nœuds source et destination doivent exister avant de créer une arête");
            }
            
            _edges[edge.Id] = edge;
        }
    }

    /// <summary>
    /// Obtient un nœud par son ID
    /// </summary>
    public Node? GetNode(Guid id)
    {
        lock (_lockObject)
        {
            return _nodes.TryGetValue(id, out var node) ? node : null;
        }
    }

    /// <summary>
    /// Obtient une arête par son ID
    /// </summary>
    public Edge? GetEdge(Guid id)
    {
        lock (_lockObject)
        {
            return _edges.TryGetValue(id, out var edge) ? edge : null;
        }
    }

    /// <summary>
    /// Obtient tous les nœuds
    /// </summary>
    public List<Node> GetAllNodes()
    {
        lock (_lockObject)
        {
            return _nodes.Values.ToList();
        }
    }

    /// <summary>
    /// Obtient toutes les arêtes
    /// </summary>
    public List<Edge> GetAllEdges()
    {
        lock (_lockObject)
        {
            return _edges.Values.ToList();
        }
    }

    /// <summary>
    /// Obtient les nœuds par label
    /// </summary>
    public List<Node> GetNodesByLabel(string label)
    {
        lock (_lockObject)
        {
            return _nodes.Values
                .Where(n => n.Label.Equals(label, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }
    }

    /// <summary>
    /// Obtient les arêtes connectées à un nœud
    /// </summary>
    public List<Edge> GetEdgesForNode(Guid nodeId)
    {
        lock (_lockObject)
        {
            return _edges.Values
                .Where(e => e.FromNodeId == nodeId || e.ToNodeId == nodeId)
                .ToList();
        }
    }

    /// <summary>
    /// Supprime un nœud et toutes ses arêtes
    /// </summary>
    public bool RemoveNode(Guid nodeId)
    {
        lock (_lockObject)
        {
            // Supprimer toutes les arêtes connectées
            var connectedEdges = _edges.Values
                .Where(e => e.FromNodeId == nodeId || e.ToNodeId == nodeId)
                .ToList();

            foreach (var edge in connectedEdges)
            {
                _edges.Remove(edge.Id);
            }

            // Supprimer le nœud
            return _nodes.Remove(nodeId);
        }
    }

    /// <summary>
    /// Supprime une arête
    /// </summary>
    public bool RemoveEdge(Guid edgeId)
    {
        lock (_lockObject)
        {
            return _edges.Remove(edgeId);
        }
    }

    /// <summary>
    /// Résultat du chargement de la base de données
    /// </summary>
    public class LoadResult
    {
        public bool Success { get; set; }
        public bool IsNewDatabase { get; set; }
        public string Message { get; set; } = string.Empty;
    }
}
