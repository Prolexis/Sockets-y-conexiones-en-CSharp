using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SERVIDORES_SOCKETS
{
    /// <summary>
    /// DoubleBufferedPanel — Panel personalizado con doble búfer por hardware
    /// para evitar cualquier parpadeo de dibujo GDI+ en Windows Forms.
    /// </summary>
    public class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | 
                     ControlStyles.OptimizedDoubleBuffer | 
                     ControlStyles.UserPaint, true);
            UpdateStyles();
        }
    }

    /// <summary>
    /// MainForm — Pantalla de bienvenida estilo SaaS Premium 2026.
    ///
    /// SOLUCIÓN AL PARPADEO (ANTI-FLICKERING):
    /// 1. El timer de animación ahora solo invalida localmente 'pnlHero.Invalidate()'.
    ///    Ya no se invalida la ventana completa del Form ('this.Invalidate()'), eliminando el redibujado
    ///    del fondo general, del layout y de las tarjetas hijas, que era el causante del parpadeo.
    /// 2. Implementación de 'DoubleBufferedPanel': El panel del hero y los iconos circulares
    ///    utilizan este componente especial para renderizar la rotación de anillos por hardware.
    /// 3. Gradiente estático de alto impacto de fondo.
    /// </summary>
    public class MainForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        // Estado del tema y animación
        private bool _isDarkMode = true;
        private System.Windows.Forms.Timer _animTimer = null!;
        private float _rotationAngle = 0f;

        // Controles y paneles clave
        private Button btnTheme = null!;
        private DoubleBufferedPanel pnlHero = null!;

        // Colores de tema dinámicos
        Color _bgTop, _bgBot, _cardNorm, _cardHov, _textPrim, _textMuted, _glowColor;

        public MainForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Text          = "SocketChat Pro";
            Size          = new Size(940, 660);
            MinimumSize   = new Size(880, 620);
            StartPosition = FormStartPosition.CenterScreen;
            Font          = new Font("Segoe UI", 9.75F);

            InitColors();
            BuildUI();
            InitAnimation();

            Load += (_, _) =>
            {
                int d = _isDarkMode ? 1 : 0;
                try { DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref d, 4); } catch { }
            };

            FormClosed += (_, _) => _animTimer.Stop();
        }

        private void InitColors()
        {
            if (_isDarkMode)
            {
                _bgTop     = Color.FromArgb(  9,  11,  30);
                _bgBot     = Color.FromArgb( 14,   7,  42);
                _cardNorm  = Color.FromArgb( 20,  26,  58);
                _cardHov   = Color.FromArgb( 30,  40,  85);
                _textPrim  = Color.FromArgb(248, 250, 252);
                _textMuted = Color.FromArgb(160, 175, 205);
                _glowColor = Color.FromArgb(32, 99, 102, 241);
            }
            else
            {
                _bgTop     = Color.FromArgb(240, 244, 252);
                _bgBot     = Color.FromArgb(224, 230, 245);
                _cardNorm  = Color.White;
                _cardHov   = Color.FromArgb(215, 222, 245);
                _textPrim  = Color.FromArgb( 15,  23,  42);
                _textMuted = Color.FromArgb( 71,  85, 105);
                _glowColor = Color.FromArgb(20, 168, 85, 247);
            }
            BackColor = _bgTop;
            ForeColor = _textPrim;
        }

        private void InitAnimation()
        {
            _animTimer = new System.Windows.Forms.Timer { Interval = 16 }; // ~60 FPS para animación fluida
            _animTimer.Tick += (s, e) =>
            {
                _rotationAngle = (_rotationAngle + 2.0f) % 360f;

                // Forzar el repintado ordenado local del logo (no de toda la ventana)
                pnlHero.Invalidate();
            };
            _animTimer.Start();
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Gradiente base de fondo
            using (var lb = new LinearGradientBrush(ClientRectangle, _bgTop, _bgBot, 45f))
            {
                g.FillRectangle(lb, ClientRectangle);
            }

            // 2. Patrón de puntos discretos (textura tecnológica sutil)
            int dotSpacing = 24;
            Color dotColor = _isDarkMode ? Color.FromArgb(10, 255, 255, 255) : Color.FromArgb(14, 0, 0, 0);
            using (var dotBrush = new SolidBrush(dotColor))
            {
                for (int x = 8; x < ClientRectangle.Width; x += dotSpacing)
                {
                    for (int y = 8; y < ClientRectangle.Height; y += dotSpacing)
                    {
                        g.FillRectangle(dotBrush, x, y, 1.5f, 1.5f);
                    }
                }
            }

            // 3. Glow holográfico central
            int cx = ClientRectangle.Width / 2;
            int cy = ClientRectangle.Height / 2 - 30;
            int rW = 600;
            int rH = 400;

            using (var path = new GraphicsPath())
            {
                path.AddEllipse(cx - rW / 2, cy - rH / 2, rW, rH);
                using (var pgb = new PathGradientBrush(path))
                {
                    pgb.CenterColor = _glowColor;
                    pgb.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(pgb, path);
                }
            }
        }

        private void BuildUI()
        {
            // Botón de tema flotante (esquina superior derecha) - reubicado para margen amplio
            btnTheme = MkBtn(_isDarkMode ? "Modo Claro" : "Modo Oscuro", 
                _isDarkMode ? Color.FromArgb(40, 55, 100) : Color.FromArgb(200, 215, 245), 
                _textPrim, new Size(125, 32));
            btnTheme.Name = "btnTheme";
            btnTheme.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            btnTheme.Location = new Point(ClientSize.Width - 145, 16);
            btnTheme.Click += (_, _) => ToggleTema();
            Controls.Add(btnTheme);
            btnTheme.BringToFront();

            // Reposicionar el botón en Resize
            this.Resize += (_, _) =>
            {
                if (btnTheme != null)
                {
                    btnTheme.Location = new Point(ClientSize.Width - 145, 16);
                }
            };

            // Contenedor Central de tamaño fijo para evitar estiramientos al maximizar
            var pnlCenter = new Panel
            {
                Size = new Size(820, 540),
                BackColor = Color.Transparent,
                Name = "pnlCenter"
            };
            pnlCenter.Location = new Point((ClientSize.Width - pnlCenter.Width) / 2, (ClientSize.Height - pnlCenter.Height) / 2);
            Controls.Add(pnlCenter);

            // Mantener centrado ante resize del Formulario
            this.Resize += (_, _) =>
            {
                pnlCenter.Location = new Point((ClientSize.Width - pnlCenter.Width) / 2, (ClientSize.Height - pnlCenter.Height) / 2);
            };

            // Layout dentro del contenedor central
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
                BackColor = Color.Transparent, Margin = Padding.Empty,
                Padding = new Padding(20, 10, 20, 10)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 32));  // Hero
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 61));  // Cards (tarjetas con alto de ~318px)
            root.RowStyles.Add(new RowStyle(SizeType.Percent,  7));  // Footer
            pnlCenter.Controls.Add(root);

            // ── Hero ──────────────────────────────────────────────────────────
            pnlHero = new DoubleBufferedPanel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Name = "pnlHero" };
            pnlHero.Paint  += PintarHero;
            pnlHero.Resize += (_, _) => pnlHero.Invalidate();
            root.Controls.Add(pnlHero, 0, 0);

            // ── Tarjetas ──────────────────────────────────────────────────────
            var tlpCards = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                BackColor = Color.Transparent, Margin = Padding.Empty,
                Padding = new Padding(0, 4, 0, 4)
            };
            tlpCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tlpCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tlpCards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var cSrv = BuildCard("Servidor",
                "Inicia el listener TCP en todas las interfaces de red.\nMonitorea y administra las conexiones en tiempo real.",
                Color.FromArgb(99, 102, 241), true, () => AbrirForm(true)); // Indigo para Servidor
            cSrv.Margin = new Padding(0, 0, 12, 0);

            var cCli = BuildCard("Cliente",
                "Conectate a un servidor mediante direccion IP y puerto.\nComparte mensajes de chat y archivos binarios al instante.",
                Color.FromArgb(20, 184, 166), false, () => AbrirForm(false)); // Teal complementario para Cliente
            cCli.Margin = new Padding(12, 0, 0, 0);

            tlpCards.Controls.Add(cSrv, 0, 0);
            tlpCards.Controls.Add(cCli, 1, 0);
            root.Controls.Add(tlpCards, 0, 1);

            // ── Footer ────────────────────────────────────────────────────────
            var pnlFoot = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent, Name = "pnlFoot" };
            pnlFoot.Paint += (_, e) =>
            {
                Color sep = _isDarkMode ? Color.FromArgb(24, 36, 68) : Color.FromArgb(210, 218, 235);
                e.Graphics.DrawLine(new Pen(sep, 1.2f), 40, 2, pnlFoot.Width - 40, 2);
            };

            var lblFoot = new Label
            {
                Dock = DockStyle.Fill, Name = "lblFoot",
                Text = "SocketChat Pro  •  Arquitectura Cliente-Servidor  •  C# WinForms  •  2026",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = _isDarkMode ? Color.FromArgb(62, 78, 120) : Color.FromArgb(115, 128, 160),
                Font = new Font("Segoe UI Semibold", 8F, FontStyle.Bold),
                BackColor = Color.Transparent
            };
            pnlFoot.Controls.Add(lblFoot);
            root.Controls.Add(pnlFoot, 0, 2);
        }

        private void PintarHero(object? s, PaintEventArgs e)
        {
            var g  = e.Graphics;
            var p  = (Panel)s!;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int cx = p.Width  / 2;
            int cy = p.Height / 2;

            Color accent1 = Color.FromArgb(99, 102, 241);
            Color accent2 = Color.FromArgb(168, 85, 247);

            // 1. Logo circular
            int lr = 34;
            var logoR = new Rectangle(cx - lr, cy - lr - 46, lr * 2, lr * 2);

            // Anillos holográficos en rotación (direcciones opuestas)
            using (var pen = new Pen(Color.FromArgb(45, accent1), 3f))
            {
                g.DrawArc(pen, cx - lr - 8, cy - lr - 54, lr * 2 + 16, lr * 2 + 16, _rotationAngle, 280f);
            }
            using (var pen = new Pen(Color.FromArgb(110, accent2), 1.5f))
            {
                g.DrawArc(pen, cx - lr - 3, cy - lr - 49, lr * 2 + 6, lr * 2 + 6, -_rotationAngle * 1.5f, 210f);
            }

            using (var lgb = new LinearGradientBrush(logoR, accent1, accent2, 135f))
                g.FillEllipse(lgb, logoR);

            using (var fLogo = new Font("Segoe UI Black", 21F, FontStyle.Bold))
            using (var sfC   = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                g.DrawString("S", fLogo, Brushes.White, logoR, sfC);

            // 2. Título (corrección de altura de 40px a 60px para evitar el truncamiento)
            int y = logoR.Bottom + 12;
            using (var f = new Font("Segoe UI", 25F, FontStyle.Bold))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                if (_isDarkMode) g.DrawString("SocketChat Pro", f, new SolidBrush(Color.FromArgb(10, 10, 20)), new RectangleF(1, y + 1.5f, p.Width, 60), sf);
                g.DrawString("SocketChat Pro", f, new SolidBrush(_textPrim), new RectangleF(0, y, p.Width, 60), sf);
            }

            // 3. Subtítulo (desplazado para dejar respirar al título de 25F)
            y += 56;
            using (var f = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                Color subCol = _isDarkMode ? Color.FromArgb(115, 135, 185) : Color.FromArgb(70, 85, 120);
                g.DrawString("Protocolo TCP/IP   |   Diseño Modular   |   Multi-hilos", f, new SolidBrush(subCol), new RectangleF(0, y, p.Width, 24), sf);
            }

            // 4. Prompt
            y += 24;
            using (var f = new Font("Segoe UI", 9F))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                Color prCol = _isDarkMode ? Color.FromArgb(145, 155, 185) : Color.FromArgb(90, 100, 130);
                g.DrawString("Elige un rol para iniciar la aplicacion", f, new SolidBrush(prCol), new RectangleF(0, y, p.Width, 22), sf);
            }
        }

        private Panel BuildCard(string titulo, string desc, Color accent, bool esServidor, Action onClick)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill, BackColor = _cardNorm, Cursor = Cursors.Hand, Name = "card_" + titulo
            };

            card.Layout += (_, _) =>
            {
                if (card.Width < 8 || card.Height < 8) return;
                card.Region = new Region(MkRound(new Rectangle(0, 0, card.Width, card.Height), 18));
            };

            card.Paint += (_, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;
                bool hov = card.BackColor == _cardHov;

                // 1. Borde de la tarjeta
                using (var pen = new Pen(Color.FromArgb(hov ? 220 : 45, accent), hov ? 2f : 1.2f))
                using (var path = MkRound(new Rectangle(1, 1, card.Width - 2, card.Height - 2), 17))
                {
                    g.DrawPath(pen, path);
                }

                // 2. Franja superior de acento en hover
                if (hov)
                {
                    var bar = new Rectangle(2, 2, card.Width - 4, 6);
                    using (var lb = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, bar.Width), 6), Color.FromArgb(160, accent), Color.Transparent, 90f))
                    {
                        g.FillRectangle(lb, bar);
                    }
                }

                // 3. Dibujar características (check ✓) con feedback en hover
                int yChar = 178;
                string[] chars = esServidor 
                    ? new[] { "✓  Multihilo asíncrono", "✓  Hasta 50 conexiones", "✓  Logs de eventos activos" }
                    : new[] { "✓  Chat privado y grupal", "✓  Transferencia de archivos", "✓  Notificaciones LED de red" };
                
                using (var fChar = new Font("Segoe UI Semibold", 9F, FontStyle.Bold))
                {
                    for (int i = 0; i < chars.Length; i++)
                    {
                        Color charCol = hov ? Color.FromArgb(220, accent) : _textMuted;
                        using (var br = new SolidBrush(charCol))
                        {
                            g.DrawString(chars[i], fChar, br, 28, yChar + (i * 20));
                        }
                    }
                }

                // 4. Botón simulado en la parte inferior
                int btnW = 140;
                int btnH = 32;
                var btnR = new Rectangle(28, card.Height - btnH - 20, btnW, btnH);
                using (var path = MkRound(btnR, 8))
                {
                    if (hov)
                    {
                        using (var br = new SolidBrush(accent)) g.FillPath(br, path);
                        using (var f = new Font("Segoe UI Semibold", 9F, FontStyle.Bold))
                        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            g.DrawString("Iniciar modulo", f, Brushes.White, btnR, sf);
                    }
                    else
                    {
                        Color btnBrd = _isDarkMode ? Color.FromArgb(80, accent) : Color.FromArgb(130, accent);
                        using (var pen = new Pen(btnBrd, 1.2f)) g.DrawPath(pen, path);
                        Color btnTxt = _isDarkMode ? Color.FromArgb(170, 185, 230) : Color.FromArgb(70, 85, 130);
                        using (var f = new Font("Segoe UI Semibold", 9F, FontStyle.Bold))
                        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                            g.DrawString("Iniciar modulo", f, new SolidBrush(btnTxt), btnR, sf);
                    }
                }
            };

            // Icono vectorial premium con doble búfer
            var ico = new DoubleBufferedPanel { Size = new Size(62, 62), Location = new Point(28, 22), BackColor = Color.Transparent };
            ico.Paint += (_, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                Color circleBg = _isDarkMode ? Color.FromArgb(24, 30, 72) : Color.FromArgb(228, 235, 255);
                using (var lb = new LinearGradientBrush(ico.ClientRectangle, circleBg, ControlPaint.LightLight(circleBg), 135f))
                    g.FillEllipse(lb, new Rectangle(0, 0, 61, 61));

                using (var pen = new Pen(Color.FromArgb(60, accent), 1f))
                    g.DrawEllipse(pen, new Rectangle(0, 0, 61, 61));

                using (var pen = new Pen(accent, 2f))
                {
                    pen.LineJoin = LineJoin.Round;
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;

                    if (esServidor)
                    {
                        // Dibujar un rack de servidor de red detallado con bahías y LEDs activos
                        g.DrawRectangle(pen, 16, 18, 30, 26);
                        g.DrawRectangle(pen, 20, 22, 22, 6);
                        g.DrawRectangle(pen, 20, 32, 22, 6);

                        using (var greenBrush = new SolidBrush(Color.FromArgb(16, 185, 129)))
                        using (var yellowBrush = new SolidBrush(Color.FromArgb(245, 158, 11)))
                        {
                            g.FillEllipse(greenBrush, 23, 24, 2.5f, 2.5f);
                            g.FillEllipse(yellowBrush, 23, 34, 2.5f, 2.5f);
                        }

                        using (var penThin = new Pen(Color.FromArgb(120, accent), 1f))
                        {
                            g.DrawLine(penThin, 28, 25, 38, 25);
                            g.DrawLine(penThin, 28, 35, 38, 35);
                        }
                    }
                    else
                    {
                        // Dibujar dos burbujas de diálogo cruzadas (estilo chat moderno)
                        var path1 = new GraphicsPath();
                        path1.AddArc(15, 16, 24, 16, 270, 180);
                        path1.AddLine(27, 32, 19, 37);
                        path1.AddLine(19, 37, 22, 31);
                        path1.AddArc(15, 16, 24, 16, 90, 180);
                        path1.CloseFigure();
                        g.DrawPath(pen, path1);

                        using (var pen2 = new Pen(Color.FromArgb(160, accent), 1.5f))
                        {
                            var path2 = new GraphicsPath();
                            path2.AddArc(26, 24, 20, 13, 270, 180);
                            path2.AddLine(36, 37, 41, 41);
                            path2.AddLine(41, 41, 39, 36);
                            path2.AddArc(26, 24, 20, 13, 90, 180);
                            path2.CloseFigure();
                            g.DrawPath(pen2, path2);
                        }

                        using (var br = new SolidBrush(accent))
                        {
                            g.FillEllipse(br, 21, 23, 2.5f, 2.5f);
                            g.FillEllipse(br, 25, 23, 2.5f, 2.5f);
                            g.FillEllipse(br, 29, 23, 2.5f, 2.5f);
                        }
                    }
                }
            };

            var lblTit = new Label
            {
                Text = titulo, Font = new Font("Segoe UI", 19F, FontStyle.Bold),
                ForeColor = _textPrim, AutoSize = true, Location = new Point(28, 94),
                BackColor = Color.Transparent, Name = "lblTit"
            };

            var lblDesc = new Label
            {
                Text = desc, Font = new Font("Segoe UI", 9F),
                ForeColor = _textMuted, AutoSize = false,
                Size = new Size(330, 42), Location = new Point(28, 132),
                BackColor = Color.Transparent, Name = "lblDesc"
            };

            card.Controls.AddRange(new Control[] { ico, lblTit, lblDesc });

            SetHover(card,    card, onClick);
            SetHover(ico,     card, onClick);
            SetHover(lblTit,  card, onClick);
            SetHover(lblDesc, card, onClick);

            return card;
        }

        private void SetHover(Control src, Panel target, Action onClick)
        {
            src.MouseEnter += (_, _) => { target.BackColor = _cardHov; target.Invalidate(); };
            src.MouseLeave += (_, _) => { target.BackColor = _cardNorm; target.Invalidate(); };
            src.Click      += (_, _) => onClick();
        }

        private void ToggleTema()
        {
            _isDarkMode = !_isDarkMode;
            int d = _isDarkMode ? 1 : 0;
            try { DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref d, 4); } catch { }
            
            InitColors();

            btnTheme.Text      = _isDarkMode ? "Modo Claro" : "Modo Oscuro";
            btnTheme.BackColor = _isDarkMode ? Color.FromArgb(40, 55, 100) : Color.FromArgb(200, 215, 245);
            btnTheme.ForeColor = _textPrim;

            ActualizarControlesTema(this);
            Invalidate(true);
        }

        private void ActualizarControlesTema(Control parent)
        {
            foreach (Control c in parent.Controls)
            {
                if (c is Panel p && p.Name.StartsWith("card_"))
                {
                    p.BackColor = _cardNorm;
                    p.ForeColor = _textPrim;
                    foreach (Control child in p.Controls)
                    {
                        if (child is Label lbl)
                        {
                            if (lbl.Name == "lblTit") lbl.ForeColor = _textPrim;
                            else if (lbl.Name == "lblDesc") lbl.ForeColor = _textMuted;
                        }
                        else if (child is Panel iconPnl)
                        {
                            iconPnl.Invalidate();
                        }
                    }
                }
                else if (c is Label lbl && lbl.Name == "lblFoot")
                {
                    lbl.ForeColor = _isDarkMode ? Color.FromArgb(62, 78, 120) : Color.FromArgb(115, 128, 160);
                }

                if (c.Controls.Count > 0)
                    ActualizarControlesTema(c);
            }
        }

        private void AbrirForm(bool esSrv)
        {
            Form f = esSrv ? (Form)new FormServidor() : new FormCliente();
            f.FormClosed += (_, _) => { if (!IsDisposed) Show(); };
            f.Show();
            Hide();
        }

        private static Button MkBtn(string text, Color back, Color fore, Size size)
        {
            var b = new Button
            {
                Text = text, Size = size, FlatStyle = FlatStyle.Flat,
                BackColor = back, ForeColor = fore, Cursor = Cursors.Hand,
                Font = new Font("Segoe UI Semibold", 9F, FontStyle.Bold),
                Margin = new Padding(2, 0, 0, 0)
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = ControlPaint.Dark(back, 0.08f);
            return b;
        }

        private static GraphicsPath MkRound(Rectangle r, int rad)
        {
            int d = Math.Max(2, rad * 2); var p = new GraphicsPath();
            p.AddArc(r.X,         r.Y,          d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
            p.AddArc(r.X,         r.Bottom - d, d, d,  90, 90);
            p.CloseFigure(); return p;
        }
    }
}
