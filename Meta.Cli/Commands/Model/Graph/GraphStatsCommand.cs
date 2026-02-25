internal sealed partial class CliRuntime
{
    async Task<int> GraphStatsAsync(string[] commandArgs)
    {
        var options = ParseGraphStatsOptions(commandArgs, startIndex: 2);
        if (!options.Ok)
        {
            return PrintArgumentError(options.ErrorMessage);
        }
    
        try
        {
            var workspace = await LoadWorkspaceForCommandAsync(options.WorkspacePath).ConfigureAwait(false);
            PrintContractCompatibilityWarning(workspace.WorkspaceConfig);
            var stats = GraphStatsService.Compute(workspace.Model, options.TopN, options.CycleSampleLimit);
    
            if (globalJson)
            {
                WriteJson(new
                {
                    command = "graph.stats",
                    workspace = workspace.WorkspaceRootPath,
                    model = workspace.Model.Name,
                    nodes = stats.NodeCount,
                    edges = new
                    {
                        declared = stats.EdgeCount,
                        unique = stats.UniqueEdgeCount,
                        duplicates = stats.DuplicateEdgeCount,
                        missingTargets = stats.MissingTargetEdgeCount,
                    },
                    components = stats.WeaklyConnectedComponents,
                    roots = stats.RootCount,
                    sinks = stats.SinkCount,
                    isolated = stats.IsolatedCount,
                    hasCycles = stats.HasCycles,
                    cycleCount = stats.CycleCount,
                    dagMaxDepth = stats.DagMaxDepth,
                    averageInDegree = stats.AverageInDegree,
                    averageOutDegree = stats.AverageOutDegree,
                    topOutDegree = stats.TopOutDegree,
                    topInDegree = stats.TopInDegree,
                    cycleSamples = stats.CycleSamples,
                });
            }
            else
            {
                PrintGraphStats(workspace, stats, options.TopN);
            }
    
            return 0;
        }
        catch (InvalidOperationException exception)
        {
            return PrintDataError("E_OPERATION", exception.Message);
        }
    }
}

