using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace OpenEmulator
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            bool created;
            using (var mutex = new Mutex(true, "Local\\OpenEmulator", out created))
            {
                if (!created) { MessageBox.Show("O Open Emulator já está na bandeja, perto do relógio.", "Open Emulator"); return; }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new Launcher());
            }
        }
    }

    internal sealed class Launcher : Form
    {
        private readonly ListBox devices = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        private readonly Label status = new Label { Dock = DockStyle.Fill, AutoEllipsis = true, Text = "Localizando SDK…", TextAlign = ContentAlignment.MiddleLeft };
        private readonly CheckBox cold = new CheckBox { Text = "Inicialização a frio", AutoSize = true };
        private readonly CheckBox pin = new CheckBox { Text = "Sempre visível", Checked = true, AutoSize = true };
        private readonly Button open = new Button { Text = "Abrir", AutoSize = true };
        private readonly Button refresh = new Button { Text = "Atualizar", AutoSize = true };
        private readonly Button create = new Button { Text = "Criar…", AutoSize = true };
        private readonly Button config = new Button { Text = "SDK…", AutoSize = true };
        private readonly NotifyIcon tray;
        private AndroidSdk sdk;
        private bool exiting, busy;
        private readonly string settings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenEmulator", "sdk.txt");

        public Launcher()
        {
            Text = "Open Emulator";
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(364, 326);
            FormBorderStyle = FormBorderStyle.FixedSingle;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.Manual;
            TopMost = true;
            using (var stream = typeof(Launcher).Assembly.GetManifestResourceStream("OpenEmulator.Icon.ico"))
                Icon = new Icon(stream, SystemInformation.SmallIconSize);
            var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(12), ColumnCount = 1, RowCount = 6 };
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 27));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 35));
            layout.Controls.Add(new Label { Text = "Emuladores Android", Font = new Font(Font, FontStyle.Bold), Dock = DockStyle.Fill }, 0, 0);
            layout.Controls.Add(devices, 0, 1);
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false, Padding = new Padding(0, 5, 0, 0) };
            actions.Controls.AddRange(new Control[] { open, refresh, create, config });
            layout.Controls.Add(actions, 0, 2);
            layout.Controls.Add(cold, 0, 3);
            layout.Controls.Add(pin, 0, 4);
            layout.Controls.Add(status, 0, 5);
            Controls.Add(layout);
            var menu = new ContextMenuStrip();
            menu.Items.Add("Abrir painel", null, (s, e) => Reveal());
            menu.Items.Add("Sair", null, (s, e) => { exiting = true; Close(); });
            tray = new NotifyIcon { Text = "Open Emulator", Icon = Icon, Visible = true, ContextMenuStrip = menu };
            tray.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) { if (Visible) Hide(); else Reveal(); } };
            open.Click += async (s, e) => await Guard(async () => {
                string name = devices.SelectedItem as string;
                if (name == null) throw new InvalidOperationException("Selecione um emulador.");
                status.Text = "Iniciando " + name + "…";
                await sdk.Launch(name, cold.Checked);
                status.Text = "Inicialização solicitada: " + name;
            });
            devices.DoubleClick += (s, e) => { if (open.Enabled) open.PerformClick(); };
            refresh.Click += async (s, e) => await Guard(RefreshDevices);
            create.Click += async (s, e) => await Guard(async () => {
                using (var dialog = new CreateDialog(sdk)) dialog.ShowDialog(this);
                await RefreshDevices();
            });
            config.Click += async (s, e) => {
                using (var dialog = new FolderBrowserDialog { Description = "Selecione a pasta do Android SDK", SelectedPath = sdk == null ? "" : sdk.Root })
                    if (dialog.ShowDialog(this) == DialogResult.OK) await Guard(async () => {
                        var selected = new AndroidSdk(dialog.SelectedPath);
                        Directory.CreateDirectory(Path.GetDirectoryName(settings));
                        File.WriteAllText(settings, selected.Root);
                        sdk = selected;
                        await RefreshDevices();
                    });
            };
            pin.CheckedChanged += (s, e) => TopMost = pin.Checked;
            Shown += async (s, e) => await Guard(async () => {
                string path = File.Exists(settings) ? File.ReadAllText(settings).Trim() : AndroidSdk.Detect();
                if (path == null) throw new IOException("SDK não encontrado. Clique em SDK para selecionar a pasta.");
                sdk = new AndroidSdk(path);
                await RefreshDevices();
            });
            LocationChanged += (s, e) => { if (Visible) DockCorner(); };
            VisibleChanged += (s, e) => { if (Visible) DockCorner(); };
            FormClosing += (s, e) => { if (!exiting && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); } };
            FormClosed += (s, e) => { tray.Dispose(); menu.Dispose(); };
            DockCorner();
        }
        private void DockCorner()
        {
            var area = Screen.PrimaryScreen.WorkingArea;
            var point = new Point(area.Right - Width - 12, area.Bottom - Height - 12);
            if (Location != point) Location = point;
        }
        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == 0x007E || m.Msg == 0x001A) DockCorner();
        }
        private void Reveal() { Show(); DockCorner(); Activate(); }
        private async Task RefreshDevices()
        {
            if (sdk == null) throw new IOException("Selecione o SDK primeiro.");
            string selected = devices.SelectedItem as string;
            var names = await sdk.ListAvds();
            devices.Items.Clear();
            devices.Items.AddRange(names);
            if (selected != null && devices.Items.Contains(selected)) devices.SelectedItem = selected;
            else if (names.Length > 0) devices.SelectedIndex = 0;
            status.Text = names.Length == 0 ? "Nenhum emulador. Clique em Criar." : names.Length + " emuladores disponíveis";
        }
        private async Task Guard(Func<Task> action)
        {
            if (busy) return;
            busy = true;
            open.Enabled = refresh.Enabled = create.Enabled = config.Enabled = false;
            try { await action(); }
            catch (Exception error) { status.Text = "Não foi possível concluir."; MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally {
                busy = false;
                if (!IsDisposed) { config.Enabled = true; refresh.Enabled = create.Enabled = sdk != null; open.Enabled = sdk != null && devices.Items.Count > 0; }
            }
        }
    }

    internal sealed class CreateDialog : Form
    {
        private readonly AndroidSdk sdk;
        private readonly TextBox name = new TextBox { Dock = DockStyle.Fill, MaxLength = 64, Text = "Meu_Android" };
        private readonly ComboBox images = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly ComboBox profiles = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        private readonly Button install = new Button { Text = "Instalar ferramentas de criação", AutoSize = true };
        private readonly Button create = new Button { Text = "Criar emulador", AutoSize = true, Enabled = false };
        private readonly Label status = new Label { Dock = DockStyle.Fill, Text = "Carregando…" };
        private bool busy;
        public CreateDialog(AndroidSdk sdk)
        {
            this.sdk = sdk;
            Text = "Criar emulador";
            Font = new Font("Segoe UI", 9F);
            ClientSize = new Size(490, 330);
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            var table = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 1, RowCount = 9 };
            foreach (int height in new[] { 22, 32, 22, 34, 22, 34, 48, 42, 42 }) table.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
            table.Controls.Add(new Label { Text = "Nome", AutoSize = true }); table.Controls.Add(name);
            table.Controls.Add(new Label { Text = "Imagem Android instalada", AutoSize = true }); table.Controls.Add(images);
            table.Controls.Add(new Label { Text = "Modelo do dispositivo", AutoSize = true }); table.Controls.Add(profiles);
            table.Controls.Add(new Label { Text = "Reutiliza as imagens do seu SDK. Para outras versões Android,\ninstale a imagem pelo SDK Manager.", Dock = DockStyle.Fill });
            var actions = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
            actions.Controls.AddRange(new Control[] { create, install }); table.Controls.Add(actions); table.Controls.Add(status);
            Controls.Add(table);
            Shown += async (s, e) => await Execute(LoadOptions);
            install.Click += async (s, e) => await Execute(async () => {
                await sdk.InstallTools(message => { if (!IsDisposed) status.Text = message; });
                await LoadOptions();
            });
            create.Click += async (s, e) => await Execute(async () => {
                status.Text = "Criando dispositivo…";
                await sdk.Create(name.Text.Trim(), images.SelectedItem as string, profiles.SelectedItem as string);
                MessageBox.Show(this, "Emulador criado. Selecione-o no painel e clique em Abrir.", Text);
                DialogResult = DialogResult.OK;
                Close();
            });
            FormClosing += (s, e) => { if (busy && e.CloseReason == CloseReason.UserClosing && DialogResult != DialogResult.OK) e.Cancel = true; };
        }
        private async Task LoadOptions()
        {
            images.Items.Clear(); images.Items.AddRange(sdk.Images());
            if (images.Items.Count > 0) images.SelectedIndex = 0;
            if (sdk.ToolsDirectory() == null) { status.Text = "Instale as ferramentas oficiais do Google (~150 MB)."; return; }
            profiles.Items.Clear(); profiles.Items.AddRange(await sdk.Profiles());
            if (profiles.Items.Count > 0) profiles.SelectedIndex = profiles.Items.Contains("pixel_5") ? profiles.Items.IndexOf("pixel_5") : 0;
            status.Text = images.Items.Count == 0 ? "Nenhuma imagem instalada. Use o SDK Manager." : "Pronto para criar.";
        }
        private async Task Execute(Func<Task> action)
        {
            if (busy) return;
            busy = true; create.Enabled = install.Enabled = name.Enabled = images.Enabled = profiles.Enabled = false;
            try { await action(); }
            catch (Exception error) { status.Text = "Não foi possível concluir."; MessageBox.Show(this, error.Message, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning); }
            finally {
                busy = false;
                if (!IsDisposed) {
                    name.Enabled = images.Enabled = profiles.Enabled = true;
                    install.Enabled = sdk.ToolsDirectory() == null;
                    create.Enabled = images.SelectedItem != null && profiles.SelectedItem != null;
                }
            }
        }
    }
}
