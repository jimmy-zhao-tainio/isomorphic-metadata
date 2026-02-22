using System.Collections;
using System.Globalization;
using System.Xml.Linq;

#nullable enable

namespace MetadataStudio.Core.WorkspaceConfig;

public static class MetaWorkspace
{
    private static readonly object SyncRoot = new();
    private static MetaWorkspaceData? data;

    public static Workspaces Workspaces => EnsureLoaded().Workspaces;
    public static WorkspaceLayouts WorkspaceLayouts => EnsureLoaded().WorkspaceLayouts;
    public static Encodings Encodings => EnsureLoaded().Encodings;
    public static NewlinesValues NewlinesValues => EnsureLoaded().NewlinesValues;
    public static CanonicalOrders CanonicalOrders => EnsureLoaded().CanonicalOrders;
    public static EntityStorages EntityStorages => EnsureLoaded().EntityStorages;

    internal static void Install(MetaWorkspaceData snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        lock (SyncRoot)
        {
            data = snapshot;
        }
    }

    private static MetaWorkspaceData EnsureLoaded()
    {
        var snapshot = data;
        if (snapshot == null)
        {
            throw new InvalidOperationException(
                "MetaWorkspace is not loaded. Call MetaWorkspaceModel.LoadFromXml first.");
        }

        return snapshot;
    }
}

public static class MetaWorkspaceModel
{
    public static MetaWorkspaceData LoadFromXml(string workspacePath)
    {
        if (string.IsNullOrWhiteSpace(workspacePath))
        {
            throw new ArgumentException("Workspace path is required.", nameof(workspacePath));
        }

        var xmlPath = ResolveWorkspaceXmlPath(workspacePath);
        return LoadFromWorkspaceXmlFile(xmlPath);
    }

    public static MetaWorkspaceData LoadFromWorkspaceXmlFile(string workspaceXmlPath)
    {
        if (string.IsNullOrWhiteSpace(workspaceXmlPath))
        {
            throw new ArgumentException("Workspace XML path is required.", nameof(workspaceXmlPath));
        }

        var fullPath = Path.GetFullPath(workspaceXmlPath);
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException($"Workspace config file was not found: {fullPath}", fullPath);
        }

