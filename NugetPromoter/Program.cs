using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CommandLine;
using NuGet;

namespace NugetPromoter
{
    internal class Program
    {
        private static void Main(string[] args)
        {
            var options = new Options();
            var result = Parser.Default.ParseArguments(args, options);

            if (!result)
                throw new NotSupportedException("Invalid arguments");

            if (!File.Exists(options.PackageToPromote))
                throw new FileNotFoundException("Cannot find the file", options.PackageToPromote);

            var package = new ZipPackage(options.PackageToPromote);
            var temppath = Path.GetTempPath() + Guid.NewGuid();
            package.ExtractContents(new PhysicalFileSystem(temppath), "");

            Console.WriteLine("Package Id: " + package.Id);
            Console.WriteLine($"Extracted to {temppath}");

            var match = Regex.Match(package.Version.ToString(), @"\d+\.\d+\.\d+");
            if (!match.Success)
                throw new NotSupportedException("No semantic version was found in the metadata of the package.");

            var newVersion = match.Value;
            Console.WriteLine($"The new version of the package will be {newVersion}");

            // Try to find rcedit.exe
            var exe = findExecutable("rcedit.exe");
            foreach (var dimaExe in Directory.GetFiles(temppath, "*.exe"))   
            {
                Console.WriteLine($"Adjusting the product version of {dimaExe} to {newVersion}");
                var rceditargs = $"{dimaExe} --set-product-version {newVersion}";
                var process = Process.Start(exe, rceditargs);
                process.WaitForExit(10000);
                if (process.ExitCode != 0)
                {
                    var msg = $"Failed to modify resources, command invoked was: '{exe} {string.Join(" ", rceditargs)}'";
                    throw new Exception(msg);
                }   
            }

            var metadata = new ManifestMetadata
            {
                Id = package.Id,
                Version = newVersion,
                Title = package.Title,
                ReleaseNotes = package.ReleaseNotes,
                Summary = package.Summary,
                Description = package.Description,
                Copyright = package.Copyright,
                Language = package.Language
            };
            if (package.IconUrl != null) metadata.IconUrl = package.IconUrl.ToString();
            if (package.LicenseUrl != null) metadata.LicenseUrl = package.LicenseUrl.ToString();
            if (package.ProjectUrl != null) metadata.ProjectUrl = package.ProjectUrl.ToString();
            if (package.Owners != null) metadata.Owners = string.Join(", ", package.Owners);
            if (package.Authors != null) metadata.Authors = string.Join(", ", package.Authors);

            var builder = new PackageBuilder();
            var files = Directory.GetFiles(temppath, "*", SearchOption.AllDirectories)
                .Where(f => !f.EndsWith(".nuspec"))
                .Select(f => new ManifestFile {Source = f, Target = f.Replace(temppath, "")})
                .ToList();
            builder.PopulateFiles("", files);
            builder.Populate(metadata);

            if (string.IsNullOrEmpty(options.OutputPath))
                options.OutputPath = Directory.GetCurrentDirectory();

            var outputFile = Path.Combine(options.OutputPath, $"{package.Id}.{newVersion}.nupkg");
            Console.WriteLine($"Saving new file to {outputFile}");
            using (var stream = File.Open(outputFile, FileMode.Create))
            {
                builder.Save(stream);
            }

            Console.WriteLine("Succesfully promoted package");
        }

        private static string findExecutable(string toFind)
        {
            var exe = @".\" + toFind;
            if (!File.Exists(exe))
            {
                exe = Path.Combine(
                    Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location),
                    toFind);

                // Run down PATH and hope for the best
                if (!File.Exists(exe)) exe = toFind;
            }

            return exe;
        }
    }
}