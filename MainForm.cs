using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace SERVIDORES_SOCKETS
{
    /// <summary>
    /// MainForm — Pantalla de bienvenida con hero holográfico, iconos vectoriales
    /// GDI+ (Monitor y Chat) y tarjetas SaaS con efecto de borde iluminado.
    ///
    /// TÉCNICAS PREMIUM GDI+:
    /// • Fondo holográfico: degradado lineal a 45 grados + PathGradientBrush central
    ///   para efecto de "glow" (resplandor radial azul/púrpura).
    /// • Iconos vectoriales: dibujados con precisión matemática mediante GraphicsPath
    ///   y suavizado anti-alias. Evitamos usar letras estáticas en el icono.
    /// • Tarjetas SaaS: cápsulas simuladas con borde dinámico que se enciende en hover.
    /// • Doble buffer nativo y repintado de fondo optimizado.
    /// </summary>
    public class MainForm : Form
    {
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr h, int a, ref int v, int s);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        static readonly Color BgTop     = Color.FromArgb(  9,  11,  30);
        static readonly Color BgBot     = Color.FromArgb( 14,   7,  42);
        static readonly Color CardNorm  = Color.FromArgb( 20,  26,  58);
        static readonly Color CardHov   = Color.FromArgb( 30,  40,  85);
        static readonly Color AccentSrv = Color.FromArgb( 99, 102, 241); // Púrpura índigo
        static readonly Color AccentCli = Color.FromArgb(168,  85, 247); // Violeta brillante
        static readonly Color TextPrim  = Color.FromArgb(248, 250, 252);
        static readonly Color TextMuted = Color.FromArgb(160, 175, 205);

        public MainForm()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint, true);
            Text          = "SocketChat Pro";
            BackColor     = BgTop;
            ForeColor     = TextPrim;
            Size          = new Size(880, 600);
            MinimumSize   = new Size(740, 510);
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
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // 1. Gradiente base
            using (var lb = new LinearGradientBrush(ClientRectangle, BgTop, BgBot, 45f))
            {
                g.FillRectangle(lb, ClientRectangle);
            }

            // 2. Glow holográfico en el centro
            int cx = ClientRectangle.Width / 2;
            int cy = ClientRectangle.Height / 2 - 30;
            using (var path = new GraphicsPath())
            {
                path.AddEllipse(cx - 300, cy - 200, 600, 400);
                using (var pgb = new PathGradientBrush(path))
                {
                    pgb.CenterColor = Color.FromArgb(32, AccentSrv);
                    pgb.SurroundColors = new[] { Color.Transparent };
                    g.FillPath(pgb, path);
                }
            }
        }

        private void BuildUI()
        {
            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1,
                BackColor = Color.Transparent, Margin = Padding.Empty,
                Padding = new Padding(40, 18, 40, 16)
            };
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 38));  // Hero
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 55));  // Cards
            root.RowStyles.Add(new RowStyle(SizeType.Percent,  7));  // Footer
            Controls.Add(root);

            // ── Hero ──────────────────────────────────────────────────────────
            var pnlHero = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            pnlHero.Paint  += PintarHero;
            pnlHero.Resize += (_, _) => pnlHero.Invalidate();
            root.Controls.Add(pnlHero, 0, 0);

            // ── Tarjetas ──────────────────────────────────────────────────────
            var tlpCards = new TableLayoutPanel
            {
                Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1,
                BackColor = Color.Transparent, Margin = Padding.Empty,
                Padding = new Padding(0, 6, 0, 6)
            };
            tlpCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tlpCards.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            tlpCards.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

            var cSrv = BuildCard("Servidor",
                "Inicia el listener TCP en todas las interfaces de red.\nMonitorea y administra las conexiones en tiempo real.",
                AccentSrv, true, () => AbrirForm(true));
            cSrv.Margin = new Padding(0, 0, 12, 0);

            var cCli = BuildCard("Cliente",
                "Conectate a un servidor mediante direccion IP y puerto.\nComparte mensajes de chat y archivos binarios al instante.",
                AccentCli, false, () => AbrirForm(false));
            cCli.Margin = new Padding(12, 0, 0, 0);

            tlpCards.Controls.Add(cSrv, 0, 0);
            tlpCards.Controls.Add(cCli, 1, 0);
            root.Controls.Add(tlpCards, 0, 1);

            // ── Footer ────────────────────────────────────────────────────────
            var pnlFoot = new Panel { Dock = DockStyle.Fill, BackColor = Color.Transparent };
            pnlFoot.Paint += (_, e) =>
            {
                // Línea sutil superior
                Color sep = Color.FromArgb(24, 36, 68);
                e.Graphics.DrawLine(new Pen(sep, 1.2f), 40, 2, pnlFoot.Width - 40, 2);
            };

            var lblFoot = new Label
            {
                Dock = DockStyle.Fill,
                Text = "SocketChat Pro  •  Arquitectura Cliente-Servidor  •  C# WinForms  •  2026",
                TextAlign = ContentAlignment.MiddleCenter,
                ForeColor = Color.FromArgb(62, 78, 120),
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

            // 1. Logo circular con doble anillo holográfico
            int lr = 34;
            var logoR = new Rectangle(cx - lr, cy - lr - 46, lr * 2, lr * 2);

            // Anillo exterior difuminado
            using (var pen = new Pen(Color.FromArgb(40, AccentSrv), 4f))
            {
                g.DrawEllipse(pen, cx - lr - 8, cy - lr - 54, lr * 2 + 16, lr * 2 + 16);
            }
            // Anillo interior fino
            using (var pen = new Pen(Color.FromArgb(120, AccentCli), 1.5f))
            {
                g.DrawEllipse(pen, cx - lr - 3, cy - lr - 49, lr * 2 + 6, lr * 2 + 6);
            }

            // Relleno degradado del círculo central
            using (var lgb = new LinearGradientBrush(logoR, AccentSrv, AccentCli, 135f))
            {
                g.FillEllipse(lgb, logoR);
            }

            // Inicial del logo
            using (var fLogo = new Font("Segoe UI Black", 21F, FontStyle.Bold))
            using (var sfC   = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
            {
                g.DrawString("S", fLogo, Brushes.White, logoR, sfC);
            }

            // 2. Título de la aplicación
            int y = logoR.Bottom + 12;
            using (var f = new Font("Segoe UI", 24F, FontStyle.Bold))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                // Sombra suave del título
                g.DrawString("SocketChat Pro", f, new SolidBrush(Color.FromArgb(10, 10, 20)),
                    new RectangleF(1, y + 1.5f, p.Width, 40), sf);
                g.DrawString("SocketChat Pro", f, new SolidBrush(TextPrim),
                    new RectangleF(0, y, p.Width, 40), sf);
            }

            // 3. Subtítulo
            y += 44;
            using (var f = new Font("Segoe UI Semibold", 9.75F, FontStyle.Bold))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                g.DrawString("Protocolo TCP/IP   |   Diseño Modular   |   Multi-hilos", f,
                    new SolidBrush(Color.FromArgb(115, 135, 185)), new RectangleF(0, y, p.Width, 24), sf);
            }

            // 4. Prompt
            y += 24;
            using (var f = new Font("Segoe UI", 9F))
            using (var sf = new StringFormat { Alignment = StringAlignment.Center })
            {
                g.DrawString("Elige un rol para iniciar la aplicacion", f,
                    new SolidBrush(Color.FromArgb(145, 155, 185)),
                    new RectangleF(0, y, p.Width, 22), sf);
            }
        }

        /// <summary>
        /// Crea una tarjeta interactiva.
        /// En lugar de letras de texto ("S" y "C") como iconos, dibuja iconos vectoriales
        /// reales utilizando GDI+: Monitor de servidor y burbuja de chat de cliente.
        /// </summary>
        private Panel BuildCard(string titulo, string desc, Color accent, bool esServidor, Action onClick)
        {
            var card = new Panel
            {
                Dock = DockStyle.Fill, BackColor = CardNorm, Cursor = Cursors.Hand
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
                bool hov = card.BackColor == CardHov;

                // 1. Dibujar borde iluminado
                using (var pen = new Pen(Color.FromArgb(hov ? 220 : 45, accent), hov ? 2f : 1.2f))
                using (var path = MkRound(new Rectangle(1, 1, card.Width - 2, card.Height - 2), 17))
                {
                    g.DrawPath(pen, path);
                }

                // 2. Dibujar franja brillante superior en hover
                if (hov)
                {
                    var bar = new Rectangle(2, 2, card.Width - 4, 6);
                    using (var lb = new LinearGradientBrush(new Rectangle(0, 0, Math.Max(1, bar.Width), 6),
                        Color.FromArgb(160, accent), Color.Transparent, 90f))
                    {
                        g.FillRectangle(lb, bar);
                    }
                }

                // 3. Dibujar cápsula/botón simulado "Entrar" en la parte inferior
                int btnW = 140;
                int btnH = 32;
                var btnR = new Rectangle(28, card.Height - btnH - 24, btnW, btnH);
                using (var path = MkRound(btnR, 8))
                {
                    if (hov)
                    {
                        using (var br = new SolidBrush(accent))
                        {
                            g.FillPath(br, path);
                        }
                        using (var f = new Font("Segoe UI Semibold", 9F, FontStyle.Bold))
                        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        {
                            g.DrawString("Iniciar modulo", f, Brushes.White, btnR, sf);
                        }
                    }
                    else
                    {
                        using (var pen = new Pen(Color.FromArgb(80, accent), 1.2f))
                        {
                            g.DrawPath(pen, path);
                        }
                        using (var f = new Font("Segoe UI Semibold", 9F, FontStyle.Bold))
                        using (var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center })
                        {
                            g.DrawString("Iniciar modulo", f, new SolidBrush(Color.FromArgb(170, 185, 230)), btnR, sf);
                        }
                    }
                }
            };

            // ── ICONO VECTORIAL GDI+ ──────────────────────────────────────────
            var ico = new Panel
            {
                Size = new Size(62, 62), Location = new Point(28, 22), BackColor = Color.Transparent
            };
            ico.Paint += (_, e) =>
            {
                var g = e.Graphics;
                g.SmoothingMode = SmoothingMode.AntiAlias;

                // Fondo degradado del icono circular
                using (var lb = new LinearGradientBrush(ico.ClientRectangle, Color.FromArgb(24, 30, 72), Color.FromArgb(36, 45, 105), 135f))
                {
                    g.FillEllipse(lb, new Rectangle(0, 0, 61, 61));
                }
                using (var pen = new Pen(Color.FromArgb(60, accent), 1f))
                {
                    g.DrawEllipse(pen, new Rectangle(0, 0, 61, 61));
                }

                // Dibujar forma vectorial según el rol
                using (var pen = new Pen(accent, 2f))
                {
                    pen.LineJoin = LineJoin.Round;
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;

                    if (esServidor)
                    {
                        // DIBUJAR MONITOR (Servidor)
                        // Pantalla
                        g.DrawRectangle(pen, 17, 18, 28, 18);
                        // Conector soporte
                        g.DrawLine(pen, 31, 36, 31, 41);
                        // Base del pie
                        g.DrawLine(pen, 24, 41, 38, 41);
                        // Línea interna de la pantalla (decorativa)
                        using (var penThin = new Pen(Color.FromArgb(100, accent), 1f))
                        {
                            g.DrawLine(penThin, 19, 31, 43, 31);
                        }
                    }
                    else
                    {
                        // DIBUJAR BURBUJA DE CHAT (Cliente)
                        var chatPath = new GraphicsPath();
                        // Elipse principal de la burbuja
                        chatPath.AddArc(16, 16, 26, 18, 270, 180);
                        chatPath.AddLine(29, 34, 21, 40); // colita del chat
                        chatPath.AddLine(21, 40, 24, 33);
                        chatPath.AddArc(16, 16, 26, 18, 90, 180);
                        chatPath.CloseFigure();

                        g.DrawPath(pen, chatPath);

                        // Puntos internos del chat
                        using (var br = new SolidBrush(Color.FromArgb(180, accent)))
                        {
                            g.FillEllipse(br, 25, 23, 3, 3);
                            g.FillEllipse(br, 30, 23, 3, 3);
                            g.FillEllipse(br, 35, 23, 3, 3);
                        }
                    }
                }
            };

            var lblTit = new Label
            {
                Text = titulo, Font = new Font("Segoe UI", 19F, FontStyle.Bold),
                ForeColor = TextPrim, AutoSize = true, Location = new Point(28, 94),
                BackColor = Color.Transparent
            };

            var lblDesc = new Label
            {
                Text = desc, Font = new Font("Segoe UI", 9F),
                ForeColor = TextMuted, AutoSize = false,
                Size = new Size(270, 56), Location = new Point(28, 138),
                BackColor = Color.Transparent
            };

            card.Controls.AddRange(new Control[] { ico, lblTit, lblDesc });

            // Suscribir hover completo para interactividad dinámica
            SetHover(card,    card, CardNorm, CardHov, onClick);
            SetHover(ico,     card, CardNorm, CardHov, onClick);
            SetHover(lblTit,  card, CardNorm, CardHov, onClick);
            SetHover(lblDesc, card, CardNorm, CardHov, onClick);

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
            int d = Math.Max(2, rad * 2); var p = new GraphicsPath();
            p.AddArc(r.X,         r.Y,          d, d, 180, 90);
            p.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
            p.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
            p.AddArc(r.X,         r.Bottom - d, d, d,  90, 90);
            p.CloseFigure(); return p;
        }
    }
}