        var document = XDocument.Load(fullPath, LoadOptions.None);
        var snapshot = Parse(document, fullPath);
        MetaWorkspace.Install(snapshot);
        return snapshot;
    }

    public static XDocument BuildDocument(MetaWorkspaceData snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var root = new XElement(MetaWorkspaceModels.ModelName);
        root.Add(BuildWorkspaces(snapshot.Workspaces));
        root.Add(BuildWorkspaceLayouts(snapshot.WorkspaceLayouts));
        root.Add(BuildNamedRows("Encodings", "Encoding", snapshot.Encodings));
        root.Add(BuildNamedRows("NewlinesValues", "Newlines", snapshot.NewlinesValues));
        root.Add(BuildNamedRows("CanonicalOrders", "CanonicalOrder", snapshot.CanonicalOrders));
        root.Add(BuildEntityStorages(snapshot.EntityStorages));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private static MetaWorkspaceData Parse(XDocument document, string sourcePath)
    {
        var root = document.Root ?? throw new InvalidDataException("workspace.xml has no root element.");
        if (!string.Equals(root.Name.LocalName, MetaWorkspaceModels.ModelName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException($"workspace.xml root must be <{MetaWorkspaceModels.ModelName}>.");
        }

        return new MetaWorkspaceData(
            ParseWorkspaces(root.Element("Workspaces"), sourcePath),
            ParseWorkspaceLayouts(root.Element("WorkspaceLayouts"), sourcePath),
            ParseNamedRows<EncodingRow, Encodings>(root.Element("Encodings"), "Encoding", "Encoding", sourcePath, (id, name) => new EncodingRow(id, name)),
            ParseNamedRows<NewlinesRow, NewlinesValues>(root.Element("NewlinesValues"), "Newlines", "Newlines", sourcePath, (id, name) => new NewlinesRow(id, name)),
            ParseNamedRows<CanonicalOrderRow, CanonicalOrders>(root.Element("CanonicalOrders"), "CanonicalOrder", "CanonicalOrder", sourcePath, (id, name) => new CanonicalOrderRow(id, name)),
            ParseEntityStorages(root.Element("EntityStorages"), sourcePath));
    }

    private static Workspaces ParseWorkspaces(XElement? container, string sourcePath)
    {
        if (container == null)
        {
            return new Workspaces(Array.Empty<WorkspaceRow>());
        }

        var rows = container.Elements()
            .Select(row => ParseWorkspaceRow(row, sourcePath))
            .OrderBy(row => row.Id)
            .ToList();
        return new Workspaces(rows);
    }

    private static WorkspaceLayouts ParseWorkspaceLayouts(XElement? container, string sourcePath)
    {
        if (container == null)
        {
            return new WorkspaceLayouts(Array.Empty<WorkspaceLayoutRow>());
        }

        var rows = container.Elements()
            .Select(row => ParseWorkspaceLayoutRow(row, sourcePath))
            .OrderBy(row => row.Id)
            .ToList();
        return new WorkspaceLayouts(rows);
    }

    private static TSet ParseNamedRows<TRow, TSet>(
        XElement? container,
        string elementName,
        string fieldPrefix,
        string sourcePath,
        Func<int, string, TRow> factory)
        where TSet : IdRowSet<TRow>
        where TRow : INamedRow
    {
        var rows = new List<TRow>();
        if (container != null)
        {
            rows = container.Elements()
                .Select(row =>
                {
                    EnsureElementName(row, elementName, sourcePath);
                    var id = ParsePositiveIdentity(ReadRequiredAttribute(row, "Id", sourcePath), $"{fieldPrefix}.Id", sourcePath);
                    var name = ReadRequiredPropertyElement(row, "Name", sourcePath);
                    EnsureNoExtraAttributes(row, new[] { "Id" }, sourcePath);
                    EnsureNoUnexpectedPropertyElements(row, new[] { "Name" }, sourcePath);
                    return factory(id, name);
                })
                .OrderBy(row => row.Id)
                .ToList();
        }

        return (TSet)Activator.CreateInstance(typeof(TSet), rows)!;
    }

    private static EntityStorages ParseEntityStorages(XElement? container, string sourcePath)
    {
        if (container == null)
        {
            return new EntityStorages(Array.Empty<EntityStorageRow>());
        }

        var rows = container.Elements()
            .Select(row => ParseEntityStorageRow(row, sourcePath))
            .OrderBy(row => row.Id)
            .ToList();
        return new EntityStorages(rows);
    }

    private static WorkspaceRow ParseWorkspaceRow(XElement row, string sourcePath)
    {
        EnsureElementName(row, "Workspace", sourcePath);
        var id = ParsePositiveIdentity(ReadRequiredAttribute(row, "Id", sourcePath), "Workspace.Id", sourcePath);
        var workspaceLayoutId = ParsePositiveIdentity(ReadRequiredAttribute(row, "WorkspaceLayoutId", sourcePath), "Workspace.WorkspaceLayoutId", sourcePath);
        var encodingId = ParsePositiveIdentity(ReadRequiredAttribute(row, "EncodingId", sourcePath), "Workspace.EncodingId", sourcePath);
        var newlinesId = ParsePositiveIdentity(ReadRequiredAttribute(row, "NewlinesId", sourcePath), "Workspace.NewlinesId", sourcePath);
        var entitiesOrderId = ParsePositiveIdentity(ReadRequiredAttribute(row, "EntitiesOrderId", sourcePath), "Workspace.EntitiesOrderId", sourcePath);
        var propertiesOrderId = ParsePositiveIdentity(ReadRequiredAttribute(row, "PropertiesOrderId", sourcePath), "Workspace.PropertiesOrderId", sourcePath);
        var relationshipsOrderId = ParsePositiveIdentity(ReadRequiredAttribute(row, "RelationshipsOrderId", sourcePath), "Workspace.RelationshipsOrderId", sourcePath);
        var rowsOrderId = ParsePositiveIdentity(ReadRequiredAttribute(row, "RowsOrderId", sourcePath), "Workspace.RowsOrderId", sourcePath);
        var attributesOrderId = ParsePositiveIdentity(ReadRequiredAttribute(row, "AttributesOrderId", sourcePath), "Workspace.AttributesOrderId", sourcePath);
        var name = ReadRequiredPropertyElement(row, "Name", sourcePath);
        var formatVersion = ReadRequiredPropertyElement(row, "FormatVersion", sourcePath);

        EnsureNoExtraAttributes(
            row,
            new[]
            {
                "Id",
                "WorkspaceLayoutId",
                "EncodingId",
                "NewlinesId",
                "EntitiesOrderId",
                "PropertiesOrderId",
                "RelationshipsOrderId",
                "RowsOrderId",
                "AttributesOrderId",
            },
            sourcePath);
        EnsureNoUnexpectedPropertyElements(row, new[] { "Name", "FormatVersion" }, sourcePath);

        return new WorkspaceRow(
            id,
            name,
            formatVersion,
            workspaceLayoutId,
            encodingId,
            newlinesId,
            entitiesOrderId,
            propertiesOrderId,
            relationshipsOrderId,
            rowsOrderId,
            attributesOrderId);
    }

    private static WorkspaceLayoutRow ParseWorkspaceLayoutRow(XElement row, string sourcePath)
    {
        EnsureElementName(row, "WorkspaceLayout", sourcePath);
        var id = ParsePositiveIdentity(ReadRequiredAttribute(row, "Id", sourcePath), "WorkspaceLayout.Id", sourcePath);
        var modelFilePath = ReadRequiredPropertyElement(row, "ModelFilePath", sourcePath);
        var instanceDirPath = ReadRequiredPropertyElement(row, "InstanceDirPath", sourcePath);
        EnsureNoExtraAttributes(row, new[] { "Id" }, sourcePath);
        EnsureNoUnexpectedPropertyElements(row, new[] { "ModelFilePath", "InstanceDirPath" }, sourcePath);
        return new WorkspaceLayoutRow(id, modelFilePath, instanceDirPath);
    }

    private static EntityStorageRow ParseEntityStorageRow(XElement row, string sourcePath)
    {
        EnsureElementName(row, "EntityStorage", sourcePath);
        var id = ParsePositiveIdentity(ReadRequiredAttribute(row, "Id", sourcePath), "EntityStorage.Id", sourcePath);
        var workspaceId = ParsePositiveIdentity(ReadRequiredAttribute(row, "WorkspaceId", sourcePath), "EntityStorage.WorkspaceId", sourcePath);
        var entityName = ReadRequiredPropertyElement(row, "EntityName", sourcePath);
        var storageKind = ReadRequiredPropertyElement(row, "StorageKind", sourcePath);
        var directoryPath = ReadOptionalPropertyElement(row, "DirectoryPath");
        var filePath = ReadOptionalPropertyElement(row, "FilePath");
        var pattern = ReadOptionalPropertyElement(row, "Pattern");
        EnsureNoExtraAttributes(row, new[] { "Id", "WorkspaceId" }, sourcePath);
        EnsureNoUnexpectedPropertyElements(row, new[] { "EntityName", "StorageKind", "DirectoryPath", "FilePath", "Pattern" }, sourcePath);
        return new EntityStorageRow(id, workspaceId, entityName, storageKind, directoryPath, filePath, pattern);
    }

    private static XElement BuildWorkspaces(Workspaces workspaces)
    {
        var container = new XElement("Workspaces");
        foreach (var row in workspaces)
        {
            container.Add(new XElement("Workspace",
                new XAttribute("Id", row.Id.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("WorkspaceLayoutId", row.WorkspaceLayoutId.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("EncodingId", row.EncodingId.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("NewlinesId", row.NewlinesId.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("EntitiesOrderId", row.EntitiesOrderId.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("PropertiesOrderId", row.PropertiesOrderId.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("RelationshipsOrderId", row.RelationshipsOrderId.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("RowsOrderId", row.RowsOrderId.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("AttributesOrderId", row.AttributesOrderId.ToString(CultureInfo.InvariantCulture)),
                new XElement("Name", row.Name),
                new XElement("FormatVersion", row.FormatVersion)));
        }

        return container;
    }

    private static XElement BuildWorkspaceLayouts(WorkspaceLayouts rows)
    {
        var container = new XElement("WorkspaceLayouts");
        foreach (var row in rows)
        {
            container.Add(new XElement("WorkspaceLayout",
                new XAttribute("Id", row.Id.ToString(CultureInfo.InvariantCulture)),
                new XElement("ModelFilePath", row.ModelFilePath),
                new XElement("InstanceDirPath", row.InstanceDirPath)));
        }

        return container;
    }

    private static XElement BuildNamedRows<TRow>(string containerName, string elementName, IdRowSet<TRow> rows)
        where TRow : INamedRow
    {
        var container = new XElement(containerName);
        foreach (var row in rows)
        {
            container.Add(new XElement(elementName,
                new XAttribute("Id", row.Id.ToString(CultureInfo.InvariantCulture)),
                new XElement("Name", row.Name)));
        }

        return container;
    }

    private static XElement BuildEntityStorages(EntityStorages rows)
    {
        var container = new XElement("EntityStorages");
        foreach (var row in rows)
        {
            var element = new XElement("EntityStorage",
                new XAttribute("Id", row.Id.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("WorkspaceId", row.WorkspaceId.ToString(CultureInfo.InvariantCulture)),
                new XElement("EntityName", row.EntityName),
                new XElement("StorageKind", row.StorageKind));

            if (!string.IsNullOrWhiteSpace(row.DirectoryPath))
            {
                element.Add(new XElement("DirectoryPath", row.DirectoryPath));
            }

            if (!string.IsNullOrWhiteSpace(row.FilePath))
            {
                element.Add(new XElement("FilePath", row.FilePath));
            }

            if (!string.IsNullOrWhiteSpace(row.Pattern))
            {
                element.Add(new XElement("Pattern", row.Pattern));
            }

            container.Add(element);
        }

        return container;
    }

    private static string ResolveWorkspaceXmlPath(string workspacePath)
    {
        var fullPath = Path.GetFullPath(workspacePath);
        if (Directory.Exists(fullPath))
        {
            if (string.Equals(Path.GetFileName(fullPath), "metadata", StringComparison.OrdinalIgnoreCase))
            {
                return Path.Combine(fullPath, "workspace.xml");
            }

            return Path.Combine(fullPath, "metadata", "workspace.xml");
        }

        return fullPath;
    }

    private static void EnsureElementName(XElement row, string expectedName, string sourcePath)
    {
        if (string.Equals(row.Name.LocalName, expectedName, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        throw new InvalidDataException($"workspace.xml '{sourcePath}' contains unexpected element '{row.Name.LocalName}'. Expected '{expectedName}'.");
    }

    private static string ReadRequiredAttribute(XElement row, string name, string sourcePath)
    {
        var attribute = row.Attribute(name)?.Value;
        if (!string.IsNullOrWhiteSpace(attribute))
        {
            return attribute.Trim();
        }

        throw new InvalidDataException($"workspace.xml '{sourcePath}' row '{row.Name.LocalName}' is missing required attribute '{name}'.");
    }

    private static string ReadRequiredPropertyElement(XElement row, string name, string sourcePath)
    {
        var element = row.Elements().FirstOrDefault(item => string.Equals(item.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
        if (element != null)
        {
            return element.Value;
        }

        throw new InvalidDataException($"workspace.xml '{sourcePath}' row '{row.Name.LocalName}' is missing required property element '{name}'.");
    }

    private static string ReadOptionalPropertyElement(XElement row, string name)
    {
        var element = row.Elements().FirstOrDefault(item => string.Equals(item.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
        return element?.Value ?? string.Empty;
    }

    private static void EnsureNoExtraAttributes(XElement row, IReadOnlyCollection<string> allowedAttributes, string sourcePath)
    {
        foreach (var attribute in row.Attributes())
        {
            if (!allowedAttributes.Contains(attribute.Name.LocalName, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"workspace.xml '{sourcePath}' row '{row.Name.LocalName}' has unsupported attribute '{attribute.Name.LocalName}'.");
            }
        }
    }

    private static void EnsureNoUnexpectedPropertyElements(XElement row, IReadOnlyCollection<string> expectedProperties, string sourcePath)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in row.Elements())
        {
            var name = element.Name.LocalName;
            if (!expectedProperties.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"workspace.xml '{sourcePath}' row '{row.Name.LocalName}' has unknown property element '{name}'.");
            }

            if (!seen.Add(name))
            {
                throw new InvalidDataException($"workspace.xml '{sourcePath}' row '{row.Name.LocalName}' has duplicate property element '{name}'.");
            }
        }
    }

    private static int ParsePositiveIdentity(string value, string fieldName, string sourcePath)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            throw new InvalidDataException($"workspace.xml '{sourcePath}' field '{fieldName}' has invalid identity value '{value}'.");
        }

        return parsed;
    }
}

public sealed class MetaWorkspaceData
{
    internal MetaWorkspaceData(
        Workspaces workspaces,
        WorkspaceLayouts workspaceLayouts,
        Encodings encodings,
        NewlinesValues newlinesValues,
        CanonicalOrders canonicalOrders,
        EntityStorages entityStorages)
    {
        Workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
        WorkspaceLayouts = workspaceLayouts ?? throw new ArgumentNullException(nameof(workspaceLayouts));
        Encodings = encodings ?? throw new ArgumentNullException(nameof(encodings));
        NewlinesValues = newlinesValues ?? throw new ArgumentNullException(nameof(newlinesValues));
        CanonicalOrders = canonicalOrders ?? throw new ArgumentNullException(nameof(canonicalOrders));
        EntityStorages = entityStorages ?? throw new ArgumentNullException(nameof(entityStorages));
    }

    public Workspaces Workspaces { get; }
    public WorkspaceLayouts WorkspaceLayouts { get; }
    public Encodings Encodings { get; }
    public NewlinesValues NewlinesValues { get; }
    public CanonicalOrders CanonicalOrders { get; }
    public EntityStorages EntityStorages { get; }
}

public interface IIdRow
{
    int Id { get; }
}

public interface INamedRow : IIdRow
{
    string Name { get; }
}

public abstract class IdRowSet<TRow> : IEnumerable<TRow> where TRow : IIdRow
{
    private readonly List<TRow> rows;
    private readonly Dictionary<int, TRow> byId;
    private readonly string label;

    protected IdRowSet(IEnumerable<TRow> source, string label)
    {
        ArgumentNullException.ThrowIfNull(source);
        this.label = label;
        rows = source.OrderBy(item => item.Id).ToList();
        byId = rows.ToDictionary(item => item.Id, item => item);
    }

    public TRow GetId(int id) => byId.TryGetValue(id, out var row) ? row : throw new KeyNotFoundException($"{label} id '{id}' was not found.");
    public bool TryGetId(int id, out TRow row) => byId.TryGetValue(id, out row!);
    public IEnumerator<TRow> GetEnumerator() => rows.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed record WorkspaceRow(int Id, string Name, string FormatVersion, int WorkspaceLayoutId, int EncodingId, int NewlinesId, int EntitiesOrderId, int PropertiesOrderId, int RelationshipsOrderId, int RowsOrderId, int AttributesOrderId) : IIdRow;
public sealed record WorkspaceLayoutRow(int Id, string ModelFilePath, string InstanceDirPath) : IIdRow;
public sealed record EncodingRow(int Id, string Name) : INamedRow;
public sealed record NewlinesRow(int Id, string Name) : INamedRow;
public sealed record CanonicalOrderRow(int Id, string Name) : INamedRow;
public sealed record EntityStorageRow(int Id, int WorkspaceId, string EntityName, string StorageKind, string DirectoryPath, string FilePath, string Pattern) : IIdRow;

public sealed class Workspaces : IdRowSet<WorkspaceRow> { public Workspaces(IEnumerable<WorkspaceRow> source) : base(source, "Workspace") { } }
public sealed class WorkspaceLayouts : IdRowSet<WorkspaceLayoutRow> { public WorkspaceLayouts(IEnumerable<WorkspaceLayoutRow> source) : base(source, "WorkspaceLayout") { } }
public sealed class Encodings : IdRowSet<EncodingRow> { public Encodings(IEnumerable<EncodingRow> source) : base(source, "Encoding") { } }
public sealed class NewlinesValues : IdRowSet<NewlinesRow> { public NewlinesValues(IEnumerable<NewlinesRow> source) : base(source, "Newlines") { } }
public sealed class CanonicalOrders : IdRowSet<CanonicalOrderRow> { public CanonicalOrders(IEnumerable<CanonicalOrderRow> source) : base(source, "CanonicalOrder") { } }
public sealed class EntityStorages : IdRowSet<EntityStorageRow> { public EntityStorages(IEnumerable<EntityStorageRow> source) : base(source, "EntityStorage") { } }
