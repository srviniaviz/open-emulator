using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenEmulator
{
    internal static class Tests
    {
        private static int count;
        private static void Check(bool condition, string message) { if (!condition) throw new Exception(message); count++; }
        private static void Reject(Action action) { try { action(); } catch (ArgumentException) { count++; return; } throw new Exception("Entrada inválida foi aceita."); }
        [STAThread]
        private static int Main(string[] args)
        {
            try {
                if (Path.GetFileName(Application.ExecutablePath) == "emulator.exe") {
                    if (args.SequenceEqual(new[] { "-list-avds" })) Console.WriteLine("Test_Phone");
                    else File.WriteAllLines(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "launch.txt"), args);
                    return 0;
                }
                if (args.FirstOrDefault() == "--echo") { foreach (string arg in args.Skip(1)) Console.WriteLine("arg:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(arg))); return 0; }
                if (args.FirstOrDefault() == "--install") {
                    new AndroidSdk(AndroidSdk.Detect()).InstallTools(Console.WriteLine).GetAwaiter().GetResult(); return 0;
                }
                if (args.FirstOrDefault() == "--integration") { Integration().GetAwaiter().GetResult(); return 0; }
                if (args.FirstOrDefault() == "--ui") { Ui(); return 0; }
                foreach (string valid in new[] { "Pixel_5", "phone-36.1", "x" }) { AndroidSdk.ValidateName(valid); count++; }
                foreach (string invalid in new[] { "", "has space", "../other", "-option", "a\n", "a&b", new string('x', 65) }) Reject(() => AndroidSdk.ValidateName(invalid));
                Reject(() => AndroidSdk.Quote("x\0y"));
                var values = new[] { "", "with space", "C:\\path with space\\", "embedded\"quote", "slashes\\\\\"quote", "&echo %PATH%", "ação" };
                var info = new ProcessStartInfo(Application.ExecutablePath, "--echo " + String.Join(" ", values.Select(AndroidSdk.Quote))) { UseShellExecute = false, RedirectStandardOutput = true, CreateNoWindow = true };
                using (var p = Process.Start(info)) {
                    var echoed = p.StandardOutput.ReadToEnd().Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                    p.WaitForExit();
                    Check(p.ExitCode == 0, "Processo de eco falhou.");
                    var expected = values.Select(v => "arg:" + Convert.ToBase64String(Encoding.UTF8.GetBytes(v)));
                    Check(echoed.SequenceEqual(expected), "Argumentos mudaram ao atravessar CreateProcess.");
                }
                string root = Path.Combine(Path.GetTempPath(), "OpenEmulator-tests-" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(Path.Combine(root, "emulator"));
                File.Copy(Application.ExecutablePath, Path.Combine(root, "emulator", "emulator.exe"));
                File.Copy(Application.ExecutablePath + ".config", Path.Combine(root, "emulator", "emulator.exe.config"));
                try {
                    var sdk = new AndroidSdk(root);
                    sdk.Launch("Test_Phone", false).GetAwaiter().GetResult();
                    string launchFile = Path.Combine(root, "emulator", "launch.txt");
                    Check(File.ReadAllLines(launchFile).SequenceEqual(new[] { "-avd", "Test_Phone" }), "Abertura não encaminhou o AVD selecionado.");
                    sdk.Launch("Test_Phone", true).GetAwaiter().GetResult();
                    Check(File.ReadAllLines(launchFile).SequenceEqual(new[] { "-avd", "Test_Phone", "-no-snapshot-load" }), "Inicialização a frio não foi encaminhada.");
                    Check(sdk.Images().Length == 0, "SDK vazio retornou imagem.");
                    string image = Path.Combine(root, "system-images", "android-35", "google_apis", "x86_64");
                    Directory.CreateDirectory(image); File.WriteAllText(Path.Combine(image, "source.properties"), "");
                    Check(sdk.Images().Length == 0, "Imagem incompleta foi aceita.");
                    File.WriteAllText(Path.Combine(image, "system.img"), "fixture");
                    Check(sdk.Images().Single() == "system-images;android-35;google_apis;x86_64", "ID de pacote incorreto.");
                } finally { Directory.Delete(root, true); }
                Console.WriteLine(count + " verificações passaram."); return 0;
            } catch (Exception error) { Console.Error.WriteLine(error); return 1; }
        }
        private static async Task Integration()
        {
            var sdk = new AndroidSdk(AndroidSdk.Detect());
            Console.WriteLine("AVDs existentes: " + String.Join(", ", await sdk.ListAvds()));
            string isolated = Path.Combine(Path.GetTempPath(), "OpenEmulator-integration-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(isolated);
            string oldAvd = Environment.GetEnvironmentVariable("ANDROID_AVD_HOME");
            try {
                Environment.SetEnvironmentVariable("ANDROID_AVD_HOME", isolated);
                Check((await sdk.ListAvds()).Length == 0, "Pasta temporária não isolou AVDs.");
                var profiles = await sdk.Profiles();
                Check(profiles.Contains("pixel_5"), "Pixel 5 não encontrado nos perfis.");
                Console.WriteLine(profiles.Length + " perfis oficiais encontrados.");
                string image = sdk.Images().Last();
                await sdk.Create("OpenEmulator_Test", image, "pixel_5");
                Check((await sdk.ListAvds()).SequenceEqual(new[] { "OpenEmulator_Test" }), "Criação não resultou em AVD listado.");
                try { await sdk.Create("OpenEmulator_Test", image, "pixel_5"); throw new Exception("Duplicação aceita."); }
                catch (IOException error) { Check(error.Message.Contains("Já existe"), "Erro de duplicação inesperado."); }
                Console.WriteLine("Criação real e rejeição de duplicata passaram; AVD temporário removido ao sair.");
            } finally {
                Environment.SetEnvironmentVariable("ANDROID_AVD_HOME", oldAvd);
                Directory.Delete(isolated, true);
            }
        }
        private static void Ui()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            using (var launcher = new Launcher())
            using (var timer = new Timer { Interval = 4000 }) {
                timer.Tick += (s, e) => {
                    timer.Stop();
                    var list = launcher.Controls.OfType<TableLayoutPanel>().Single().Controls.OfType<ListBox>().Single();
                    Check(list.Items.Count > 0, "O painel não carregou os emuladores.");
                    var area = Screen.PrimaryScreen.WorkingArea;
                    Check(launcher.Right == area.Right - 12 && launcher.Bottom == area.Bottom - 12, "Painel fora do canto inferior direito.");
                    using (var bitmap = new Bitmap(launcher.Width, launcher.Height)) { launcher.DrawToBitmap(bitmap, new Rectangle(Point.Empty, launcher.Size)); bitmap.Save(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "preview.png")); }
                    Console.WriteLine("Painel carregou " + list.Items.Count + " AVDs e está no canto inferior direito.");
                    launcher.Close();
                    Check(!launcher.Visible && !launcher.IsDisposed, "Fechar o painel não o recolheu para a bandeja.");
                    Application.Exit();
                };
                timer.Start(); Application.Run(launcher);
            }
        }
    }
}
