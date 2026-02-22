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
    public static WorkspacePaths WorkspacePaths => EnsureLoaded().WorkspacePaths;
    public static GeneratorSettings GeneratorSettings => EnsureLoaded().GeneratorSettings;

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
        root.Add(BuildWorkspacePaths(snapshot.WorkspacePaths));
        root.Add(BuildGeneratorSettings(snapshot.GeneratorSettings));

        return new XDocument(new XDeclaration("1.0", "utf-8", null), root);
    }

    private static MetaWorkspaceData Parse(XDocument document, string sourcePath)
    {
        var root = document.Root ?? throw new InvalidDataException("workspace.xml has no root element.");
        if (!string.Equals(root.Name.LocalName, MetaWorkspaceModels.ModelName, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"workspace.xml root must be <{MetaWorkspaceModels.ModelName}>.");
        }

        var workspaces = ParseWorkspaces(root.Element("Workspaces"), sourcePath);
        var workspacePaths = ParseWorkspacePaths(root.Element("WorkspacePaths"), sourcePath);
        var generatorSettings = ParseGeneratorSettings(root.Element("GeneratorSettings"), sourcePath);
        return new MetaWorkspaceData(
            workspaces,
            workspacePaths,
            generatorSettings);
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

    private static WorkspacePaths ParseWorkspacePaths(XElement? container, string sourcePath)
    {
        if (container == null)
        {
            return new WorkspacePaths(Array.Empty<WorkspacePathRow>());
        }

        var rows = container.Elements()
            .Select(row => ParseWorkspacePathRow(row, sourcePath))
            .OrderBy(row => row.Id)
            .ToList();
        return new WorkspacePaths(rows);
    }

    private static GeneratorSettings ParseGeneratorSettings(XElement? container, string sourcePath)
    {
        if (container == null)
        {
            return new GeneratorSettings(Array.Empty<GeneratorSettingRow>());
        }

        var rows = container.Elements()
            .Select(row => ParseGeneratorSettingRow(row, sourcePath))
            .OrderBy(row => row.Id)
            .ToList();
        return new GeneratorSettings(rows);
    }

    private static WorkspaceRow ParseWorkspaceRow(XElement row, string sourcePath)
    {
        EnsureElementName(row, "Workspace", sourcePath);
        var id = ParsePositiveIdentity(ReadRequiredAttribute(row, "Id", sourcePath), "Workspace.Id", sourcePath);
        var name = ReadRequiredPropertyElement(row, "Name", sourcePath);
        var formatVersion = ReadRequiredPropertyElement(row, "FormatVersion", sourcePath);
        EnsureNoExtraAttributes(row, new[] { "Id" }, sourcePath);
        EnsureNoUnexpectedPropertyElements(row, new[] { "Name", "FormatVersion" }, sourcePath);
        return new WorkspaceRow(id, name, formatVersion);
    }

    private static WorkspacePathRow ParseWorkspacePathRow(XElement row, string sourcePath)
    {
        EnsureElementName(row, "WorkspacePath", sourcePath);
        var id = ParsePositiveIdentity(ReadRequiredAttribute(row, "Id", sourcePath), "WorkspacePath.Id", sourcePath);
        var workspaceId = ParsePositiveIdentity(
            ReadRequiredAttribute(row, "WorkspaceId", sourcePath),
            "WorkspacePath.WorkspaceId",
            sourcePath);
        var key = ReadRequiredPropertyElement(row, "Key", sourcePath);
        var path = ReadRequiredPropertyElement(row, "Path", sourcePath);
        EnsureNoExtraAttributes(row, new[] { "Id", "WorkspaceId" }, sourcePath);
        EnsureNoUnexpectedPropertyElements(row, new[] { "Key", "Path" }, sourcePath);
        return new WorkspacePathRow(id, workspaceId, key, path);
    }

    private static GeneratorSettingRow ParseGeneratorSettingRow(XElement row, string sourcePath)
    {
        EnsureElementName(row, "GeneratorSetting", sourcePath);
        var id = ParsePositiveIdentity(
            ReadRequiredAttribute(row, "Id", sourcePath),
            "GeneratorSetting.Id",
            sourcePath);
        var workspaceId = ParsePositiveIdentity(
            ReadRequiredAttribute(row, "WorkspaceId", sourcePath),
            "GeneratorSetting.WorkspaceId",
            sourcePath);
        var key = ReadRequiredPropertyElement(row, "Key", sourcePath);
        var value = ReadRequiredPropertyElement(row, "Value", sourcePath);
        EnsureNoExtraAttributes(row, new[] { "Id", "WorkspaceId" }, sourcePath);
        EnsureNoUnexpectedPropertyElements(row, new[] { "Key", "Value" }, sourcePath);
        return new GeneratorSettingRow(id, workspaceId, key, value);
    }

    private static XElement BuildWorkspaces(Workspaces workspaces)
    {
        var container = new XElement("Workspaces");
        foreach (var row in workspaces)
        {
            container.Add(new XElement("Workspace",
                new XAttribute("Id", row.Id.ToString(CultureInfo.InvariantCulture)),
                new XElement("Name", row.Name),
                new XElement("FormatVersion", row.FormatVersion)));
        }

        return container;
    }

    private static XElement BuildWorkspacePaths(WorkspacePaths workspacePaths)
    {
        var container = new XElement("WorkspacePaths");
        foreach (var row in workspacePaths)
        {
            container.Add(new XElement("WorkspacePath",
                new XAttribute("Id", row.Id.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("WorkspaceId", row.WorkspaceId.ToString(CultureInfo.InvariantCulture)),
                new XElement("Key", row.Key),
                new XElement("Path", row.Path)));
        }

        return container;
    }

    private static XElement BuildGeneratorSettings(GeneratorSettings settings)
    {
        var container = new XElement("GeneratorSettings");
        foreach (var row in settings)
        {
            container.Add(new XElement("GeneratorSetting",
                new XAttribute("Id", row.Id.ToString(CultureInfo.InvariantCulture)),
                new XAttribute("WorkspaceId", row.WorkspaceId.ToString(CultureInfo.InvariantCulture)),
                new XElement("Key", row.Key),
                new XElement("Value", row.Value)));
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

        throw new InvalidDataException(
            $"workspace.xml '{sourcePath}' contains unexpected element '{row.Name.LocalName}'. Expected '{expectedName}'.");
    }

    private static string ReadRequiredAttribute(XElement row, string name, string sourcePath)
    {
        var attribute = row.Attribute(name)?.Value;
        if (!string.IsNullOrWhiteSpace(attribute))
        {
            return attribute.Trim();
        }

        throw new InvalidDataException(
            $"workspace.xml '{sourcePath}' row '{row.Name.LocalName}' is missing required attribute '{name}'.");
    }

    private static string ReadRequiredPropertyElement(XElement row, string name, string sourcePath)
    {
        var element = row.Elements()
            .FirstOrDefault(item => string.Equals(item.Name.LocalName, name, StringComparison.OrdinalIgnoreCase));
        if (element != null)
        {
            return element.Value;
        }

        throw new InvalidDataException(
            $"workspace.xml '{sourcePath}' row '{row.Name.LocalName}' is missing required property element '{name}'.");
    }

    private static void EnsureNoExtraAttributes(XElement row, IReadOnlyCollection<string> allowedAttributes, string sourcePath)
    {
        foreach (var attribute in row.Attributes())
        {
            if (!allowedAttributes.Contains(attribute.Name.LocalName, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"workspace.xml '{sourcePath}' row '{row.Name.LocalName}' has unsupported attribute '{attribute.Name.LocalName}'.");
            }
        }
    }

    private static void EnsureNoUnexpectedPropertyElements(
        XElement row,
        IReadOnlyCollection<string> expectedProperties,
        string sourcePath)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var element in row.Elements())
        {
            var name = element.Name.LocalName;
            if (!expectedProperties.Contains(name, StringComparer.OrdinalIgnoreCase))
            {
                throw new InvalidDataException(
                    $"workspace.xml '{sourcePath}' row '{row.Name.LocalName}' has unknown property element '{name}'.");
            }

            if (!seen.Add(name))
            {
                throw new InvalidDataException(
                    $"workspace.xml '{sourcePath}' row '{row.Name.LocalName}' has duplicate property element '{name}'.");
            }
        }
    }

    private static int ParsePositiveIdentity(string value, string fieldName, string sourcePath)
    {
        if (!int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0)
        {
            throw new InvalidDataException(
                $"workspace.xml '{sourcePath}' field '{fieldName}' has invalid identity value '{value}'.");
        }

        return parsed;
    }
}

