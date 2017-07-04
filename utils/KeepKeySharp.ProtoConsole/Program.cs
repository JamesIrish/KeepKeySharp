using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace KeepKeySharp.ProtoConsole
{
    class Program
    {
        static void Main(string[] args)
        {
            // Run this console to update Proto files!
            // Make sure you have restored NuGet packages FIRST

            var workingDir = AppDomain.CurrentDomain.BaseDirectory;
            var configDir = Path.GetDirectoryName(workingDir);
            var binDir = Path.GetDirectoryName(configDir);
            var projectDir = Path.GetDirectoryName(binDir);
            var utilsDir = Path.GetDirectoryName(projectDir);
            var solutionDir = Path.GetDirectoryName(utilsDir);
            var toolsDir = Path.Combine(solutionDir, "tools");
            var keepKeySharpDir = Path.Combine(solutionDir, "KeepKeySharp");
            var contractsDir = Path.Combine(keepKeySharpDir, "Contracts");
            if (!Directory.Exists(toolsDir)) throw new DirectoryNotFoundException();
            if (!Directory.Exists(contractsDir)) throw new DirectoryNotFoundException();
            var protoc = Path.Combine(toolsDir, @"protobuf\CodeGenerator.exe");
            if (!File.Exists(protoc)) throw new FileNotFoundException();

            // Define the proto files to download from official GitHub account of KeepKey
            var keepKeyProtoFiles = new[] { "exchange.proto", "messages.proto", "storage.proto", "types.proto", "google/protobuf/descriptor.proto" };
            const string keepKeyGithubRawDownloadRoot = "https://raw.githubusercontent.com/keepkey/device-protocol/master";

            // Download to temp
            var tempDir = Environment.GetEnvironmentVariable("TEMP", EnvironmentVariableTarget.User);
            if (string.IsNullOrWhiteSpace(tempDir)) throw new ApplicationException("Cannot read TEMP environment variable");
            var randomTempDir = Path.Combine(tempDir, Path.GetRandomFileName());
            Directory.CreateDirectory(randomTempDir);

            Console.WriteLine("Downloading latest proto files from GitHub...");
            Console.WriteLine();

            using (var webClient = new WebClient())
            {
                webClient.BaseAddress = keepKeyGithubRawDownloadRoot;

                foreach (var keepKeyProtoFile in keepKeyProtoFiles)
                {
                    var remote = keepKeyGithubRawDownloadRoot + "/" + keepKeyProtoFile;
                    var local = Path.Combine(randomTempDir, keepKeyProtoFile.Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar));
                    if (!Directory.Exists(Path.GetDirectoryName(local))) Directory.CreateDirectory(Path.GetDirectoryName(local));

                    webClient.DownloadFile(remote, local);

                    var content = File.ReadAllText(local);
                    if (content.IndexOf("\npackage ", StringComparison.OrdinalIgnoreCase) == -1)
                        File.WriteAllText(local, "package KeepKeySharp.Contracts;" + Environment.NewLine + Environment.NewLine + content);

                    Console.WriteLine("Downloaded {0}", keepKeyProtoFile);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Generating C# contracts...");
            Console.WriteLine();

            var processStartInfo = new ProcessStartInfo
            {
                WorkingDirectory = AppDomain.CurrentDomain.BaseDirectory,
                FileName = protoc,
                WindowStyle = ProcessWindowStyle.Normal,
                UseShellExecute = false
            };

            var outputFile = Path.Combine(contractsDir, "Messages.cs");
            processStartInfo.Arguments = $"--fix-nameclash --serializable --nullable -o \"{outputFile}\" \"{randomTempDir}\\*.proto\"";
            var codeGenProcess = Process.Start(processStartInfo);
            if (codeGenProcess == null) throw new ApplicationException("Process failed to run.  Run process manually.");
            codeGenProcess.WaitForExit(10000);
            Console.WriteLine("Generated {0}", outputFile);
            

            Console.WriteLine();
            Console.WriteLine("Process complete, please rebuild solution.");
            Console.WriteLine("Press <enter> to quit.");
            Console.ReadLine();
        }
    }
}