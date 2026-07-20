using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SERVIDORES_SOCKETS
{
    /// <summary>
    /// FormServidor — Interfaz exclusiva del servidor TCP.
    ///
    /// TÉCNICAS UI:
    /// • GroupBox redondeado: evento Paint usa GraphicsPath con AddArc (radio escalado por DPI).
    ///   FillRectangle elimina el borde Windows; DrawPath dibuja el nuevo borde suave.
    /// • Avatares en ListView: OwnerDraw=true + DrawSubItem dibuja círculo GDI+ con color
    ///   determinístico (Math.Abs(usuario.GetHashCode()) % 7 colores) mas la inicial.
    /// • Tema: AplicarTema() recorre todos los controles recursivamente via TemaRec().
    /// • Microinteracciones: FlatAppearance.MouseOverBackColor cambiado por tema.
    ///   FlatAppearance.BorderSize = 0 — sin bordes de Windows en modo Flat.
    ///   TextBox.Enter/Leave cambia BackColor al color de acento suave.
    /// • Header botones: FlowLayoutPanel(Dock=Right) sin coordenadas fijas.
    /// </summary>
    public class FormServidor : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;
        [DllImport("user32.dll", EntryPoint = "SendMessage", CharSet = CharSet.Auto)]
        private static extern int WinMsg(IntPtr h, int msg, int w, int l);
        private const int WM_SETREDRAW = 0x000B;

        private readonly ServidorTcp _server = new();
        private bool _dark = true;

        // Historial de sesiones para mostrar desconectados en la tabla
        private readonly List<ManejadorCliente> _clientesHistoricos = new();
        private readonly System.Collections.Generic.HashSet<string> _clientesDesconectados = new(StringComparer.OrdinalIgnoreCase);

        // Variables de animación LED de estado
        private System.Windows.Forms.Timer _ledTimer = null!;
        private float _ledAlpha = 1f;
        private bool _ledAlphaDown = true;

        // Controles clave
        private TextBox     txtPort    = null!;
        private Button      btnStart   = null!;
        private Button      btnStop    = null!;
        private Label       lblStatus  = null!;
        private Label       lblIPs     = null!;
        private ListView    lstClients = null!;
        private RichTextBox rtxtLog    = null!;
        private Button      btnTheme   = null!;

        // Paleta fija (acento, ok, error)
        static readonly Color Ac  = Color.FromArgb( 79,  70, 229);
        static readonly Color Ok  = Color.FromArgb( 16, 185, 129);
        static readonly Color Err = Color.FromArgb(239,  68,  68);

        // Colores de tema (recalculados en AplicarTema)
        Color _bg, _card, _txt, _mut, _inp;

        public FormServidor()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            InitTemaColors();  // colores oscuros desde el inicio
            BuildUI();
            AttachServerEvents();
            InitLedTimer();
            Load        += OnLoad;
            FormClosing += (_, _) => { _ledTimer.Stop(); _server.Stop(); };
        }

        void InitTemaColors()
        {
            _bg   = Color.FromArgb( 12,  18,  45);
            _card = Color.FromArgb( 20,  30,  64);
            _txt  = Color.FromArgb(248, 250, 252);
            _mut  = Color.FromArgb(148, 163, 184);
            _inp  = Color.FromArgb( 12,  18,  45);
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CONSTRUCCIÓN
        // ══════════════════════════════════════════════════════════════════════
        void BuildUI()
        {
            Text          = "SocketChat Pro — Servidor";
            Size          = new Size(1120, 720);
            MinimumSize   = new Size(940, 580);
            StartPosition = FormStartPosition.CenterScreen;
            Font          = new Font("Segoe UI", 10F);
            BackColor     = _bg;
            ForeColor     = _txt;

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
                BackColor = _bg, Padding = new Padding(12), Margin = Padding.Empty
            };
            root.RowStyles.Add(new RowStyle(SizeType.Absolute,  68));  // header
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 115));  // control
            root.RowStyles.Add(new RowStyle(SizeType.Percent,  100));  // main
            Controls.Add(root);

            root.Controls.Add(BuildHeader(),   0, 0);
            root.Controls.Add(BuildControl(),  0, 1);
            root.Controls.Add(BuildMain(),     0, 2);
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

            // Logo
            var logo = new Panel { Size = new Size(42, 42), Location = new Point(10, 13), BackColor = Color.Transparent };
            logo.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var lb = new LinearGradientBrush(logo.ClientRectangle, Ac, Color.FromArgb(139, 92, 246), 135f);
                e.Graphics.FillEllipse(lb, new Rectangle(0, 0, 41, 41));
                using var f  = new Font("Segoe UI Black", 18F, FontStyle.Bold);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString("S", f, Brushes.White, new RectangleF(0, 0, 41, 41), sf);
            };

            var lblTitle = new Label
            {
                Text = "Servidor TCP", Name = "lblTitle",
                Font = new Font("Segoe UI Semibold", 15F, FontStyle.Bold),
                ForeColor = _txt, AutoSize = true, Location = new Point(62, 20),
                BackColor = Color.Transparent
            };

            lblStatus = new Label
            {
                Text = "  DETENIDO", Name = "lblStatus",
                Font = new Font("Segoe UI Black", 9F, FontStyle.Bold),
                ForeColor = Err, AutoSize = true, Location = new Point(238, 24),
                BackColor = Color.Transparent
            };

            // Botones derechos: FlowLayoutPanel(Dock=Right) — sin coordenadas manuales
            // TECNICA: Dock=Right hace que el panel siempre esté pegado a la derecha
            // sin necesidad de Resize event ni coordenadas fijas.
            btnTheme = MkBtn("Modo Claro", Color.FromArgb(45, 60, 100), _txt, new Size(125, 34));
            btnTheme.Name = "btnTheme";
            btnTheme.Click += (_, _) => ToggleTema();

            var btnBack = MkBtn("<- Inicio", Err, Color.White, new Size(90, 34));
            btnBack.Click += (_, _) => Close();

            var flow = new FlowLayoutPanel
            {
                Dock = DockStyle.Right, AutoSize = true,
                FlowDirection = FlowDirection.LeftToRight, WrapContents = false,
                BackColor = Color.Transparent, Padding = new Padding(0, 15, 14, 0)
            };
            flow.Controls.Add(btnTheme);
            flow.Controls.Add(new Panel { Width = 8, Height = 1, BackColor = Color.Transparent });
            flow.Controls.Add(btnBack);

            pnl.Controls.AddRange(new Control[] { logo, lblTitle, lblStatus, flow });
            return pnl;
        }

        // ── PANEL DE CONTROL DEL SERVIDOR ────────────────────────────────────
        GroupBox BuildControl()
        {
            var gb = new GroupBox
            {
                Dock = DockStyle.Fill, Text = "CONTROL DEL SERVIDOR (TcpListener — IPAddress.Any)",
                Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
                BackColor = _card, ForeColor = _txt,
                Margin = new Padding(0, 6, 0, 4), Name = "gbCtrl"
            };
            gb.Paint += GbPaint;

            // Layout: 2 filas (etiquetas | inputs+botones)
            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 2,
                BackColor = Color.Transparent,
                Padding = new Padding(14, 26, 14, 8), Margin = Padding.Empty
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));  // IP
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 110)); // Puerto (label+field)
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112)); // Iniciar
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 112)); // Detener
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 20));   // labels
            tlp.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));   // inputs y botones fijados a 30px

            // Fila 0: etiquetas
            tlp.Controls.Add(MkLbl("IP de escucha (todas las interfaces):"), 0, 0);
            tlp.Controls.Add(MkLbl("Puerto:"), 1, 0);

            // Fila 1: inputs y botones
            lblIPs = new Label
            {
                Dock = DockStyle.Fill, Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                ForeColor = Ok, AutoEllipsis = true, TextAlign = ContentAlignment.MiddleLeft
            };

            txtPort = new TextBox
            {
                Dock = DockStyle.Fill, Text = "8080", BorderStyle = BorderStyle.FixedSingle,
                Font = new Font("Segoe UI", 9.75F), BackColor = _inp, ForeColor = _txt
            };
            txtPort.Enter += TxtEnter; txtPort.Leave += TxtLeave;

            btnStart = MkBtn("Iniciar", Ac, Color.White, new Size(1, 1));
            btnStart.Dock = DockStyle.Fill; btnStart.Name = "btnStart"; btnStart.Margin = new Padding(4, 2, 0, 2);
            btnStart.Click += (_, _) => IniciarServidor();

            btnStop = MkBtn("Detener", Err, Color.White, new Size(1, 1));
            btnStop.Dock = DockStyle.Fill; btnStop.Name = "btnStop"; btnStop.Enabled = false; btnStop.Margin = new Padding(4, 2, 0, 2);
            btnStop.Click += (_, _) => _server.Stop();

            tlp.Controls.Add(lblIPs,   0, 1);
            tlp.Controls.Add(txtPort,  1, 1);
            tlp.Controls.Add(btnStart, 2, 1);
            tlp.Controls.Add(btnStop,  3, 1);

            gb.Controls.Add(tlp);
            return gb;
        }

        // ── AREA PRINCIPAL (Clientes + Log) ───────────────────────────────────
        Control BuildMain()
        {
            var tlp = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                BackColor = Color.Transparent, Margin = Padding.Empty
            };
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 42));
            tlp.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 58));
            tlp.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            // Clientes
            var gbCli = new GroupBox
            {
                Dock = DockStyle.Fill, Text = "CLIENTES CONECTADOS",
                Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
                BackColor = _card, ForeColor = _txt,
                Margin = new Padding(0, 0, 5, 0), Name = "gbCli"
            };
            gbCli.Paint += GbPaint;

            lstClients = new ListView
            {
                Dock = DockStyle.Fill, View = View.Details, FullRowSelect = true,
                HeaderStyle = ColumnHeaderStyle.Nonclickable,
                BorderStyle = BorderStyle.None, OwnerDraw = true,
                Font = new Font("Segoe UI", 10F),
                BackColor = _inp, ForeColor = _txt
            };
            lstClients.Columns.Add("Usuario",  140);
            lstClients.Columns.Add("IP",       110);
            lstClients.Columns.Add("Puerto",    60);
            lstClients.Columns.Add("Hora",      90);
            lstClients.Columns.Add("Estado",    -2);
            lstClients.DrawColumnHeader += (_, e) => e.DrawDefault = true;
            lstClients.DrawItem         += (_, e) => e.DrawBackground();
            lstClients.DrawSubItem      += DrawSubItem;
            lstClients.Resize           += (_, _) =>
            { if (lstClients.Columns.Count > 4) lstClients.Columns[4].Width = -2; };

            var wCli = new Panel { Dock = DockStyle.Fill, BackColor = _card, Padding = new Padding(6, 26, 6, 6) };
            wCli.Controls.Add(lstClients);
            gbCli.Controls.Add(wCli);
            tlp.Controls.Add(gbCli, 0, 0);

            // Log
            var gbLog = new GroupBox
            {
                Dock = DockStyle.Fill, Text = "LOG DE EVENTOS (tiempo real)",
                Font = new Font("Segoe UI Semibold", 10.5F, FontStyle.Bold),
                BackColor = _card, ForeColor = _txt,
                Margin = new Padding(5, 0, 0, 0), Name = "gbLog"
            };
            gbLog.Paint += GbPaint;

            rtxtLog = new RichTextBox
            {
                Dock = DockStyle.Fill, ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = Color.FromArgb(8, 10, 22),
                ForeColor = Color.FromArgb(220, 220, 230),
                Font = new Font("Consolas", 10F),
                ScrollBars = RichTextBoxScrollBars.Vertical,
                Name = "rtxtLog"
            };

            var wLog = new Panel { Dock = DockStyle.Fill, BackColor = _card, Padding = new Padding(6, 26, 6, 6) };
            wLog.Controls.Add(rtxtLog);
            gbLog.Controls.Add(wLog);
            tlp.Controls.Add(gbLog, 1, 0);

            return tlp;
        }

        // ══════════════════════════════════════════════════════════════════════
        //  CARGA Y SERVIDOR
        // ══════════════════════════════════════════════════════════════════════
        void OnLoad(object? s, EventArgs e)
        {
            int d = 1;
            try { DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref d, 4); } catch { }
            AplicarTema();
            MostrarIPs();
        }

        void AttachServerEvents()
        {
            _server.OnLog                += (msg, lvl) => AddLog(msg, lvl);
            _server.OnClientConnected    += c => SafeUI(() => AlConectar(c));
            _server.OnClientDisconnected += c => SafeUI(() => AlDesconectar(c));
            _server.OnStateChanged       += ok => SafeUI(() => UpdateUI(ok));
        }

        void IniciarServidor()
        {
            string portStr = txtPort.Text.Trim();
            if (!int.TryParse(portStr, out int p) || p < 1 || p > 65535)
            {
                MsgError("Puerto invalido. Ingresa un numero entre 1 y 65535.", "Validacion");
                return;
            }
            try { _server.Start(p); }
            catch (Exception ex) { MsgError($"No se pudo iniciar el servidor:\n{ex.Message}", "Error de red"); }
        }

        void UpdateUI(bool on)
        {
            btnStart.Enabled = !on;
            btnStop.Enabled  =  on;
            txtPort.ReadOnly =  on;
            lblStatus.Text      = on ? "  ACTIVO"    : "  DETENIDO";
            lblStatus.ForeColor = on ? Ok             : Err;
            if (!on)
            {
                lock (_clientesHistoricos)
                {
                    _clientesHistoricos.Clear();
                    _clientesDesconectados.Clear();
                }
                lstClients.Items.Clear();
            }
        }

        void AlConectar(ManejadorCliente c)
        {
            lock (_clientesHistoricos)
            {
                _clientesHistoricos.RemoveAll(x => x.Usuario.Equals(c.Usuario, StringComparison.OrdinalIgnoreCase));
                _clientesHistoricos.Add(c);
                _clientesDesconectados.Remove(c.Usuario);
            }
            RefrescarLista();
        }

        void AlDesconectar(ManejadorCliente c)
        {
            lock (_clientesHistoricos)
            {
                _clientesDesconectados.Add(c.Usuario);
            }
            RefrescarLista();
        }

        void RefrescarLista()
        {
            lstClients.BeginUpdate();
            lstClients.Items.Clear();
            System.Collections.Generic.List<ManejadorCliente> copia;
            lock (_clientesHistoricos)
            {
                copia = new System.Collections.Generic.List<ManejadorCliente>(_clientesHistoricos);
            }
            foreach (var c in copia)
            {
                bool activo = !_clientesDesconectados.Contains(c.Usuario);
                var it = new ListViewItem(c.Usuario);
                it.SubItems.Add(c.IP);
                it.SubItems.Add(c.Puerto.ToString());
                it.SubItems.Add(c.HoraConexion.ToString("HH:mm:ss"));
                it.SubItems.Add(activo ? "Activo" : "Desconectado");
                lstClients.Items.Add(it);
            }
            lstClients.EndUpdate();
        }

        void MostrarIPs()
        {
            try
            {
                var ips = Dns.GetHostEntry(Dns.GetHostName())
                             .AddressList
                             .Where(i => i.AddressFamily == AddressFamily.InterNetwork)
                             .Select(i => i.ToString());
                lblIPs.Text = string.Join("   |   ", ips);
            }
            catch { lblIPs.Text = "No detectadas"; }
        }

        void AddLog(string msg, LogLevel lvl)
        {
            if (IsDisposed || !rtxtLog.IsHandleCreated) return;
            SafeUI(() =>
            {
                Color c = lvl == LogLevel.Success ? Ok
                        : lvl == LogLevel.Error   ? Err
                        : (_dark ? Color.FromArgb(215, 215, 225) : Color.FromArgb(45, 55, 75));
                rtxtLog.SelectionStart  = rtxtLog.TextLength;
                rtxtLog.SelectionColor  = c;
                rtxtLog.AppendText($"[{DateTime.Now:HH:mm:ss}] {msg}\r\n");
                rtxtLog.SelectionColor  = rtxtLog.ForeColor;
                rtxtLog.ScrollToCaret();
                if (rtxtLog.Lines.Length > 2000)
                {
                    int cut = rtxtLog.GetFirstCharIndexFromLine(400);
                    if (cut > 0) { rtxtLog.Select(0, cut); rtxtLog.SelectedText = ""; }
                }
            });
        }

        // ══════════════════════════════════════════════════════════════════════
        //  AVATARES (ListView OwnerDraw)
        // ══════════════════════════════════════════════════════════════════════
        void DrawSubItem(object? s, DrawListViewSubItemEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            bool sel = e.Item?.Selected ?? false;
            Color bg = sel ? Color.FromArgb(50, 68, 145) : (_dark ? Color.FromArgb(14, 22, 50) : Color.FromArgb(242, 245, 255));
            g.FillRectangle(new SolidBrush(bg), e.Bounds);

            if (e.ColumnIndex == 0 && e.Item != null)
            {
                string u  = e.Item.Text;
                var    cr = new Rectangle(e.Bounds.X + 5, e.Bounds.Y + 3, 24, 24);
                g.FillEllipse(new SolidBrush(AvatarColor(u)), cr);
                using var fA  = new Font("Segoe UI Semibold", 8.5F, FontStyle.Bold);
                using var sfA = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(u.Length > 0 ? char.ToUpper(u[0]).ToString() : "?", fA, Brushes.White, cr, sfA);
                
                // Determinar si está activo para pintar el punto
                bool activo = e.Item.SubItems.Count > 4 && e.Item.SubItems[4].Text.Equals("Activo", StringComparison.OrdinalIgnoreCase);
                var dot = new Rectangle(cr.Right - 8, cr.Bottom - 8, 9, 9);
                g.FillEllipse(new SolidBrush(activo ? Ok : Err), dot);
                g.DrawEllipse(new Pen(bg, 1.5f), dot);

                var tr = new Rectangle(cr.Right + 4, e.Bounds.Y, e.Bounds.Width - cr.Width - 10, e.Bounds.Height);
                Color fg = sel ? Color.White : (_dark ? Color.FromArgb(248,250,252) : Color.FromArgb(15,23,42));
                TextRenderer.DrawText(g, u, new Font("Segoe UI Semibold", 9F, FontStyle.Bold), tr, fg,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter);
            }
            else if (e.ColumnIndex == 4) // Columna de Estado (Chip visual)
            {
                string estado = e.SubItem?.Text ?? "Desconectado";
                bool activo   = estado.Equals("Activo", StringComparison.OrdinalIgnoreCase);

                Color chipBg = activo ? Color.FromArgb(32, Ok) : Color.FromArgb(32, Err);
                Color chipFg = activo ? Ok : Err;

                var rect = new Rectangle(e.Bounds.X + 6, e.Bounds.Y + 4, e.Bounds.Width - 12, e.Bounds.Height - 8);
                using (var path = MkRound(rect, 4))
                {
                    g.FillPath(new SolidBrush(chipBg), path);
                    g.DrawPath(new Pen(chipFg, 1f), path);
                }

                using var f = new Font("Segoe UI Semibold", 8F, FontStyle.Bold);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                g.DrawString(estado, f, new SolidBrush(chipFg), rect, sf);
            }
            else
            {
                Color fg = sel ? Color.White : (_dark ? Color.FromArgb(140,158,180) : Color.FromArgb(65,80,100));
                TextRenderer.DrawText(g, e.SubItem?.Text ?? "", new Font("Segoe UI", 9F),
                    e.Bounds, fg, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
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
        //  TEMA CLARO / OSCURO
        // ══════════════════════════════════════════════════════════════════════
        void ToggleTema()
        {
            WinMsg(Handle, WM_SETREDRAW, 0, 0);
            _dark = !_dark;
            int d = _dark ? 1 : 0;
            try { DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref d, 4); } catch { }
            AplicarTema();
            RefrescarLista();
            WinMsg(Handle, WM_SETREDRAW, 1, 0);
            Refresh();
        }

        /// <summary>
        /// Recalcula los colores del tema y los aplica de forma recursiva a todos los controles.
        /// Técnica: switch por nombre o tipo. Un único punto de cambio para ambos temas.
        /// </summary>
        void AplicarTema()
        {
            if (_dark)
            {
                _bg   = Color.FromArgb( 12,  18,  45);
                _card = Color.FromArgb( 20,  30,  64);
                _txt  = Color.FromArgb(248, 250, 252);
                _mut  = Color.FromArgb(148, 163, 184);
                _inp  = Color.FromArgb( 12,  18,  45);
            }
            else
            {
                _bg   = Color.FromArgb(238, 242, 255);
                _card = Color.White;
                _txt  = Color.FromArgb( 15,  23,  42);
                _mut  = Color.FromArgb( 71,  85, 105);
                _inp  = Color.White;
            }

            BackColor = _bg;
            ForeColor = _txt;
            btnTheme.Text      = _dark ? "Modo Claro" : "Modo Oscuro";
            btnTheme.BackColor = _dark ? Color.FromArgb(40, 55, 100) : Color.FromArgb(200, 210, 240);
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
                    case GroupBox gb:
                        gb.BackColor = _card; gb.ForeColor = _txt; break;
                    case Panel p when p.Name == "pnlHdr":
                        p.BackColor = _card; p.Invalidate(); break;
                    case Panel p when p.BackColor != Color.Transparent:
                        p.BackColor = _card; break;
                    case TableLayoutPanel t:
                        t.BackColor = Color.Transparent; break;
                    case Button b when b.Name == "btnStart":
                        b.BackColor = Ac; b.ForeColor = Color.White;
                        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(60, 50, 195); break;
                    case Button b when b.Name == "btnStop":
                        b.BackColor = Err; b.ForeColor = Color.White;
                        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(210, 40, 40); break;
                    case Button b when b.Name == "btnTheme": break; // ya manejado arriba
                    case Button b:
                        b.BackColor = Err; b.ForeColor = Color.White;
                        b.FlatAppearance.MouseOverBackColor = Color.FromArgb(210, 40, 40); break;
                    case TextBox tb when tb.Name == "txtPort":
                        tb.BackColor = _inp; tb.ForeColor = _txt; break;
                    case TextBox tb:
                        tb.BackColor = _inp; tb.ForeColor = _txt; break;
                    case ListView lv:
                        lv.BackColor = _inp; lv.ForeColor = _txt; break;
                    case RichTextBox rt:
                        rt.BackColor = _dark ? Color.FromArgb(8, 10, 22) : Color.FromArgb(242, 245, 255);
                        rt.ForeColor = _dark ? Color.FromArgb(215, 215, 225) : Color.FromArgb(40, 50, 70); break;
                    case Label lb when lb.Name == "lblTitle":
                        lb.ForeColor = _txt; break;
                    case Label lb when lb.Name is "lblStatus" or "lblIPs":
                        break;
                    case Label lb: lb.ForeColor = _mut; break;
                    case FlowLayoutPanel fp: fp.BackColor = Color.Transparent; break;
                }
                if (c.Controls.Count > 0) TemaRec(c);
            }
        }

        // ══════════════════════════════════════════════════════════════════════
        //  GROUPBOX REDONDEADO (GDI+)
        // ══════════════════════════════════════════════════════════════════════
        /// <summary>
        /// Pinta el GroupBox con borde redondeado.
        /// (1) FillRectangle en ClientRectangle elimina el borde y título de Windows.
        /// (2) DrawPath dibuja el borde redondeado con radio adaptado al DPI.
        /// (3) DrawString redibuja el título sobre un fondo limpio (FillRectangle previo).
        /// </summary>
        void GbPaint(object? s, PaintEventArgs e)
        {
            if (s is not GroupBox gb) return;
            var g = e.Graphics;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            float sc = DeviceDpi / 96f;
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

        // ── Microinteraccion foco TextBox ──────────────────────────────────────
        void TxtEnter(object? s, EventArgs e) { if (s is TextBox t) t.BackColor = _dark ? Color.FromArgb(28, 42, 90) : Color.FromArgb(220, 228, 255); }
        void TxtLeave(object? s, EventArgs e) { if (s is TextBox t) t.BackColor = _inp; }

        // ── Animación LED de Estado ───────────────────────────────────────────
        void InitLedTimer()
        {
            _ledTimer = new System.Windows.Forms.Timer { Interval = 40 }; // Alta frecuencia para suavidad
            _ledTimer.Tick += (s, e) =>
            {
                if (_server.IsRunning)
                {
                    if (_ledAlphaDown)
                    {
                        _ledAlpha -= 0.04f;
                        if (_ledAlpha <= 0.35f) _ledAlphaDown = false;
                    }
                    else
                    {
                        _ledAlpha += 0.04f;
                        if (_ledAlpha >= 1.0f) _ledAlphaDown = true;
                    }
                    lblStatus.ForeColor = Color.FromArgb((int)(_ledAlpha * 255), Ok.R, Ok.G, Ok.B);
                }
                else
                {
                    lblStatus.ForeColor = Err; // Rojo sólido cuando está detenido
                }
            };
            _ledTimer.Start();
        }

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
        /// Crea un Button con FlatStyle.Flat y BorderSize=0.
        /// TÉCNICA MICROINTERACCION: FlatAppearance.BorderSize=0 elimina el borde de Windows
        /// en modo Flat. Sin esta línea, el botón muestra un borde blanco aunque el fondo sea
        /// el color correcto. MouseOverBackColor se establece al aplicar el tema.
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
            b.FlatAppearance.BorderSize = 0;  // SIN BORDE de Windows en modo Flat
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(back, 0.08f);
            return b;
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
