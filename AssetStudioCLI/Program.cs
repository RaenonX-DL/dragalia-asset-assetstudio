using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AssetStudio;
using CommandLine;

namespace AssetStudioCLI
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            Console.WriteLine("AssetStudio CLI activated.");
            var exitCode = Parser.Default.ParseArguments<ExtractOptions, ConvertOptions, LoadOptions>(args)
                .MapResult(
                    (ExtractOptions o) => ExtractFolder(o),
                    (ConvertOptions o) => ConvertPath(o),
                    (LoadOptions o) => LoadAsset(o),
                    error => 1
                );
            return exitCode;
        }

        private static int ExtractFolder(ExtractOptions opt)
        {
            if (!Directory.Exists(opt.SourceFolder))
            {
                Console.WriteLine("Source folder not exists");
                return 1;
            }

            Directory.CreateDirectory(opt.TargetFolder);

            using (new CLIProgress())
            {
                Studio.ExtractFolder(opt.SourceFolder, opt.TargetFolder);
            }

            return 0;
        }

        private static int ConvertPath(ConvertOptions opt)
        {
            var isDir = Directory.Exists(opt.SourcePath);
            if (!isDir)
            {
                if (!File.Exists(opt.SourcePath))
                {
                    Console.WriteLine("Source path not exists");
                    return 1;
                }
            }

            Directory.CreateDirectory(opt.TargetFolder);
            Studio.assemblyReferenceFolder = opt.AssemblyPath;
            Studio.exportSuffix = opt.TargetSuffix;
            Studio.skipExists = opt.SkipExists;

            using (new CLIProgress())
            {
                if (isDir)
                {
                    Studio.assetsManager.LoadFolder(opt.SourcePath);
                }
                else
                {
                    if (Path.GetExtension(opt.SourcePath) == ".files")
                    {
                        var files = File.ReadAllLines(opt.SourcePath);
                        Studio.assetsManager.LoadFiles(files);
                    }
                    else
                    {
                        Studio.assetsManager.LoadFiles(opt.SourcePath);
                    }
                }

                Studio.BuildAssetData();

                if (!string.IsNullOrEmpty(opt.Makefile))
                {
                    var exportArgs = Proto.ExportArguments.Parser.ParseJson(File.ReadAllText(opt.Makefile));
                    foreach (var arg in exportArgs.Exports)
                    {
                        FilterWithArg(arg);
                        Enum.TryParse(arg.ExportType, out ExportType exportType);
                        Enum.TryParse(arg.ProcessType, out ProcessType processType);
                        Studio.ExportAssets(opt.TargetFolder, Studio.visibleAssets, exportType, processType);
                    }
                }
                else
                {
                    FilterWithOptions(opt);
                    Studio.ExportAssets(opt.TargetFolder, Studio.visibleAssets, ExportType.Convert, ProcessType.Async);
                }
            }

            return 0;
        }

        private static int LoadAsset(LoadOptions opt)
        {
            Studio.LoadAsset(opt.AssetPath);

            return 0;
        }

        private static void FilterWithArg(Proto.ExportContainerWithType arg)
        {
            var list = Studio.exportableAssets;

            var filterTypes = arg.Types_.Select(s => Enum.Parse(typeof(ClassIDType), s)).ToList();
            if (filterTypes.Count > 0)
            {
                list = list.FindAll(x => filterTypes.Contains(x.Type));
            }

            var filterContainerPaths = arg.Containers.Select(s => new Regex(s, RegexOptions.IgnoreCase)).ToList();
            if (filterContainerPaths.Count > 0)
            {
                list = list.FindAll(x => filterContainerPaths.Any(r => r.IsMatch(x.Container)));
            }

            Studio.visibleAssets = list;
        }

        private static void FilterWithOptions(ConvertOptions opt)
        {
            var list = Studio.exportableAssets;

            var filterTypes = opt.Types?.Select(s => Enum.Parse(typeof(ClassIDType), s)).ToList();
            if (filterTypes != null &&
                filterTypes.Count > 0)
            {
                list = list.FindAll(x => filterTypes.Contains(x.Type));
            }

            var filterContainerPaths = opt.ContainerPaths?.Select(s => new Regex(s, RegexOptions.IgnoreCase)).ToList();
            if (filterContainerPaths != null &&
                filterContainerPaths.Count > 0)
            {
                list = list.FindAll(x => filterContainerPaths.Any(r => r.IsMatch(x.Container)));
            }

            Studio.visibleAssets = list;
        }

        [Verb("extract", HelpText = "Extract assets")]
        public class ExtractOptions
        {
            [Value(0, HelpText = "Source Folder")] public string SourceFolder { get; set; }

            [Value(1, HelpText = "Target Folder")] public string TargetFolder { get; set; }
        }

        [Verb("convert", HelpText = "Convert assets")]
        public class ConvertOptions
        {
            [Value(0, HelpText = "Source Path")] public string SourcePath { get; set; }

            [Value(1, HelpText = "Target Folder")] public string TargetFolder { get; set; }

            [Option('m', "make", Required = false, HelpText = "Convert with makefile (json)", Default = null)]
            public string Makefile { get; set; }

            [Option('t', "type", Required = false, HelpText = "Types filter")]
            public IEnumerable<string> Types { get; set; }

            [Option('c', "Container", Required = false, HelpText = "Container path filter (Regex)")]
            public IEnumerable<string> ContainerPaths { get; set; }

            [Option('a', "Assembly", Required = false, HelpText = "Assembly reference path", Default = "")]
            public string AssemblyPath { get; set; }

            [Option("with-suffix", Required = false, HelpText = "append suffix to target name", Default = "")]
            public string TargetSuffix { get; set; }

            [Option("skip-exists", HelpText = "do not overwrite exists target")]
            public bool SkipExists { get; set; }
        }

        [Verb("load", HelpText = "Load assets")]
        public class LoadOptions
        {
            [Value(0, HelpText = "Asset Path")] public string AssetPath { get; set; }
        }
    }
}