public sealed class MetaWorkspaceData
{
    internal MetaWorkspaceData(
        Workspaces workspaces,
        WorkspacePaths workspacePaths,
        GeneratorSettings generatorSettings)
    {
        Workspaces = workspaces ?? throw new ArgumentNullException(nameof(workspaces));
        WorkspacePaths = workspacePaths ?? throw new ArgumentNullException(nameof(workspacePaths));
        GeneratorSettings = generatorSettings ?? throw new ArgumentNullException(nameof(generatorSettings));
    }

    public Workspaces Workspaces { get; }
    public WorkspacePaths WorkspacePaths { get; }
    public GeneratorSettings GeneratorSettings { get; }
}

public sealed record WorkspaceRow(
    int Id,
    string Name,
    string FormatVersion);

public sealed record WorkspacePathRow(
    int Id,
    int WorkspaceId,
    string Key,
    string Path);

public sealed record GeneratorSettingRow(
    int Id,
    int WorkspaceId,
    string Key,
    string Value);

public sealed class Workspaces : IEnumerable<WorkspaceRow>
{
    private readonly List<WorkspaceRow> rows;
    private readonly Dictionary<int, WorkspaceRow> byId;

    public Workspaces(IEnumerable<WorkspaceRow> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        rows = source.OrderBy(item => item.Id).ToList();
        byId = rows.ToDictionary(item => item.Id, item => item);
    }

