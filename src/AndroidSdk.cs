using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace OpenEmulator
{
    internal sealed class AndroidSdk
    {
        public readonly string Root;
        public AndroidSdk(string root)
        {
            Root = Path.GetFullPath(root);
            if (!File.Exists(Emulator)) throw new IOException("Selecione a pasta do SDK que contém emulator\\emulator.exe.");
        }
        public string Emulator { get { return Path.Combine(Root, "emulator", "emulator.exe"); } }
        public static string Detect()
        {
            return new[] { Environment.GetEnvironmentVariable("ANDROID_HOME"), Environment.GetEnvironmentVariable("ANDROID_SDK_ROOT"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Android", "Sdk") }
                .FirstOrDefault(p => !String.IsNullOrWhiteSpace(p) && File.Exists(Path.Combine(p, "emulator", "emulator.exe")));
        }

        // Windows CreateProcess quoting; no shell interprets user input.
        public static string Quote(string value)
        {
            if (value.IndexOf('\0') >= 0 || value.IndexOf('\r') >= 0 || value.IndexOf('\n') >= 0)
                throw new ArgumentException("Argumento inválido.");
            return "\"" + Regex.Replace(Regex.Replace(value, "(\\\\*)\"", "$1$1\\\""), "(\\\\+)$", "$1$1") + "\"";
        }
        private ProcessStartInfo StartInfo(string exe, string[] args)
        {
            var info = new ProcessStartInfo(exe, String.Join(" ", args.Select(Quote))) {
                UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = Root
            };
            info.EnvironmentVariables["ANDROID_HOME"] = Root;
            info.EnvironmentVariables["ANDROID_SDK_ROOT"] = Root;
            return info;
        }
        private async Task<string> Run(string exe, params string[] args)
        {
            var info = StartInfo(exe, args);
            info.RedirectStandardOutput = info.RedirectStandardError = info.RedirectStandardInput = true;
            using (var process = Process.Start(info))
            {
                var output = process.StandardOutput.ReadToEndAsync();
                var error = process.StandardError.ReadToEndAsync();
                // No custom hardware prompts; selected device profiles provide the configuration.
                if (args.Contains("create")) process.StandardInput.WriteLine("no");
                process.StandardInput.Close();
                var exited = await Task.Run(() => process.WaitForExit(120000));
                if (!exited) { process.Kill(); throw new TimeoutException("A ferramenta Android não respondeu em 2 minutos."); }
                string stdout = await output, stderr = await error;
                if (process.ExitCode != 0) throw new IOException("A ferramenta Android falhou (" + process.ExitCode + ").\r\n" + stderr + "\r\n" + stdout);
                return stdout;
            }
        }
        public async Task<string[]> ListAvds()
        {
            return Lines(await Run(Emulator, "-list-avds"));
        }
        public async Task Launch(string name, bool coldBoot)
        {
            if (!(await ListAvds()).Contains(name)) throw new IOException("Este emulador não existe mais. Atualize a lista.");
            var args = new List<string> { "-avd", name };
            if (coldBoot) args.Add("-no-snapshot-load");
            var info = StartInfo(Emulator, args.ToArray());
            using (var process = Process.Start(info))
            {
                bool exited = await Task.Run(() => process.WaitForExit(2000));
                if (exited && process.ExitCode != 0) throw new IOException("O emulador encerrou ao iniciar (código " + process.ExitCode + "). Verifique virtualização e se o dispositivo já está aberto.");
            }
        }
        public string[] Images()
        {
            string folder = Path.Combine(Root, "system-images");
            if (!Directory.Exists(folder)) return new string[0];
            return Directory.GetFiles(folder, "source.properties", SearchOption.AllDirectories)
                .Where(p => File.Exists(Path.Combine(Path.GetDirectoryName(p), "system.img")))
                .Select(p => "system-images;" + Path.GetDirectoryName(p).Substring(folder.Length + 1).Replace('\\', ';'))
                .OrderByDescending(p => p, StringComparer.OrdinalIgnoreCase).ToArray();
        }
        public string ToolsDirectory()
        {
            var parent = Path.Combine(Root, "cmdline-tools");
            if (!Directory.Exists(parent)) return null;
            return Directory.GetDirectories(parent).OrderByDescending(p => Path.GetFileName(p) == "latest")
                .FirstOrDefault(p => File.Exists(Path.Combine(p, "lib", "avdmanager-classpath.jar")));
        }
        private string Java()
        {
            string home = Environment.GetEnvironmentVariable("JAVA_HOME");
            var paths = new[] { home == null ? null : Path.Combine(home, "bin", "java.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Android", "Android Studio", "jbr", "bin", "java.exe") };
            string java = paths.FirstOrDefault(p => p != null && File.Exists(p));
            if (java == null) throw new IOException("Java não encontrado. Configure JAVA_HOME para um JDK 21 ou use o Java incluído no Android Studio.");
            return java;
        }
        private Task<string> AvdManager(params string[] args)
        {
            string tools = ToolsDirectory();
            if (tools == null) throw new IOException("Instale as ferramentas de criação primeiro.");
            return Run(Java(), new[] { "-Dcom.android.sdkmanager.toolsdir=" + tools, "-cp", Path.Combine(tools, "lib", "avdmanager-classpath.jar"),
                "com.android.sdklib.tool.AvdManagerCli" }.Concat(args).ToArray());
        }
        public async Task<string[]> Profiles()
        {
            return Lines(await AvdManager("list", "device", "-c"))
                .Where(p => Regex.IsMatch(p, @"^[a-zA-Z0-9_.-]+$")).ToArray();
        }
        public static void ValidateName(string name)
        {
            if (name == null || !Regex.IsMatch(name, @"\A[a-zA-Z0-9][a-zA-Z0-9_.-]{0,63}\z"))
                throw new ArgumentException("Use de 1 a 64 letras sem acentos, números, ponto, hífen ou sublinhado; comece com letra ou número.");
        }
        public async Task Create(string name, string image, string profile)
        {
            ValidateName(name);
            if ((await ListAvds()).Contains(name, StringComparer.OrdinalIgnoreCase)) throw new IOException("Já existe um emulador com este nome.");
            if (!Images().Contains(image)) throw new IOException("Selecione uma imagem Android instalada.");
            if (!(await Profiles()).Contains(profile)) throw new IOException("Selecione um perfil de dispositivo válido.");
            await AvdManager("create", "avd", "-n", name, "-k", image, "-d", profile);
            if (!(await ListAvds()).Contains(name)) throw new IOException("A ferramenta terminou, mas o dispositivo não apareceu na lista.");
        }
        private static string[] Lines(string value)
        {
            return value.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(p => p.Trim()).Where(p => p.Length > 0).ToArray();
        }
        public async Task InstallTools(Action<string> progress)
        {
            // Official stable repository: select Windows archive and verify its published checksum.
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
            using (var client = new WebClient())
            {
                progress("Consultando ferramentas oficiais…");
                var document = XDocument.Parse(await client.DownloadStringTaskAsync("https://dl.google.com/android/repository/repository2-1.xml"));
                var package = document.Descendants().Single(e => e.Name.LocalName == "remotePackage" && (string)e.Attribute("path") == "cmdline-tools;latest");
                var archive = package.Descendants().Single(e => e.Name.LocalName == "archive" && e.Elements().Any(c => c.Name.LocalName == "host-os" && c.Value == "windows"));
                string url = archive.Descendants().Single(e => e.Name.LocalName == "url").Value;
                string checksum = archive.Descendants().Single(e => e.Name.LocalName == "checksum").Value.Trim();
                if (!Regex.IsMatch(url, @"^commandlinetools-win-[0-9]+_latest\.zip$")) throw new IOException("URL inesperada no repositório Android.");
                string parent = Path.Combine(Root, "cmdline-tools");
                Directory.CreateDirectory(parent);
                string stagingPath = Path.Combine(parent, ".oe-" + Guid.NewGuid().ToString("N"));
                // Extended Windows paths also work when the system-wide long-path policy is off.
                string staging = stagingPath.StartsWith(@"\\") ? @"\\?\UNC\" + stagingPath.Substring(2) : @"\\?\" + stagingPath;
                Directory.CreateDirectory(staging);
                try
                {
                    string zip = Path.Combine(staging, "tools.zip");
                    int lastProgress = -1;
                    client.DownloadProgressChanged += (s, e) => {
                        if (e.ProgressPercentage != lastProgress) { lastProgress = e.ProgressPercentage; progress("Baixando ferramentas: " + e.ProgressPercentage + "%"); }
                    };
                    await client.DownloadFileTaskAsync("https://dl.google.com/android/repository/" + url, zip);
                    using (var stream = File.OpenRead(zip))
                    using (var sha = SHA1.Create())
                    {
                        string actual = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "");
                        if (!String.Equals(actual, checksum, StringComparison.OrdinalIgnoreCase)) throw new IOException("O download falhou na verificação de integridade.");
                    }
                    progress("Extraindo ferramentas…");
                    await Task.Run(() => {
                        // ZipFile.ExtractToDirectory keeps '/' from archive entries, which Windows
                        // rejects on extended paths. Normalize entries and contain them in staging.
                        using (var archiveFile = ZipFile.OpenRead(zip))
                        foreach (var entry in archiveFile.Entries)
                        {
                            string destination = Path.GetFullPath(Path.Combine(staging, entry.FullName.Replace('/', '\\')));
                            if (!destination.StartsWith(staging + "\\", StringComparison.OrdinalIgnoreCase))
                                throw new IOException("Caminho inválido no arquivo das ferramentas.");
                            if (entry.Name.Length == 0) Directory.CreateDirectory(destination);
                            else {
                                Directory.CreateDirectory(Path.GetDirectoryName(destination));
                                using (var source = entry.Open())
                                using (var output = File.Create(destination)) source.CopyTo(output);
                            }
                        }
                    });
                    string target = Path.Combine(Path.GetDirectoryName(staging), "open-emulator");
                    if (Directory.Exists(target)) throw new IOException("A pasta cmdline-tools\\open-emulator já existe. Verifique sua instalação do SDK.");
                    Directory.Move(Path.Combine(staging, "cmdline-tools"), target);
                }
                finally { Directory.Delete(staging, true); }
            }
        }
    }
}
