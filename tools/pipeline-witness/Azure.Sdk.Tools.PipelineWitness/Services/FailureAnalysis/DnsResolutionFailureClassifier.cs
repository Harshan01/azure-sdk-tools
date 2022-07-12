﻿namespace Azure.Sdk.Tools.PipelineWitness.Services.FailureAnalysis
{
    using Microsoft.TeamFoundation.Build.WebApi;
    using System;
    using System.Linq;
    using System.Threading.Tasks;

    public class DnsResolutionFailureClassifier : IFailureClassifier
    {
        private readonly BuildLogProvider buildLogProvider;

        public DnsResolutionFailureClassifier(BuildLogProvider buildLogProvider)
        {
            this.buildLogProvider = buildLogProvider;
        }

        private static bool IsDnsResolutionFailure(string line)
        {
            return line.Contains("EAI_AGAIN", StringComparison.OrdinalIgnoreCase)
                || line.Contains("getaddrinfo", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Temporary failure in name resolution", StringComparison.OrdinalIgnoreCase)
                || line.Contains("No such host is known", StringComparison.OrdinalIgnoreCase)
                || line.Contains("Couldn't resolve host name", StringComparison.OrdinalIgnoreCase);
        }

        public async Task ClassifyAsync(FailureAnalyzerContext context)
        {
            var failedTasks = context.Timeline.Records
                .Where(r => r.Result == TaskResult.Failed &&
                            r.RecordType == "Task" &&
                            r.Log != null);

            foreach (var failedTask in failedTasks)
            {
                var lines = await buildLogProvider.GetLogLinesAsync(context.Build, failedTask.Log.Id);

                if (lines.Any(IsDnsResolutionFailure))
                {
                    context.AddFailure(failedTask, "DNS Resolution Failure");
                }
            }
        }
    }
}
