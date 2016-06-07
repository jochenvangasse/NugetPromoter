using CommandLine;

namespace NugetPromoter
{
    class Options
    {
        [Option('p', "package", Required = true, HelpText = "Path to the package to promote")]
        public string PackageToPromote { get; set; }

        [Option('o', "outputpath", Required = false, HelpText = "Output path")]
        public string OutputPath { get; set; }

        [Option('t', "tag", Required = false, HelpText = "An optional new prerelease tag to be applied on the new package")]
        public string PreleaseTag { get; set; }
    }
}