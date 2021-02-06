using Azure.Sdk.Tools.PerfAutomation.Models;
using CommandLine;
using CommandLine.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Azure.Sdk.Tools.PerfAutomation
{
    public static class Program
    {
        public static OptionsDefinition Options { get; set; }
        public static Config Config { get; set; }

        private static Dictionary<Language, ILanguage> _languages = new Dictionary<Language, ILanguage>
        {
            { Language.Net, new Net() },
            { Language.Java, new Java() },
            { Language.Python, new Python() }
        };

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            Converters =
            {
                new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
            }
        };

        public class OptionsDefinition
        {
            [Option('c', "configFile", Default = "config.yml")]
            public string ConfigFile { get; set; }

            [Option('d', "debug")]
            public bool Debug { get; set; }

            [Option('n', "dry-run")]
            public bool DryRun { get; set; }

            [Option('i', "iterations", Default = 3)]
            public int Iterations { get; set; }

            [Option('l', "languages")]
            public IEnumerable<Language> Languages { get; set; }

            [Option("input-file", Default = "tests.yml")]
            public string InputFile { get; set; }

            [Option("no-async")]
            public bool NoAsync { get; set; }

            [Option("no-sync")]
            public bool NoSync { get; set; }

            [Option('o', "output-file", Default = "results.json")]
            public string OutputFile { get; set; }

            [Option('t', "testFilter", HelpText = "Regex of tests to run")]
            public string TestFilter { get; set; }

            [Option('v', "versionFilter", HelpText = "Regex of versions to run")]
            public string VersionFilter { get; set; }
        }

        public static async Task Main(string[] args)
        {
            var parser = new CommandLine.Parser(settings =>
            {
                settings.CaseSensitive = false;
                settings.CaseInsensitiveEnumValues = true;
                settings.HelpWriter = null;
            });

            var parserResult = parser.ParseArguments<OptionsDefinition>(args);

            await parserResult.MapResult(
                options => Run(options),
                errors => DisplayHelp(parserResult)
            );
        }

        static Task DisplayHelp<T>(ParserResult<T> result)
        {
            var helpText = HelpText.AutoBuild(result, settings =>
            {
                settings.AddEnumValuesToHelpText = true;
                return settings;
            });

            Console.Error.WriteLine(helpText);

            return Task.CompletedTask;
        }

        private static async Task Run(OptionsDefinition options)
        {
            Options = options;

            Config = DeserializeYaml<Config>(options.ConfigFile);

            var services = DeserializeYaml<List<ServiceInfo>>(options.InputFile);

            var selectedServices = services.Select(s => new ServiceInfo
            {
                Service = s.Service,
                Languages = s.Languages.Where(l => !options.Languages.Any() || options.Languages.Contains(l.Key))
                    .ToDictionary(p => p.Key, p => new LanguageInfo()
                    {
                        Project = p.Value.Project,
                        AdditionalArguments = p.Value.AdditionalArguments,
                        PackageVersions = p.Value.PackageVersions.Where(d => d.Keys.Concat(d.Values).Any(s =>
                            String.IsNullOrEmpty(options.VersionFilter) || Regex.IsMatch(s, options.VersionFilter)
                        ))
                    }),
                Tests = s.Tests.Where(t =>
                    String.IsNullOrEmpty(options.TestFilter) || Regex.IsMatch(t.Test, options.TestFilter, RegexOptions.IgnoreCase)).Select(t =>
                        new TestInfo
                        {
                            Test = t.Test,
                            Arguments = t.Arguments,
                            TestNames = t.TestNames.Where(n => !options.Languages.Any() || options.Languages.Contains(n.Key))
                                .ToDictionary(p => p.Key, p => p.Value)
                        }
                    )
            }); ;

            var serializer = new Serializer();
            Console.WriteLine("=== Options ===");
            serializer.Serialize(Console.Out, options);

            Console.WriteLine();

            Console.WriteLine("=== Test Plan ===");
            serializer.Serialize(Console.Out, selectedServices);

            if (options.DryRun)
            {
                return;
            }

            var uniqueOutputFile = Util.GetUniquePath(options.OutputFile);
            // Create output file early so user sees it immediately
            using (File.Create(uniqueOutputFile)) { }

            var results = new List<Result>();

            foreach (var service in selectedServices)
            {
                foreach (var l in service.Languages)
                {
                    var language = l.Key;
                    var languageInfo = l.Value;

                    foreach (var packageVersions in languageInfo.PackageVersions)
                    {
                        try
                        {
                            var (setupOutput, setupError, context) = await _languages[language].SetupAsync(languageInfo.Project, packageVersions);

                            foreach (var test in service.Tests)
                            {
                                IEnumerable<string> selectedArguments;
                                if (!options.NoAsync && !options.NoSync)
                                {
                                    selectedArguments = test.Arguments.SelectMany(a => new string[] { a, a + " --sync" });
                                }
                                else if (!options.NoSync)
                                {
                                    selectedArguments = test.Arguments.Select(a => a + " --sync");
                                }
                                else if (!options.NoAsync)
                                {
                                    selectedArguments = test.Arguments;
                                }
                                else
                                {
                                    throw new InvalidOperationException("Cannot set both --no-sync and --no-async");
                                }

                                foreach (var arguments in selectedArguments)
                                {
                                    var allArguments = $"{arguments} {languageInfo.AdditionalArguments}";

                                    var result = new Result
                                    {
                                        TestName = test.Test,
                                        Language = language,
                                        Project = languageInfo.Project,
                                        LanguageTestName = test.TestNames[language],
                                        Arguments = allArguments,
                                        PackageVersions = packageVersions,
                                        SetupStandardOutput = setupOutput,
                                        SetupStandardError = setupError,
                                    };

                                    results.Add(result);

                                    using (var stream = File.OpenWrite(uniqueOutputFile))
                                    {
                                        await JsonSerializer.SerializeAsync(stream, results, JsonOptions);
                                    }

                                    for (var i = 0; i < options.Iterations; i++)
                                    {
                                        var iterationResult = await _languages[language].RunAsync(
                                            languageInfo.Project,
                                            test.TestNames[language],
                                            allArguments,
                                            context
                                        );

                                        result.Iterations.Add(iterationResult);

                                        using (var stream = File.OpenWrite(uniqueOutputFile))
                                        {
                                            await JsonSerializer.SerializeAsync(stream, results, JsonOptions);
                                        }
                                    }
                                }
                            }
                        }
                        finally
                        {
                            await _languages[language].CleanupAsync(languageInfo.Project);
                        }
                    }
                }
            }
        }

        private static T DeserializeYaml<T>(string path)
        {
            using var fileReader = File.OpenText(path);
            var parser = new MergingParser(new YamlDotNet.Core.Parser(fileReader));
            return new Deserializer().Deserialize<T>(parser);
        }
    }
}