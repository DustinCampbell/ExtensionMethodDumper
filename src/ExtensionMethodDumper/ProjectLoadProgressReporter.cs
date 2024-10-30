using System.Collections.Immutable;
using Microsoft.CodeAnalysis.MSBuild;
using Microsoft.Extensions.Logging;

internal sealed class ProjectLoadProgressReporter(Dictionary<string, ImmutableArray<string>> projectPathToTfmMap, ILogger logger) : IProgress<ProjectLoadProgress>
{
    public void Report(ProjectLoadProgress value)
    {
        if (value.Operation == ProjectLoadOperation.Resolve)
        {
            lock (projectPathToTfmMap)
            {
                if (!projectPathToTfmMap.TryGetValue(value.FilePath, out var targetFrameworks))
                {
                    targetFrameworks = value.TargetFramework is string targetFramework
                        ? [targetFramework]
                        : [];

                    projectPathToTfmMap.Add(value.FilePath, targetFrameworks);
                }
                else if (value.TargetFramework is string targetFramework)
                {
                    projectPathToTfmMap[value.FilePath] = targetFrameworks.Add(targetFramework);
                }
            }
        }

        if (value.TargetFramework is not null)
        {
            logger.LogInformation("[{Operation}] ({ElapsedTime:s\\.fffffff}): {TargetFramework} - {FilePath}", value.Operation, value.ElapsedTime, value.TargetFramework, value.FilePath);
        }
        else
        {
            logger.LogInformation("[{Operation}] ({ElapsedTime:s\\.fffffff}): {FilePath}", value.Operation, value.ElapsedTime, value.FilePath);
        }
    }
}