    public WorkspaceRow GetId(int id)
    {
        if (!byId.TryGetValue(id, out var row))
        {
            throw new KeyNotFoundException($"Workspace id '{id}' was not found.");
        }

        return row;
    }

    public bool TryGetId(int id, out WorkspaceRow row)
    {
        return byId.TryGetValue(id, out row!);
    }

    public IEnumerator<WorkspaceRow> GetEnumerator() => rows.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class WorkspacePaths : IEnumerable<WorkspacePathRow>
{
    private readonly List<WorkspacePathRow> rows;
    private readonly Dictionary<int, WorkspacePathRow> byId;

    public WorkspacePaths(IEnumerable<WorkspacePathRow> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        rows = source.OrderBy(item => item.Id).ToList();
        byId = rows.ToDictionary(item => item.Id, item => item);
    }

    public WorkspacePathRow GetId(int id)
    {
        if (!byId.TryGetValue(id, out var row))
        {
            throw new KeyNotFoundException($"WorkspacePath id '{id}' was not found.");
        }

        return row;
    }

    public bool TryGetId(int id, out WorkspacePathRow row)
    {
        return byId.TryGetValue(id, out row!);
    }

    public IEnumerator<WorkspacePathRow> GetEnumerator() => rows.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public sealed class GeneratorSettings : IEnumerable<GeneratorSettingRow>
{
    private readonly List<GeneratorSettingRow> rows;
    private readonly Dictionary<int, GeneratorSettingRow> byId;

    public GeneratorSettings(IEnumerable<GeneratorSettingRow> source)
    {
        ArgumentNullException.ThrowIfNull(source);
        rows = source.OrderBy(item => item.Id).ToList();
        byId = rows.ToDictionary(item => item.Id, item => item);
    }

    public GeneratorSettingRow GetId(int id)
    {
        if (!byId.TryGetValue(id, out var row))
        {
            throw new KeyNotFoundException($"GeneratorSetting id '{id}' was not found.");
        }

        return row;
    }

    public bool TryGetId(int id, out GeneratorSettingRow row)
    {
        return byId.TryGetValue(id, out row!);
    }

    public IEnumerator<GeneratorSettingRow> GetEnumerator() => rows.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}
