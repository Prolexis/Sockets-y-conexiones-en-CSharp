using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SERVIDORES_SOCKETS
{
    /// <summary>
    /// FormCliente — Interfaz exclusiva del cliente TCP.
    ///
    /// TÉCNICAS UI:
    /// • Burbujas de chat: Panel con Region = new Region(GraphicsPath redondeado).
    ///   TextRenderer.MeasureText calcula el tamaño antes de crear el panel.
    ///   Propio → derecha + color acento. Recibido → izquierda + color neutro.
    /// • Lista contactos: ListBox.OwnerDraw con avatar circular GDI+ y punto online.
    /// • Buttons: FlatStyle.Flat + FlatAppearance.BorderSize=0 → sin borde Windows.
    /// • Tema: AplicarTema() recursivo, un único punto para dark/light.
    /// • Cue banners nativos (placeholder): SendMessage(EM_SETCUEBANNER).
    /// • Header: FlowLayoutPanel(Dock=Right) → sin coordenadas manuales.
    /// </summary>
    public class FormCliente : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SendMessage(IntPtr h, int msg, int w, [MarshalAs(UnmanagedType.LPWStr)] string l);
        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
        private static extern int WinMsg(IntPtr h, int msg, int w, int l);
        private const int EM_SETCUEBANNER = 0x1501;
        private const int WM_SETREDRAW    = 0x000B;

        private readonly ClienteTcp _client = new();
        private bool _dark = true;
        private int  _nextY = 8;

        // Controles
        private TextBox  txtIp    = null!;
        private TextBox  txtPort  = null!;
        private TextBox  txtUser  = null!;
        private Button   btnConn  = null!;
        private Button   btnDisc  = null!;
        private Button   btnPing  = null!;
        private Label    lblStat  = null!;
        private ListBox  lstConts = null!;
        private Panel    pnlChat  = null!;
        private ComboBox cmbDest  = null!;
        private TextBox  txtMsg   = null!;
        private Button   btnSend  = null!;
        private Button   btnFile  = null!;
        private Button   btnTheme = null!;

        static readonly Color Ac  = Color.FromArgb( 79,  70, 229);
        static readonly Color Ok  = Color.FromArgb( 16, 185, 129);
        static readonly Color Err = Color.FromArgb(239,  68,  68);

        Color _bg, _card, _txt, _mut, _inp, _chatBg;

        public FormCliente()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            InitColors();
            BuildUI();
            AttachClientEvents();
            Load        += OnLoad;
            FormClosing += (_, _) => _client.Disconnect();
        }

        void InitColors()
        {
            _bg     = Color.FromArgb( 12,  18,  45);
            _card   = Color.FromArgb( 20,  30,  64);
            _txt    = Color.FromArgb(248, 250, 252);
            _mut    = Color.FromArgb(148, 163, 184);
            _inp    = Color.FromArgb( 12,  18,  45);
            _chatBg = Color.FromArgb(  8,  12,  32);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CONSTRUCCIÓN
        // ══════════════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text          = "SocketChat Pro — Cliente";
            Size          = new Size(1120, 740);
            MinimumSize   = new Size(860, 580);
            StartPosition = FormStartPosition.CenterScreen;
            Font          = new Font("Segoe UI", 10F);
            BackColor     = _bg;
            ForeColor     = _txt;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, RowCount = 4, ColumnCount = 1,
                BackColor = _bg, Padding = new Padding(12), Margin = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,  68));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
            root.RowStyles.Add(new RowStyle(SizeType.Percent,  100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,  48));
            Controls.Add(root);

            root.Controls.Add(BuildHeader(),   0, 0);
            root.Controls.Add(BuildConexion(), 0, 1);
            root.Controls.Add(BuildChat(),     0, 2);
            root.Controls.Add(BuildInput(),    0, 3);
        }

        // ── HEADER ────────────────────────────────────────────────────────────
        Panel BuildHeader()
        {
            var pnl = new Panel { Dock = DockStyle.Fill, BackColor = _card, Name = "pnlHdr" };
            pnl.Paint += (_, e) =>
            {
                Color sep = _dark ? Color.FromArgb(35, 50, 90) : Color.FromArgb(195, 210, 235);
                e.Graphics.DrawLine(new Pen(sep, 1f), 0, pnl.Height - 1, pnl.Width, pnl.Height - 1);
            };

            var logo = new Panel { Size = new Size(42, 42), Location = new Point(10, 13), BackColor = Color.Transparent };
            logo.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var lb = new LinearGradientBrush(logo.ClientRectangle, Color.FromArgb(139, 92, 246), Ac, 135f);
                e.Graphics.FillEllipse(lb, new Rectangle(0, 0, 41, 41));
                using var f  = new Font("Segoe UI Black", 18F, FontStyle.Bold);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString("C", f, Brushes.White, new RectangleF(0, 0, 41, 41), sf);
            };

            var lblTitle = new Label
            {
                Text = "Cliente TCP", Name = "lblTitle",
                Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold),
                ForeColor = _txt, AutoSize = true, Location = new Point(62, 20),
                BackColor = Color.Transparent
            };

            lblStat = new Label
            {
                Text = "  Desconectado", Name = "lblStat",
                Font = new Font("Segoe UI Black", 9F, FontStyle.Bold),
                ForeColor = Err, AutoSize = true, Location = new Point(222, 24),
                BackColor = Color.Transparent
            };

            // Botones a la derecha con FlowLayoutPanel(Dock=Right)
            btnTheme = MkBtn("Modo Claro", Color.FromArgb(40, 55, 100), _txt, new Size(110, 34));
            btnTheme.Name   = "btnTheme";
            btnTheme.Click += (_, _) => ToggleTema();

            var btnBack = MkBtn("<- Inicio", Err, Color.White, new Size(90, 34));
            btnBack.Click += (_, _) => Close();

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right, AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
                BackColor = Color.Transparent, Padding = new Padding(0, 15, 8, 0)
            };
            flow.Controls.Add(btnTheme);
            flow.Controls.Add(new Panel { Width = 8, Height = 1, BackColor = Color.Transparent });
            flow.Controls.Add(btnBack);

            pnl.Controls.AddRange(new Control[] { logo, lblTitle, lblStat, flow });
            return pnl;
        }

        // ── PANEL CONEXION ────────────────────────────────────────────────────
        GroupBox BuildConexion()
        {
            var gb = new GroupBox
            {
                Dock = DockStyle.Fill, Text = "CONEXION AL SERVIDOR",
                Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
                BackColor = _card, ForeColor = _txt,
                Margin = new Padding(0, 6, 0, 4), Name = "gbConn"
            };
            gb.Paint += GbPaint;

            // 2 filas: labels (20px) | inputs+botones (30px)
            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 6, RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(12, 26, 12, 8), Margin = Padding.Empty
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));  // IP del Servidor
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 85));  // Puerto
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 35));  // Nombre de Usuario
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 102)); // Conectar
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 108)); // Desconectar
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 70));  // Ping
            
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));   // labels
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // inputs y botones fijados a 30px

            // Fila 0: etiquetas
            tlp.Controls.Add(MkLbl("IP del Servidor:"),    0, 0);
            tlp.Controls.Add(MkLbl("Puerto:"),             1, 0);
            tlp.Controls.Add(MkLbl("Nombre de Usuario:"),  2, 0);

            // Fila 1: campos de texto
            txtIp   = MkField("127.0.0.1");
            txtPort = MkField("8080");
            txtUser = MkField("Cliente1");

            btnConn = MkBtn("Conectar",    Ac,  Color.White, new Size(1, 1));
            btnConn.Dock = DockStyle.Fill; btnConn.Name = "btnConn"; btnConn.Margin = new Padding(4, 2, 0, 2);
            btnConn.Click += async (_, _) => await Conectar();

            btnDisc = MkBtn("Desconectar", Err, Color.White, new Size(1, 1));
            btnDisc.Dock = DockStyle.Fill; btnDisc.Name = "btnDisc"; btnDisc.Enabled = false; btnDisc.Margin = new Padding(4, 2, 0, 2);
            btnDisc.Click += (_, _) => _client.Disconnect();

            btnPing = MkBtn("Ping", Color.FromArgb(50, 70, 110), Color.White, new Size(1, 1));
            btnPing.Dock = DockStyle.Fill; btnPing.Name = "btnPing"; btnPing.Enabled = false; btnPing.Margin = new Padding(4, 2, 0, 2);
            btnPing.Click += async (_, _) => await _client.EnviarPingAsync();

            tlp.Controls.Add(txtIp,    0, 1);
            tlp.Controls.Add(txtPort,  1, 1);
            tlp.Controls.Add(txtUser,  2, 1);
            tlp.Controls.Add(btnConn,  3, 1);
            tlp.Controls.Add(btnDisc,  4, 1);
            tlp.Controls.Add(btnPing,  5, 1);

            gb.Controls.Add(tlp);
            return gb;
        }

        // ── AREA DE CHAT ──────────────────────────────────────────────────────
        Control BuildChat()
        {
            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                BackColor = Color.Transparent, Margin = Padding.Empty
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Contactos
            var gbC = new GroupBox
            {
                Dock = DockStyle.Fill, Text = "CONTACTOS",
                Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
                BackColor = _card, ForeColor = _txt,
                Margin = new Padding(0, 0, 5, 0), Name = "gbConts"
            };
            gbC.Paint += GbPaint;

            lstConts = new ListBox
            {
                Dock = DockStyle.Fill, DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = 36, BorderStyle = BorderStyle.None,
                BackColor = _inp, ForeColor = _txt, Font = new Font("Segoe UI", 10F)
            };
            lstConts.DrawItem += DrawContact;

            var wC = new Panel { Dock = DockStyle.Fill, BackColor = _card, Padding = new Padding(4, 26, 4, 4) };
            wC.Controls.Add(lstConts);
            gbC.Controls.Add(wC);
            tlp.Controls.Add(gbC, 0, 0);

            // Chat
            var gbM = new GroupBox
            {
                Dock = DockStyle.Fill, Text = "CHAT",
                Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
                BackColor = _card, ForeColor = _txt,
                Margin = new Padding(5, 0, 0, 0), Name = "gbChat"
            };
            gbM.Paint += GbPaint;

            pnlChat = new Panel
            {
                Dock = DockStyle.Fill, AutoScroll = true,
                BackColor = _chatBg, Name = "pnlChat"
            };

            var wM = new Panel { Dock = DockStyle.Fill, BackColor = _card, Padding = new Padding(6, 26, 6, 4) };
            wM.Controls.Add(pnlChat);
            gbM.Controls.Add(wM);
            tlp.Controls.Add(gbM, 1, 0);

            return tlp;
        }

        // ── BARRA DE INPUT ────────────────────────────────────────────────────
        Control BuildInput()
        {
            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1,
                BackColor = Color.Transparent, Margin = new Padding(0, 4, 0, 0)
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 152));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            cmbDest = new ComboBox
            {
                Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList,
                Font = new Font("Segoe UI", 10F), Margin = new Padding(0, 0, 6, 0),
                BackColor = _inp, ForeColor = _txt
            };
            cmbDest.Items.Add("(Todos)");
            cmbDest.SelectedIndex = 0;

            txtMsg = new TextBox
            {
                Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10F), Margin = new Padding(0, 0, 6, 0),
                BackColor = _inp, ForeColor = _txt
            };
            txtMsg.KeyDown += (_, e) => { if (e.KeyCode == Keys.Enter && !e.Shift) { e.SuppressKeyPress = true; _ = EnviarMsg(); } };
            txtMsg.Enter += TxtEnter; txtMsg.Leave += TxtLeave;

            btnSend = MkBtn("Enviar",  Ac,  Color.White, new Size(1,1));
            btnSend.Dock = DockStyle.Fill; btnSend.Name = "btnSend"; btnSend.Enabled = false; btnSend.Margin = new Padding(0,0,4,0);
            btnSend.Click += async (_, _) => await EnviarMsg();

            btnFile = MkBtn("Archivo", Color.FromArgb(50, 70, 110), Color.White, new Size(1,1));
            btnFile.Dock = DockStyle.Fill; btnFile.Name = "btnFile"; btnFile.Enabled = false;
            btnFile.Click += BtnFile_Click;

            tlp.Controls.Add(cmbDest, 0, 0);
            tlp.Controls.Add(txtMsg,  1, 0);
            tlp.Controls.Add(btnSend, 2, 0);
            tlp.Controls.Add(btnFile, 3, 0);
            return tlp;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CARGA Y EVENTOS
        // ══════════════════════════════════════════════════════════════════════
        void OnLoad(object? s, EventArgs e)
        {
            int d = 1;
            try { DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref d, 4); } catch { }
            AplicarTema();
            // Cue banners nativos — texto placeholder para TextBox en Win32
            try { SendMessage(txtIp.Handle,   EM_SETCUEBANNER, 0, "ej: 192.168.1.10"); } catch { }
            try { SendMessage(txtPort.Handle, EM_SETCUEBANNER, 0, "ej: 8080"); }         catch { }
            try { SendMessage(txtUser.Handle, EM_SETCUEBANNER, 0, "Tu nombre..."); }      catch { }
            try { SendMessage(txtMsg.Handle,  EM_SETCUEBANNER, 0, "Escribe un mensaje..."); } catch { }
        }

        void AttachClientEvents()
        {
            _client.OnLog += (msg, lvl) => SafeUI(() => Burbuja("Sistema", $"[{lvl.ToString().ToUpper()}] {msg}"));
            _client.OnConnectionStatusChanged += ok => SafeUI(() => UpdateUI(ok));
            _client.OnChatMessageReceived     += (from, msg) => SafeUI(() => Burbuja(from, msg));
            _client.OnUserListUpdated         += users => SafeUI(() => ActualizarContactos(users));
            _client.OnFileIncomingStarted     += (from, _, name, size) =>
                SafeUI(() => Burbuja(from, $"[ARCHIVO ENTRANTE] \"{name}\" ({size / 1024} KB)..."));
            _client.OnFileTransferCompleted   += (_, from, path) =>
                SafeUI(() => Burbuja(from, $"[OK] Guardado en: {path}"));
            _client.OnFileTransferError       += (_, msg) =>
                SafeUI(() => Burbuja("Sistema", $"[ERROR] {msg}"));

            _client.SolicitarRutaGuardado = (nombre, size) =>
            {
                string? ruta = null;
                if (IsDisposed || !IsHandleCreated) return null;
                Invoke(() =>
                {
                    using var dlg = new SaveFileDialog
                    {
                        FileName = nombre, Title = $"Guardar archivo ({size / 1024} KB)",
                        OverwritePrompt = true
                    };
                    if (dlg.ShowDialog(this) == DialogResult.OK) ruta = dlg.FileName;
                });
                return ruta;
            };
        }

        async System.Threading.Tasks.Task Conectar()
        {
            // Validaciones completas antes de conectar
            string ip   = txtIp.Text.Trim();
            string portS= txtPort.Text.Trim();
            string user = txtUser.Text.Trim();

            if (string.IsNullOrEmpty(ip))
            { MsgError("Ingresa la IP o hostname del servidor.", "IP requerida"); return; }
            if (!int.TryParse(portS, out int p) || p < 1 || p > 65535)
            { MsgError("Puerto invalido. Debe ser un numero entre 1 y 65535.", "Puerto invalido"); return; }
            if (string.IsNullOrEmpty(user) || user.Length < 2)
            { MsgError("El nombre de usuario debe tener al menos 2 caracteres.", "Usuario invalido"); return; }
            if (user.Contains('|') || user.Contains(' '))
            { MsgError("El nombre no puede contener espacios ni el caracter '|'.", "Usuario invalido"); return; }

            btnConn.Enabled = false;
            btnConn.Text    = "Conectando...";
            try
            {
                await _client.ConnectAsync(ip, p, user);
            }
            catch (Exception ex)
            {
                MsgError($"No se pudo conectar al servidor:\n{ex.Message}", "Error de conexion");
                btnConn.Enabled = true;
                btnConn.Text    = "Conectar";
            }
        }

        void UpdateUI(bool on)
        {
            btnConn.Enabled = !on;
            btnConn.Text    = "Conectar";
            btnDisc.Enabled = on;
            btnPing.Enabled = on;
            btnSend.Enabled = on;
            btnFile.Enabled = on;
            txtIp.ReadOnly = txtPort.ReadOnly = txtUser.ReadOnly = on;
            lblStat.Text      = on ? "  Conectado"    : "  Desconectado";
            lblStat.ForeColor = on ? Ok                : Err;
        }

        void ActualizarContactos(List<string> users)
        {
            string prev = cmbDest.SelectedItem as string ?? "(Todos)";
            cmbDest.Items.Clear(); cmbDest.Items.Add("(Todos)");
            lstConts.Items.Clear(); lstConts.Items.Add("> Todos");
            foreach (var u in users.Where(u => !u.Equals(_client.UsuarioActual, StringComparison.OrdinalIgnoreCase)))
            { cmbDest.Items.Add(u); lstConts.Items.Add(u); }
            int idx = cmbDest.Items.IndexOf(prev);
            cmbDest.SelectedIndex = idx >= 0 ? idx : 0;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  BURBUJAS DE CHAT
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Crea una burbuja de chat como Panel con Region redondeada.
        ///
        /// TÉCNICA Region: Panel.Region = new Region(GraphicsPath) recorta físicamente
        /// el panel a la forma de cápsula. Las esquinas del chat container son visibles
        /// detrás, creando el efecto de burbuja real (no solo borde dibujado).
        ///
        /// TÉCNICA MeasureText: TextRenderer.MeasureText calcula el tamaño necesario
        /// ANTES de crear el panel, con el mismo ancho máximo que usará, para evitar
        /// el problema chicken-and-egg de AutoSize en controles con Region.
        /// </summary>
        void Burbuja(string from, string texto)
        {
            bool propio = from.Equals(_client.UsuarioActual, StringComparison.OrdinalIgnoreCase);
            bool system = from.Equals("Sistema", StringComparison.OrdinalIgnoreCase) || from.StartsWith("[");

            int chatW = Math.Max(pnlChat.ClientSize.Width, 200);
            int maxW  = (int)(chatW * 0.68);
            int pH    = 20; // padding horizontal total
            int pV    = 14; // padding vertical total

            string encabezado = (propio || system) ? "" : $"{from}\n";
            string hora       = DateTime.Now.ToString("HH:mm");
            string fullText   = $"{encabezado}{texto}   {hora}";

            var fnt = new Font("Segoe UI", 10.5F);
            var measured = TextRenderer.MeasureText(fullText, fnt,
                new Size(maxW - pH, 4000), TextFormatFlags.WordBreak | TextFormatFlags.Left);

            int bW = Math.Min(measured.Width + pH + 4, maxW);
            int bH = measured.Height + pV;

            Color bgBurbuja = system ? Color.FromArgb(50, 65, 105)
                            : propio  ? Ac
                            : (_dark  ? Color.FromArgb(28, 42, 85) : Color.FromArgb(222, 228, 240));

            Color fgBurbuja = (system || propio || _dark) ? Color.White : Color.FromArgb(15, 23, 42);

            var bubble = new Panel { Size = new Size(bW, bH), BackColor = bgBurbuja };

            // Recorte a forma redondeada (radio 12)
            using var rp = MkRound(new Rectangle(0, 0, bW, bH), 12);
            bubble.Region = new Region(rp);

            bubble.Controls.Add(new Label
            {
                Text = fullText, Font = fnt, ForeColor = fgBurbuja,
                BackColor = Color.Transparent,
                Location = new Point(pH / 2, pV / 2),
                Size = new Size(bW - pH, bH - pV),
                TextAlign = ContentAlignment.TopLeft
            });

            int x = propio ? chatW - bW - 8 : 8;
            bubble.Location = new Point(x, _nextY);
            pnlChat.Controls.Add(bubble);
            _nextY += bH + 8;

            pnlChat.AutoScrollMinSize = new Size(0, _nextY + 8);
            pnlChat.ScrollControlIntoView(bubble);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  ENVÍO
        // ══════════════════════════════════════════════════════════════════════
        async System.Threading.Tasks.Task EnviarMsg()
        {
            string txt = txtMsg.Text.Trim();
            if (string.IsNullOrEmpty(txt)) return;

            string sel  = cmbDest.SelectedItem as string ?? "(Todos)";
            string dest = sel == "(Todos)" ? "" : sel;

            Burbuja(_client.UsuarioActual, txt);
            await _client.EnviarMensajeAsync(dest, txt);
            txtMsg.Clear();
            txtMsg.Focus();
        }

        async void BtnFile_Click(object? s, EventArgs e)
        {
            using var dlg = new OpenFileDialog { Title = "Seleccionar archivo para enviar" };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            // Validar tamaño de archivo (máx 50 MB)
            var fi = new FileInfo(dlg.FileName);
            if (fi.Length > 50 * 1024 * 1024)
            { MsgError("Archivo demasiado grande. El limite es 50 MB.", "Archivo invalido"); return; }

            string sel  = cmbDest.SelectedItem as string ?? "(Todos)";
            string dest = sel == "(Todos)" ? "" : sel;
            btnFile.Enabled = false;
            Burbuja(_client.UsuarioActual, $"[ARCHIVO] Enviando \"{fi.Name}\" ({fi.Length / 1024} KB)...");
            try { await _client.EnviarArchivoAsync(dest, dlg.FileName); }
            catch (Exception ex) { MsgError($"Error al enviar el archivo:\n{ex.Message}", "Error"); }
            finally { btnFile.Enabled = true; }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CONTACTOS (OwnerDraw)
        // ══════════════════════════════════════════════════════════════════════
        void DrawContact(object? s, DrawItemEventArgs e)
        {
            if (e.Index < 0) return;
            var g = e.Graphics; g.SmoothingMode = SmoothingMode.AntiAlias;
            bool sel = (e.State & DrawItemState.Selected) != 0;
            Color bg = sel ? Ac : _inp;
            g.FillRectangle(new SolidBrush(bg), e.Bounds);
            string item = lstConts.Items[e.Index]?.ToString() ?? "";
            bool todos = item.StartsWith(">");
            if (!todos)
            {
                var cr = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 4, 28, 28);
                g.FillEllipse(new SolidBrush(AvatarColor(item)), cr);
                using var fA = new Font("Segoe UI Semibold", 9F, FontStyle.Bold);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(item.Length > 0 ? char.ToUpper(item[0]).ToString() : "?", fA, Brushes.White, cr, sf);
                var dot = new Rectangle(cr.Right - 9, cr.Bottom - 9, 9, 9);
                g.FillEllipse(new SolidBrush(Ok), dot);
                g.DrawEllipse(new Pen(bg, 1.5f), dot);
                var tr = new Rectangle(cr.Right + 4, e.Bounds.Y, e.Bounds.Width - cr.Right, e.Bounds.Height);
                TextRenderer.DrawText(g, item, new Font("Segoe UI", 9F), tr,
                    sel ? Color.White : _txt, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
            else
            {
                TextRenderer.DrawText(g, item.TrimStart('>').Trim(), new Font("Segoe UI", 9F),
                    new Rectangle(e.Bounds.X + 8, e.Bounds.Y, e.Bounds.Width, e.Bounds.Height),
                    sel ? Color.White : _mut, TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
        }

        static Color AvatarColor(string s)
        {
            var pal = new[] {
                Color.FromArgb(99,102,241), Color.FromArgb(16,185,129), Color.FromArgb(245,158,11),
                Color.FromArgb(239,68, 68), Color.FromArgb(139,92,246), Color.FromArgb(20,184,166),
                Color.FromArgb(236,72,153)
            };
            return pal[Math.Abs(s.GetHashCode()) % pal.Length];
        }

        // ══════════════════════════════════════════════════════════════════════
        //  TEMA
        // ══════════════════════════════════════════════════════════════════════
        void ToggleTema()
        {
            WinMsg(Handle, WM_SETREDRAW, 0, 0);
            _dark = !_dark;
            int dv = _dark ? 1 : 0;
            try { DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref dv, 4); } catch { }
            AplicarTema();
            lstConts.Invalidate();
            WinMsg(Handle, WM_SETREDRAW, 1, 0);
            Refresh();
        }

        void AplicarTema()
        {
            if (_dark)
            {
                _bg     = Color.FromArgb( 12,  18,  45);
                _card   = Color.FromArgb( 20,  30,  64);
                _txt    = Color.FromArgb(248, 250, 252);
                _mut    = Color.FromArgb(148, 163, 184);
                _inp    = Color.FromArgb( 12,  18,  45);
                _chatBg = Color.FromArgb(  8,  12,  32);
            }
            else
            {
                _bg     = Color.FromArgb(238, 242, 255);
                _card   = Color.White;
                _txt    = Color.FromArgb( 15,  23,  42);
                _mut    = Color.FromArgb( 71,  85, 105);
                _inp    = Color.White;
                _chatBg = Color.FromArgb(228, 234, 255);
            }

            BackColor = _bg; ForeColor = _txt;
            if (pnlChat != null) pnlChat.BackColor = _chatBg;
            btnTheme.Text      = _dark ? "Modo Claro" : "Modo Oscuro";
            btnTheme.BackColor = _dark ? Color.FromArgb(40, 55, 100) : Color.FromArgb(200, 215, 245);
            btnTheme.ForeColor = _txt;

            TemaRec(this);
            foreach (var c in Desc(this)) if (c is GroupBox) c.Invalidate();
        }

        void TemaRec(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                switch (c)
                {
                    case GroupBox gb: gb.BackColor = _card; gb.ForeColor = _txt; break;
                    case Panel p when p.Name == "pnlHdr": p.BackColor = _card; p.Invalidate(); break;
                    case Panel p when p.Name == "pnlChat": break; // manejo especial
                    case Panel p when p.BackColor != Color.Transparent: p.BackColor = _card; break;
                    case TableLayoutPanel t: t.BackColor = Color.Transparent; break;
                    case FlowLayoutPanel f: f.BackColor = Color.Transparent; break;
                    case Button b when b.Name == "btnConn":
                        b.BackColor = Ac; b.ForeColor = Color.White;
                        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(60,50,195); break;
                    case Button b when b.Name is "btnDisc":
                        b.BackColor = Err; b.ForeColor = Color.White;
                        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(210,40,40); break;
                    case Button b when b.Name == "btnSend":
                        b.BackColor = Ac; b.ForeColor = Color.White;
                        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(60,50,195); break;
                    case Button b when b.Name is "btnPing" or "btnFile":
                        b.BackColor = _dark ? Color.FromArgb(50,70,110) : Color.FromArgb(195,210,245);
                        b.ForeColor = _dark ? Color.White : _txt;
                        b.FlatAppearance.MouseOverBackColor = _dark ? Color.FromArgb(65,88,138) : Color.FromArgb(175,192,235); break;
                    case Button b when b.Name is "btnTheme" or "btnBack": break;
                    case Button b:
                        b.BackColor = Err; b.ForeColor = Color.White;
                        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(210,40,40); break;
                    case TextBox tb: tb.BackColor = _inp; tb.ForeColor = _txt; break;
                    case ComboBox cb: cb.BackColor = _inp; cb.ForeColor = _txt; break;
                    case ListBox lb: lb.BackColor = _inp; lb.ForeColor = _txt; break;
                    case Label lb when lb.Name == "lblTitle":
                        lb.ForeColor = _txt; break;
                    case Label lb when lb.Name == "lblStat":
                        break;
                    case Label lb: lb.ForeColor = _mut; break;
                }
                if (c.Controls.Count > 0) TemaRec(c);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  GROUPBOX REDONDEADO (GDI+)
        // ══════════════════════════════════════════════════════════════════════
        void GbPaint(object? s, PaintEventArgs e)
        {
            if (s is not GroupBox gb) return;
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float sc  = DeviceDpi / 96f;
            Color brd = _dark ? Color.FromArgb(42, 58, 110) : Color.FromArgb(185, 200, 230);
            Color bk  = _dark ? Color.FromArgb(20, 30,  64) : Color.White;
            Color tc  = _dark ? Color.FromArgb(248,250,252) : Color.FromArgb(15, 23, 42);

            using (var br = new SolidBrush(bk)) g.FillRectangle(br, gb.ClientRectangle);

            var rect = new Rectangle(0, (int)(12 * sc), gb.Width - 1, gb.Height - (int)(13 * sc));
            using (var path = MkRound(rect, (int)(10 * sc)))
            using (var pen  = new Pen(brd, 1.5f))
                g.DrawPath(pen, path);

            if (!string.IsNullOrEmpty(gb.Text))
            {
                using var f   = new Font("Segoe UI Semibold", 8f + sc, FontStyle.Bold);
                using var br  = new SolidBrush(tc);
                using var bg2 = new SolidBrush(bk);
                var sz = g.MeasureString(gb.Text, f);
                g.FillRectangle(bg2, new RectangleF(9, 0, sz.Width + 8, sz.Height + 2));
                g.DrawString(gb.Text, f, br, 13, 0);
            }
        }

        void TxtEnter(object? s, EventArgs e) { if (s is TextBox t) t.BackColor = _dark ? Color.FromArgb(28,42,90) : Color.FromArgb(218,226,255); }
        void TxtLeave(object? s, EventArgs e) { if (s is TextBox t) t.BackColor = _inp; }

        // ══════════════════════════════════════════════════════════════════════
        //  HELPERS
        // ══════════════════════════════════════════════════════════════════════
        void SafeUI(Action a)
        {
            if (IsDisposed || !IsHandleCreated) return;
            if (InvokeRequired) { try { BeginInvoke(a); } catch { } } else a();
        }

        static void MsgError(string msg, string title) =>
            MessageBox.Show(msg, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);

        /// <summary>
        /// Crea un Button con FlatStyle=Flat y BorderSize=0 (sin borde Windows).
        /// </summary>
        static Button MkBtn(string text, Color back, Color fore, Size size)
        {
            var b = new Button
            {
                Text = text, Size = size, FlatStyle = FlatStyle.Flat,
                BackColor = back, ForeColor = fore, Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold),
                Margin = new Padding(2, 0, 0, 0)
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(back, 0.08f);
            return b;
        }

        TextBox MkField(string val)
        {
            var t = new TextBox
            {
                Text = val, Dock = DockStyle.Fill,
                BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 10F),
                BackColor = _inp, ForeColor = _txt
            };
            t.Enter += TxtEnter; t.Leave += TxtLeave;
            return t;
        }

        static Label MkLbl(string t) => new Label
        {
            Text = t, AutoSize = true, Dock = DockStyle.Fill,
            Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold), TextAlign = ContentAlignment.BottomLeft
        };

        static GraphicsPath MkRound(Rectangle r, int rad)
        {
            int d = Math.Max(2, rad * 2); var p = new GraphicsPath();
            p.AddArc(r.X,         r.Y,          d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
            p.AddArc(r.X,         r.Bottom - d, d, d,  90, 90);
            p.CloseFigure(); return p;
        }

        static IEnumerable<Control> Desc(Control c)
        {
            foreach (Control x in c.Controls) { yield return x; foreach (var y in Desc(x)) yield return y; }
        }
    }
}
