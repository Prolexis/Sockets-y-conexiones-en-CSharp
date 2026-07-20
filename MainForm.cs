using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SERVIDORES_SOCKETS
{
    /// <summary>
    /// MainForm — Pantalla de bienvenida con hero GDI+ y tarjetas clickeables.
    ///
    /// TÉCNICAS:
    /// • Hero: Panel.Paint pinta logo, título y subtítulo con GDI+ puro.
    ///   Evita problemas de AutoSize y Layout que surgían con controles Label.
    /// • Tarjetas: Region = new Region(GraphicsPath) para recorte redondeado.
    ///   La Region se actualiza en el evento Layout (se adapta al resize).
    /// • Hover: MouseEnter/Leave cambian BackColor de la tarjeta objetivo (target).
    ///   Se propaga a todos los hijos con SuscribirHover() para no perder el estado.
    /// • Sin coordenadas fijas: TableLayoutPanel + Dock=Fill en todo.
    /// </summary>
    public class MainForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        static readonly Color BgTop     = Color.FromArgb( 10,  14,  40);
        static readonly Color BgBot     = Color.FromArgb( 15,   8,  48);
        static readonly Color CardNorm  = Color.FromArgb( 22,  30,  66);
        static readonly Color CardHov   = Color.FromArgb( 35,  48,  96);
        static readonly Color AccentSrv = Color.FromArgb( 79,  70, 229);
        static readonly Color AccentCli = Color.FromArgb(139,  92, 246);
        static readonly Color TextPrim  = Color.FromArgb(248, 250, 252);
        static readonly Color TextMuted = Color.FromArgb(148, 163, 184);

        public MainForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Text          = "SocketChat Pro";
            BackColor     = BgTop;
            ForeColor     = TextPrim;
            Size          = new Size(880, 600);
            MinimumSize   = new Size(720, 500);
            StartPosition = FormStartPosition.CenterScreen;
            Font          = new Font("Segoe UI", 9.75F);
            BuildUI();
            Load += (_, _) =>
            {
                int d = 1;
                try { DwmSetWindowAttribute(Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref d, 4); } catch { }
            };
        }

        protected override void OnPaintBackground(PaintEventArgs e)
        {
            using var lb = new LinearGradientBrush(ClientRectangle, BgTop, BgBot, 150f);
            e.Graphics.FillRectangle(lb, ClientRectangle);
        }

        private void BuildUI()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
                BackColor = Color.Transparent, Margin = Padding.Empty,
                Padding = new Padding(36, 20, 36, 16)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));  // Hero
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 56));  // Cards
            root.RowStyles.Add(new RowStyle(SizeType.Percent,  6));  // Footer
            Controls.Add(root);

            // ── Hero: GDI+ puro ───────────────────────────────────────────────
            var pnlHero = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
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
                "Inicia el listener TCP en todas las interfaces.\nAdministra clientes conectados en tiempo real.",
                AccentSrv, () => AbrirForm(true));
            cSrv.Margin = new Padding(0, 0, 10, 0);

            var cCli = BuildCard("Cliente",
                "Conectate a un servidor con IP y puerto.\nEnvia mensajes y archivos de forma segura.",
                AccentCli, () => AbrirForm(false));
            cCli.Margin = new Padding(10, 0, 0, 0);

            tlpCards.Controls.Add(cSrv, 0, 0);
            tlpCards.Controls.Add(cCli, 1, 0);
            root.Controls.Add(tlpCards, 0, 1);

            // ── Footer ────────────────────────────────────────────────────────
            root.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                Text = "SocketChat Pro  •  TCP/IP  •  C# WinForms  •  2026",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(55, 75, 115),
                Font = new Font("Segoe UI", 8F),
                BackColor = Color.Transparent
            }, 0, 2);
        }

        /// <summary>
        /// Pinta el hero completo con GDI+: logo circular, título, subtítulo y prompt.
        /// Al usar Paint en vez de controles hijo, evitamos todos los problemas de
        /// AutoSize, Layout y posicionamiento que surgían con Labels dentro de FlowLayoutPanel.
        /// </summary>
        private void PintarHero(object? s, PaintEventArgs e)
        {
            var g  = e.Graphics;
            var p  = (Panel)s!;
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int cx = p.Width  / 2;
            int cy = p.Height / 2;

            // Halo de fondo (efecto glow)
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(cx - 180, cy - 120, 360, 240);
                using var pgb = new PathGradientBrush(path)
                {
                    CenterColor    = Color.FromArgb(25, AccentSrv),
                    SurroundColors = new[] { Color.Transparent }
                };
                g.FillPath(pgb, path);
            }

            // Logo circular
            int lr = 38;
            var logoR = new Rectangle(cx - lr, cy - lr - 60, lr * 2, lr * 2);
            using (var lgb = new LinearGradientBrush(logoR, AccentSrv, AccentCli, 135f))
                g.FillEllipse(lgb, logoR);
            using var fLogo = new Font("Segoe UI Black", 24F, FontStyle.Bold);
            using var sfC   = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString("S", fLogo, Brushes.White, logoR, sfC);

            // Titulo
            int y = logoR.Bottom + 12;
            using (var f = new Font("Segoe UI", 22F, FontStyle.Bold))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
                g.DrawString("SocketChat Pro", f, new SolidBrush(TextPrim),
                    new RectangleF(0, y, p.Width, 38), sf);

            // Subtitulo
            y += 42;
            using (var f = new Font("Segoe UI", 10.5F))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
                g.DrawString("Sistema de comunicacion TCP/IP — C# WinForms", f,
                    new SolidBrush(TextMuted), new RectangleF(0, y, p.Width, 24), sf);

            // Prompt
            y += 28;
            using (var f = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
                g.DrawString("Elige como deseas iniciar la aplicacion:", f,
                    new SolidBrush(Color.FromArgb(150, 170, 215)),
                    new RectangleF(0, y, p.Width, 22), sf);
        }

        /// <summary>
        /// Crea un Panel-tarjeta con:
        ///  • BackColor = CardNorm (se convierte a CardHov en hover)
        ///  • Paint: dibuja solo el borde redondeado con GraphicsPath (sin FillPath)
        ///  • Region: recorta el panel a forma redondeada → actualizado en Layout
        ///  • Hover propagado a todos los hijos con SuscribirHover
        /// </summary>
        private Panel BuildCard(string titulo, string desc, Color accent, Action onClick)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill, BackColor = CardNorm, Cursor = Cursors.Hand
            };

            // Region redondeada: actualizada cada vez que cambia el tamaño
            card.Layout += (_, _) =>
            {
                if (card.Width < 8 || card.Height < 8) return;
                card.Region = new Region(MkRound(new Rectangle(0, 0, card.Width, card.Height), 16));
            };

            // Solo dibujamos el borde; el fondo es el BackColor
            card.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                bool hov = card.BackColor == CardHov;
                using var pen  = new Pen(Color.FromArgb(hov ? 200 : 55, accent), hov ? 2f : 1.2f);
                using var path = MkRound(new Rectangle(1, 1, card.Width - 2, card.Height - 2), 15);
                e.Graphics.DrawPath(pen, path);
                if (hov)
                {
                    // Franja de acento en la parte superior
                    var bar = new Rectangle(2, 2, card.Width - 4, 5);
                    using var lb = new LinearGradientBrush(
                        new Rectangle(0, 0, Math.Max(1, bar.Width), Math.Max(1, bar.Height)),
                        Color.FromArgb(150, accent), Color.Transparent, 90f);
                    e.Graphics.FillRectangle(lb, bar);
                }
            };

            // ── Contenido ─────────────────────────────────────────────────────
            // Icono: circulo con inicial
            var ico = new Panel
            {
                Size = new Size(56, 56), Location = new Point(28, 22), BackColor = Color.Transparent
            };
            ico.Paint += (_, e) =>
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using var lb = new LinearGradientBrush(ico.ClientRectangle, accent,
                    ControlPaint.Light(accent, 0.3f), 135f);
                e.Graphics.FillEllipse(lb, new Rectangle(0, 0, 55, 55));
                using var f  = new Font("Segoe UI Black", 22F, FontStyle.Bold);
                using var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
                e.Graphics.DrawString(titulo[0].ToString(), f, Brushes.White, new RectangleF(0, 0, 55, 55), sf);
            };

            var lblTit = new Label
            {
                Text = titulo, Font = new Font("Segoe UI", 18F, FontStyle.Bold),
                ForeColor = TextPrim, AutoSize = true, Location = new Point(28, 96),
                BackColor = Color.Transparent
            };

            var lblDesc = new Label
            {
                Text = desc, Font = new Font("Segoe UI", 9F),
                ForeColor = TextMuted, AutoSize = false,
                Size = new Size(260, 54), Location = new Point(28, 138),
                BackColor = Color.Transparent
            };

            var lblArrow = new Label
            {
                Text = $"-> Abrir {titulo}",
                Font = new Font("Segoe UI Semibold", 9.5F, FontStyle.Bold),
                ForeColor = accent, AutoSize = true, Location = new Point(28, 205),
                BackColor = Color.Transparent
            };

            card.Controls.AddRange(new Control[] { ico, lblTit, lblDesc, lblArrow });

            // Hover en tarjeta y todos sus hijos
            SetHover(card,   card, CardNorm, CardHov, onClick);
            SetHover(ico,    card, CardNorm, CardHov, onClick);
            SetHover(lblTit, card, CardNorm, CardHov, onClick);
            SetHover(lblDesc,card, CardNorm, CardHov, onClick);
            SetHover(lblArrow,card,CardNorm, CardHov, onClick);
            return card;
        }

        private static void SetHover(Control src, Panel target, Color norm, Color hov, Action onClick)
        {
            src.MouseEnter += (_, _) => { target.BackColor = hov; target.Invalidate(); };
            src.MouseLeave += (_, _) => { target.BackColor = norm; target.Invalidate(); };
            src.Click      += (_, _) => onClick();
        }

        private void AbrirForm(bool esSrv)
        {
            Form f = esSrv ? (Form)new FormServidor() : new FormCliente();
            f.FormClosed += (_, _) => { if (!IsDisposed) Show(); };
            f.Show();
            Hide();
        }

        private static GraphicsPath MkRound(Rectangle r, int rad)
        {
            int d = rad * 2; var p = new GraphicsPath();
            p.AddArc(r.X,         r.Y,          d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
            p.AddArc(r.X,         r.Bottom - d, d, d,  90, 90);
            p.CloseFigure(); return p;
        }
    }
}
