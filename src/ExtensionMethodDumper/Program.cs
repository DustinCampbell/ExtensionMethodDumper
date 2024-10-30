using System.Collections.Immutable;
using System.Diagnostics;
using ExtensionMethodDumper;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.FileSystemGlobbing;
using Microsoft.Extensions.Logging;

using var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
var logger = loggerFactory.CreateLogger("ExtensionMethodDumper");

var searchDirectoryPath = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Directory.GetCurrentDirectory();

var matcher = new Matcher();
matcher.AddInclude("*.sln");

ImmutableArray<string> solutionFilePaths = [.. matcher.GetResultsInFullPath(searchDirectoryPath)];

if (solutionFilePaths.IsEmpty)
{
    logger.LogCritical($"Did not find any solution files in {searchDirectoryPath}");
}

using var workspace = MSBuildWorkspace.Create();

var allExtensionTypes = new List<(ProjectKey project, AssemblyIdentity assembly, List<ExtensionTypeDetails> extensionTypes)>();
var projectsLoaded = new HashSet<ProjectKey>();

foreach (var solutionFilePath in solutionFilePaths)
{
    logger.LogInformation($"Loading solution: {solutionFilePath}");

    try
    {
        await OpenSolution(solutionFilePath);
    }
    catch (Exception ex)
    {
        logger.LogError(ex, $"EXCEPTION: {ex.Message}");
    }
}

async Task OpenSolution(string solutionFilePath)
{
    var projectPathToTfmMap = new Dictionary<string, ImmutableArray<string>>(StringComparer.OrdinalIgnoreCase);
    var reporter = new ProjectLoadProgressReporter(projectPathToTfmMap, logger);
    var solution = await workspace.OpenSolutionAsync(solutionFilePath, reporter);

    foreach (var project in solution.Projects)
    {
        // Skip ref projects from .NET runtime.
        if (project.FilePath is null || project.FilePath.Contains(@"\ref\"))
        {
            continue;
        }

        var projectKey = ProjectKey.From(project);
        if (!projectsLoaded.Add(projectKey))
        {
            continue;
        }

        logger.LogInformation($"Processing {project.Name} ...");
        var compilation = await project.GetCompilationAsync();
        if (compilation is null)
        {
            logger.LogError($"Could get compilation for {project.Name}");
            continue;
        }

        var extensionTypes = compilation.Assembly.GetExtensionTypes();

        logger.LogInformation($"Found {extensionTypes.Count} type(s) containing extension methods");

        if (extensionTypes.Count > 0)
        {
            allExtensionTypes.Add((projectKey, compilation.Assembly.Identity, extensionTypes));
        }
    }

    workspace.CloseSolution();
}

var typeReportFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Report-extension-types.csv");
var methodReportFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Report-extension-methods.csv");

using (var typeReport = new StreamWriter(typeReportFilePath))
using (var methodReport = new StreamWriter(methodReportFilePath))
{
    // Write headers
    typeReport.WriteCommaSeparatedLine(
        "Assembly",
        "TargetFramework",
        "Type",
        "IsPublic",
        "ExtensionMethodCount",
        "ContainsNonExtensionMembers",
        "AllExtensionsHaveSameThisParameterType");

    methodReport.WriteCommaSeparatedLine(
        "Assembly",
        "TargetFramework",
        "Type",
        "Method",
        "IsPublic",
        "IsGeneric",
        "ReducedFormParameterCount",
        "ThisParameterType",
        "ThisParameterUsesTypeParameter",
        "ThisParameterIsErrorType",
        "ThisParameterIsGenericType",
        "ThisParameterIsValueType",
        "ThisParameterIsRefKind");

    foreach (var (project, assembly, extensionTypes) in allExtensionTypes.OrderBy(static x => x.project.Name))
    {
        var assemblyDisplay = assembly.GetDisplayName();
        var targetFramework = project.TargetFramework;

        foreach (var type in extensionTypes.OrderBy(static x => x.DisplayText))
        {
            typeReport.WriteCommaSeparatedLine(
                assemblyDisplay,
                targetFramework,
                type.DisplayText,
                type.IsPublic,
                type.ExtensionMethods.Length,
                type.ContainsNonExtensionMembers,
                type.AllExtensionsHaveSameThisParameterType);

            foreach (var method in type.ExtensionMethods.OrderBy(static x => x.DisplayText))
            {
                methodReport.WriteCommaSeparatedLine(
                    assemblyDisplay,
                    targetFramework,
                    type.DisplayText,
                    method.DisplayText,
                    method.IsPublic,
                    method.IsGeneric,
                    method.ReducedFormParameterCount,
                    method.ThisParameterType.ToDisplayString(),
                    method.ThisParameterUsesTypeParameter,
                    method.ThisParameterIsErrorType,
                    method.ThisParameterIsGenericType,
                    method.ThisParameterIsValueType,
                    method.ThisParameterRefKind);
            }
        }
    }
}

readonly record struct ProjectKey(string FilePath, string Name, string TargetFramework)
{
    public static ProjectKey From(Project project)
    {
        Debug.Assert(project.FilePath is not null);

        var projectName = project.Name;
        var targetFramework = string.Empty;

        if (projectName.EndsWith(')'))
        {
            var openParen = projectName.LastIndexOf('(', projectName.Length - 1);

            if (openParen >= 0)
            {
                targetFramework = projectName[(openParen + 1)..^1];
                projectName = projectName[..openParen];
            }
        }

        if (targetFramework.Length == 0 &&
            project.CompilationOutputInfo.AssemblyPath is string assemblyPath)
        {
            var directoryName = Path.GetDirectoryName(assemblyPath);
            var parts = directoryName!.Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

            while (parts is [.. var rest, var part])
            {
                if (TargetFrameworks.Known.Contains(part))
                {
                    targetFramework = part;
                }

                parts = rest;
            }
        }

        return new(project.FilePath, projectName, targetFramework);
    }

    public bool Equals(ProjectKey other)
    {
        return StringComparer.OrdinalIgnoreCase.Equals(FilePath, other.FilePath) &&
               StringComparer.Ordinal.Equals(Name, other.Name) &&
               StringComparer.Ordinal.Equals(TargetFramework, other.TargetFramework);
    }

    public override int GetHashCode()
    {
        return HashCode.Combine(
            StringComparer.OrdinalIgnoreCase.GetHashCode(FilePath),
            StringComparer.Ordinal.GetHashCode(Name),
            StringComparer.Ordinal.GetHashCode(TargetFramework));
    }
}
