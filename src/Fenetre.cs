// Interface "C" : gros chiffres + courbe d'utilisation sur 60 secondes, theme violet
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Windows.Forms;

class Fenetre : Form
{
    static readonly Color Fond = Color.FromArgb(14, 10, 26);
    static readonly Color Carte = Color.FromArgb(23, 17, 39);
    static readonly Color Bord = Color.FromArgb(44, 34, 72);
    static readonly Color Grille = Color.FromArgb(32, 25, 54);
    static readonly Color Texte = Color.FromArgb(245, 243, 255);
    static readonly Color Discret = Color.FromArgb(139, 128, 176);
    static readonly Color TresDiscret = Color.FromArgb(107, 98, 144);
    static readonly Color Violet = Color.FromArgb(167, 139, 250);
    static readonly Color Fuchsia = Color.FromArgb(232, 121, 249);

    const int Historique = 60;
    readonly Parametres prm = Parametres.Charger();
    float SeuilAlerte { get { return prm.Seuil; } }   // degres : au-dessus, la carte clignote en rouge
    // L'ecran est dessine en 640x480 puis agrandi/reduit d'un bloc a la taille de la fenetre
    const float BaseL = 640, BaseH = 480;
    readonly List<float> histCpu = new List<float>(), histGpu = new List<float>();

    readonly Font fTag, fNom, fGrand, fUnite, fPetit, fHeure, fInfo, fInfoUnite, fInfoEtiq, fPied, fJeu;
    readonly Timer rafraichir = new Timer(), echantillon = new Timer();
    Point? glisse;

    static Font Police(string nom, float px, FontStyle style)
    {
        using (var test = new Font(nom, px, style, GraphicsUnit.Pixel))
            if (test.Name == nom) return new Font(nom, px, style, GraphicsUnit.Pixel);
        return new Font("Segoe UI", px, style, GraphicsUnit.Pixel);
    }

    public Fenetre(int ecran)
    {
        Text = "Moniteur CPU / GPU";
        FormBorderStyle = FormBorderStyle.None;
        BackColor = Fond;
        StartPosition = FormStartPosition.Manual;
        DoubleBuffered = true;
        KeyPreview = true;

        fTag = Police("Segoe UI", 28, FontStyle.Bold);
        fNom = Police("Segoe UI Semibold", 24, FontStyle.Regular);
        fGrand = Police("Segoe UI Semibold", 72, FontStyle.Regular);
        fUnite = Police("Segoe UI Semibold", 30, FontStyle.Regular);
        fPetit = Police("Segoe UI", 16, FontStyle.Regular);
        fInfo = Police("Segoe UI Semibold", 64, FontStyle.Regular);
        fInfoUnite = Police("Segoe UI Semibold", 26, FontStyle.Regular);
        fInfoEtiq = Police("Segoe UI", 18, FontStyle.Regular);
        fHeure = Police("Segoe UI Semibold", 30, FontStyle.Regular);
        fPied = Police("Segoe UI Semibold", 28, FontStyle.Regular);
        fJeu = Police("Segoe UI Semibold", 18, FontStyle.Regular);

        numeroEcran = ecran;
        Appliquer();
        // si un jeu change la resolution ou la disposition des ecrans, on se replace
        Microsoft.Win32.SystemEvents.DisplaySettingsChanged += EcransModifies;
        FormClosed += (s, e) => Microsoft.Win32.SystemEvents.DisplaySettingsChanged -= EcransModifies;

        // Pas de bouton dans la barre des taches : une icone a cote de l'horloge a la place
        ShowInTaskbar = false;
        icone = new NotifyIcon();
        icone.Icon = CreerIcone();
        icone.Text = "Moniteur CPU / GPU";
        var menu = new ContextMenuStrip();
        menu.Items.Add("Afficher / masquer", null, (s, e) => Basculer());
        menu.Items.Add("Paramètres…", null, (s, e) => OuvrirParametres());
        var itemDemarrage = new ToolStripMenuItem("Lancer au démarrage de Windows");
        itemDemarrage.Click += (s, e) =>
        {
            bool ok = itemDemarrage.Checked ? Demarrage.Desactiver() : Demarrage.Activer();
            if (!ok) MessageBox.Show("Impossible de modifier le lancement au démarrage.", Text);
        };
        menu.Items.Add(itemDemarrage);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Quitter", null, (s, e) => { quitter = true; Close(); });
        menu.Opening += (s, e) => itemDemarrage.Checked = Demarrage.Actif;
        icone.ContextMenuStrip = menu;
        icone.MouseClick += (s, e) => { if (e.Button == MouseButtons.Left) Basculer(); };
        icone.Visible = true;
        if (!prm.Bienvenue && !Capteurs.Demo)
        {
            icone.ShowBalloonTip(10000, "Moniteur CPU / GPU",
                "Clic droit sur l'icône violette pour les paramètres (écran, taille, alerte) et le lancement au démarrage.", ToolTipIcon.Info);
            prm.Bienvenue = true; prm.Enregistrer();
        }
        FormClosing += (s, e) =>
        {
            // la croix / Alt+F4 / Echap cachent seulement la fenetre ; "Quitter" ferme vraiment
            if (!quitter && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); }
        };
        FormClosed += (s, e) => { icone.Visible = false; icone.Dispose(); };

        KeyDown += (s, e) => { if (e.KeyCode == Keys.Escape) Hide(); };
        DoubleClick += (s, e) => Hide();
        MouseDown += (s, e) => { if (e.Button == MouseButtons.Left) glisse = e.Location; };
        MouseMove += (s, e) => { if (glisse.HasValue) { Left += e.X - glisse.Value.X; Top += e.Y - glisse.Value.Y; } };
        MouseUp += (s, e) =>
        {
            // en mode fenetre, on retient la position apres un deplacement
            if (glisse.HasValue && !place.HasValue) { prm.X = Left; prm.Y = Top; prm.Enregistrer(); }
            glisse = null;
        };
        FormClosed += (s, e) => Capteurs.Fermer();

        rafraichir.Interval = 500;
        rafraichir.Tick += (s, e) =>
        {
            // animation plus fluide pendant une alerte
            Mesures mm = Capteurs.M;
            bool alerte = (mm.CpuTemp ?? 0) >= SeuilAlerte || (mm.GpuTemp ?? 0) >= SeuilAlerte;
            rafraichir.Interval = (alerte || AnimationEnCours) ? 30 : 250;
            if (Visible) Invalidate();
            MajInfobulle();
        };
        rafraichir.Start();

        echantillon.Interval = 1000;
        echantillon.Tick += (s, e) =>
        {
            Ajouter(histCpu, Capteurs.M.CpuCharge);
            Ajouter(histGpu, Capteurs.M.GpuCharge);
            Surveiller();
        };
        echantillon.Start();

        Capteurs.Demarrer();
    }

    readonly NotifyIcon icone;
    bool quitter;
    int numeroEcran;
    Rectangle? place;   // emplacement du petit ecran (null si pas trouve)

    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool ShowWindow(IntPtr h, int cmd);
    [System.Runtime.InteropServices.DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr apres, int x, int y, int cx, int cy, uint flags);
    static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
    const uint SWP_NOSIZE = 0x1, SWP_NOMOVE = 0x2, SWP_NOACTIVATE = 0x10;

    // Plein ecran : l'ecran choisi dans les parametres (ou, en automatique, un ecran 640x480).
    // Mode fenetre ou aucun ecran trouve : place = null, la fenetre garde sa taille et sa position.
    void TrouverPlace()
    {
        Screen[] ecrans = Screen.AllScreens;
        Screen cible = null;
        if (numeroEcran >= 0 && numeroEcran < ecrans.Length) cible = ecrans[numeroEcran];
        else if (prm.PleinEcran)
        {
            if (prm.Ecran != "auto") cible = ecrans.FirstOrDefault(s => s.DeviceName == prm.Ecran);
            if (cible == null) cible = ecrans.FirstOrDefault(s => s.Bounds.Width == 640 && s.Bounds.Height == 480);
        }
        place = cible != null ? (Rectangle?)cible.Bounds : null;
    }

    void Appliquer()
    {
        TrouverPlace();
        if (place.HasValue) { Bounds = place.Value; TopMost = true; }
        else
        {
            TopMost = false;
            var r = new Rectangle(prm.X, prm.Y, prm.Largeur, prm.Hauteur);
            // si la position enregistree n'est plus sur un ecran (ecran debranche), on revient a l'ecran principal
            if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(r)))
                r.Location = new Point(Screen.PrimaryScreen.WorkingArea.X + 100, Screen.PrimaryScreen.WorkingArea.Y + 100);
            Bounds = r;
        }
        Invalidate();
    }

    void OuvrirParametres()
    {
        using (var f = new FenetreParametres(prm))
            if (f.ShowDialog() == DialogResult.OK) { numeroEcran = -1; Appliquer(); if (!Visible) Show(); }
    }

    void EcransModifies(object s, EventArgs e)
    {
        if (InvokeRequired) { BeginInvoke(new Action(() => EcransModifies(s, e))); return; }
        TrouverPlace();
        Surveiller();
    }

    // Toutes les secondes : si Windows (jeu plein ecran, Windows + D...) a reduit ou deplace
    // le moniteur, on le remet a sa place, au premier plan, sans voler le focus du jeu.
    void Surveiller()
    {
        if (!Visible || !place.HasValue) return;   // cache volontairement, ou pas de petit ecran
        if (WindowState == FormWindowState.Minimized) ShowWindow(Handle, 4); // SW_SHOWNOACTIVATE
        if (WindowState != FormWindowState.Normal) WindowState = FormWindowState.Normal;
        if (Bounds != place.Value) Bounds = place.Value;
        SetWindowPos(Handle, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    // Empeche la reduction de la fenetre (Windows + D, Windows + M, jeux plein ecran)
    protected override void WndProc(ref Message m)
    {
        const int WM_SYSCOMMAND = 0x112, SC_MINIMIZE = 0xF020;
        if (m.Msg == WM_SYSCOMMAND && ((int)m.WParam & 0xFFF0) == SC_MINIMIZE) return;
        base.WndProc(ref m);
    }

    void Basculer()
    {
        if (Visible) Hide();
        else { Show(); Activate(); }
    }

    static string Val(float? v, string unite) { return v.HasValue ? v.Value.ToString("0") + unite : "--"; }

    void MajInfobulle()
    {
        Mesures m = Capteurs.M;
        string t = "CPU " + Val(m.CpuTemp, "°C") + " " + Val(m.CpuCharge, "%") + "  |  GPU " + Val(m.GpuTemp, "°C") + " " + Val(m.GpuCharge, "%");
        if (t.Length > 63) t = t.Substring(0, 63);
        if (icone.Text != t) icone.Text = t;
    }

    // Petite icone violette dessinee a la volee (carre arrondi + courbe)
    static Icon CreerIcone()
    {
        using (var bmp = new Bitmap(32, 32))
        {
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var p = Arrondi(new RectangleF(1, 1, 30, 30), 8))
                using (var b = new SolidBrush(Color.FromArgb(124, 58, 237))) g.FillPath(b, p);
                using (var stylo = new Pen(Color.White, 3.2f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
                    g.DrawLines(stylo, new[] { new PointF(6, 21), new PointF(11, 15), new PointF(16, 19), new PointF(21, 9), new PointF(26, 13) });
            }
            return Icon.FromHandle(bmp.GetHicon());
        }
    }

    static void Ajouter(List<float> h, float? v)
    {
        if (!v.HasValue) return;
        h.Add(Math.Max(0, Math.Min(100, v.Value)));
        if (h.Count > Historique) h.RemoveAt(0);
    }

    static Color CouleurTemp(float? t)
    {
        if (!t.HasValue) return Discret;
        if (t < 60) return Color.FromArgb(94, 234, 212);   // turquoise
        if (t < 80) return Color.FromArgb(251, 191, 36);   // orange
        return Color.FromArgb(248, 113, 113);              // rouge
    }

    static GraphicsPath Arrondi(RectangleF r, float rayon)
    {
        float d = rayon * 2;
        var p = new GraphicsPath();
        p.AddArc(r.X, r.Y, d, d, 180, 90);
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90);
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
        p.CloseFigure();
        return p;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
        g.PixelOffsetMode = PixelOffsetMode.HighQuality;

        // mise a l'echelle : on dessine en 640x480, centre, en gardant les proportions
        float echelle = Math.Min(ClientSize.Width / BaseL, ClientSize.Height / BaseH);
        if (echelle <= 0) return;
        g.TranslateTransform((ClientSize.Width - BaseL * echelle) / 2, (ClientSize.Height - BaseH * echelle) / 2);
        g.ScaleTransform(echelle, echelle);
        g.DrawImage(Decor.Image(prm.Fond), 0, 0, BaseL, BaseH);
        // voile sombre en bas pour garder la ligne RAM / FPS / heure bien lisible
        using (var voile = new LinearGradientBrush(new RectangleF(0, BaseH - 60, BaseL, 61), Color.FromArgb(0, Fond), Color.FromArgb(215, Fond), LinearGradientMode.Vertical))
            g.FillRectangle(voile, 0, BaseH - 60, BaseL, 60);

        Mesures m = Capteurs.M;
        float W = BaseL, H = BaseH;
        float marge = 10, pied = 40;
        float hCarte = (H - marge * 3 - pied) / 2;

        string cpuInfo = m.CpuGhz.HasValue ? m.CpuGhz.Value.ToString("0.0") : "--";
        string gpuInfo = m.GpuMemUtilisee.HasValue ? (m.GpuMemUtilisee.Value / 1024f).ToString("0.0") : "--";
        string gpuEtiq = m.GpuMemTotale.HasValue ? "VRAM sur " + Math.Round(m.GpuMemTotale.Value / 1024f) + " Go" : "VRAM";
        DessinerCarte(g, new RectangleF(marge, marge, W - 2 * marge, hCarte), "CPU", m.CpuNom, m.CpuTemp, m.CpuCharge, cpuInfo, "GHz", "fréquence", histCpu, Violet);
        DessinerCarte(g, new RectangleF(marge, marge * 2 + hCarte, W - 2 * marge, hCarte), "GPU", m.GpuNom, m.GpuTemp, m.GpuCharge, gpuInfo, "Go", gpuEtiq, histGpu, Fuchsia);

        // Barre du bas : RAM a gauche, FPS au milieu, heure a droite (alignes sur la meme ligne de base)
        float ligne = H - 12;
        if (m.Erreur != null)
            using (var b = new SolidBrush(TresDiscret))
                g.DrawString(m.Erreur, fPetit, b, new RectangleF(marge + 6, H - pied - 2, W - 150, pied), new StringFormat { LineAlignment = StringAlignment.Center, Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });
        float finRam = marge + 8;
        if (m.Erreur == null && m.RamUtilisee.HasValue)
        {
            // RAM : les chiffres roulent quand ils changent
            float x = marge + 8;
            x = Morceaux(g, x, ligne, false, Tuple.Create("RAM  ", fPetit, Discret));
            x = rouleauRam.Dessiner(g, m.RamUtilisee.Value.ToString("0.0"), fPied, Texte, x, ligne);
            finRam = Morceaux(g, x, ligne, false, Tuple.Create(" / " + Math.Round(m.RamTotale ?? 0) + " Go", fPetit, Discret));
        }

        // Heure : les chiffres roulent a chaque minute
        string heure = HeureAffichee().ToString("HH:mm");
        float lHeure = g.MeasureString(heure, fHeure, 1000, StringFormat.GenericTypographic).Width;
        float xHeure = W - marge - 8 - lHeure;
        rouleauHeure.Dessiner(g, heure, fHeure, Violet, xHeure, ligne);

        // Jeu + FPS au milieu (seulement quand un jeu tourne) ; le nom est raccourci s'il est trop long
        if (m.Fps.HasValue)
        {
            var fmt = StringFormat.GenericTypographic;
            string fps = m.Fps.Value.ToString("0");
            float lFps = g.MeasureString(fps, fPied, 1000, fmt).Width + g.MeasureString(" FPS", fPetit, 1000, fmt).Width;
            float dispo = xHeure - finRam - 40;
            string jeu = m.Jeu;
            float lJeu = 0;
            if (!string.IsNullOrEmpty(jeu))
            {
                string sep = "  ·  ";
                float maxJeu = dispo - lFps - g.MeasureString(sep, fPetit, 1000, fmt).Width;
                if (maxJeu < 40) jeu = null;
                else
                {
                    while (jeu.Length > 1 && g.MeasureString(jeu, fJeu, 1000, fmt).Width > maxJeu) jeu = jeu.Substring(0, jeu.Length - 1).TrimEnd();
                    if (jeu != m.Jeu) jeu += "…";
                    jeu += sep;
                    lJeu = g.MeasureString(jeu.Replace(' ', ' '), fJeu, 1000, fmt).Width;
                }
            }
            float x = (finRam + xHeure) / 2 - (lJeu + lFps) / 2;
            if (jeu != null) x = Morceaux(g, x, ligne, false, Tuple.Create(jeu, fJeu, Color.FromArgb(196, 186, 235)));
            Morceaux(g, x, ligne, false, Tuple.Create(fps, fPied, Texte), Tuple.Create(" FPS", fPetit, Violet));
        }
    }

    readonly Rouleau rouleauRam = new Rouleau(), rouleauHeure = new Rouleau();
    readonly DateTime debut = DateTime.Now;

    // En mode demo, l'heure avance d'une minute toutes les 4 secondes pour voir l'animation
    DateTime HeureAffichee()
    {
        if (!Capteurs.Demo) return DateTime.Now;
        return debut.AddMinutes(Math.Floor((DateTime.Now - debut).TotalSeconds / 4));
    }

    public bool AnimationEnCours { get { return rouleauRam.EnCours || rouleauHeure.EnCours; } }

    static float Ascent(Font f)
    {
        return f.Size * f.FontFamily.GetCellAscent(f.Style) / f.FontFamily.GetEmHeight(f.Style);
    }

    // Dessine plusieurs bouts de texte cote a cote sur une meme ligne de base
    float Morceaux(Graphics g, float x, float ligneBase, bool centrer, params Tuple<string, Font, Color>[] bouts)
    {
        var fmt = StringFormat.GenericTypographic;
        if (centrer)
        {
            float total = 0;
            foreach (var t in bouts) total += g.MeasureString(t.Item1, t.Item2, 1000, fmt).Width;
            x -= total / 2;
        }
        foreach (var t in bouts)
        {
            using (var b = new SolidBrush(t.Item3)) g.DrawString(t.Item1, t.Item2, b, x, ligneBase - Ascent(t.Item2), fmt);
            x += g.MeasureString(t.Item1.Replace(' ', ' '), t.Item2, 1000, fmt).Width;
        }
        return x;
    }

    // Texte dont chaque chiffre "roule" verticalement (facon compteur) quand il change
    class Rouleau
    {
        const double Duree = 550; // ms
        string actuel = "", ancien = "";
        DateTime depart = DateTime.MinValue;

        public bool EnCours { get { return (DateTime.Now - depart).TotalMilliseconds < Duree; } }

        public float Dessiner(Graphics g, string texte, Font f, Color c, float x, float ligneBase)
        {
            if (texte != actuel)
            {
                ancien = actuel.Length == texte.Length ? actuel : texte;  // longueur differente : pas d'animation
                actuel = texte;
                depart = DateTime.Now;
            }
            double t = Math.Min(1, (DateTime.Now - depart).TotalMilliseconds / Duree);
            float p = (float)(1 - Math.Pow(1 - t, 3));   // ralentit a l'arrivee
            var fmt = StringFormat.GenericTypographic;
            float haut = ligneBase - Ascent(f);
            float h = f.GetHeight(g);
            var etat = g.Save();
            using (var b = new SolidBrush(c))
            {
                for (int i = 0; i < actuel.Length; i++)
                {
                    string n = actuel[i].ToString(), o = ancien[i].ToString();
                    float l = g.MeasureString(n == " " ? " " : n, f, 1000, fmt).Width;
                    if (n != o && t < 1 && char.IsDigit(actuel[i]))
                    {
                        g.SetClip(new RectangleF(x - 1, haut + h * 0.12f, l + 2, h * 0.8f));
                        g.DrawString(o, f, b, x, haut - h * p, fmt);         // l'ancien chiffre monte et sort
                        g.DrawString(n, f, b, x, haut + h * (1 - p), fmt);   // le nouveau arrive par le bas
                        g.ResetClip();
                    }
                    else g.DrawString(n, f, b, x, haut, fmt);
                    x += l;
                }
            }
            g.Restore(etat);
            return x;
        }
    }

    void DessinerCarte(Graphics g, RectangleF r, string tag, string nom, float? temp, float? charge, string info, string infoUnite, string infoEtiq, List<float> hist, Color accent)
    {
        bool alerte = temp.HasValue && temp.Value >= SeuilAlerte;
        using (var p = Arrondi(r, 18))
        {
            // cartes legerement transparentes : on devine le fond derriere
            using (var b = new SolidBrush(alerte ? Color.FromArgb(215, 40, 16, 30) : Color.FromArgb(190, Carte))) g.FillPath(b, p);
            if (alerte)
            {
                // contour rouge qui "respire" doucement
                double phase = (Math.Sin(DateTime.Now.TimeOfDay.TotalMilliseconds / 1000.0 * Math.PI * 1.2) + 1) / 2;
                int a = (int)(110 + 145 * phase);
                using (var halo = new Pen(Color.FromArgb(a / 3, 248, 80, 80), 9)) g.DrawPath(halo, p);
                using (var s = new Pen(Color.FromArgb(a, 248, 80, 80), 3)) g.DrawPath(s, p);
            }
            else using (var s = new Pen(Bord, 1)) g.DrawPath(s, p);
        }

        // En-tete : point de couleur, CPU/GPU, nom du composant, "60 dernieres s"
        float xg = r.X + 18, yh = r.Y + 8;
        using (var b = new SolidBrush(accent)) g.FillEllipse(b, xg, yh + 14, 12, 12);
        using (var b = new SolidBrush(Texte)) g.DrawString(tag, fTag, b, xg + 18, yh);
        float lTag = g.MeasureString(tag, fTag).Width;
        using (var b = new SolidBrush(Color.FromArgb(196, 186, 235)))
            g.DrawString(nom, fNom, b, new RectangleF(xg + 18 + lTag, yh + 4, r.Right - (xg + 18 + lTag) - 14, 36), new StringFormat { Trimming = StringTrimming.EllipsisCharacter, FormatFlags = StringFormatFlags.NoWrap });

        // Gros chiffres
        float yChiffres = r.Y + 40;
        GrosChiffre(g, xg, yChiffres, temp.HasValue ? temp.Value.ToString("0") : "--", temp.HasValue ? "°C" : "", CouleurTemp(temp), CouleurTemp(temp));
        // pourcentage centre dans la carte
        string vCharge = charge.HasValue ? charge.Value.ToString("0") : "--", uCharge = charge.HasValue ? "%" : "";
        var fmtT = StringFormat.GenericTypographic;
        float lCharge = g.MeasureString(vCharge, fGrand, 1000, fmtT).Width + 4 + g.MeasureString(uCharge, fUnite, 1000, fmtT).Width;
        GrosChiffre(g, r.X + (r.Width - lCharge) / 2, yChiffres, vCharge, uCharge, Texte, accent);

        // Troisieme info, alignee a droite : valeur + unite, etiquette dessous
        var fmt = StringFormat.GenericTypographic;
        float xd = r.Right - 18;
        float lUnite = g.MeasureString(infoUnite, fInfoUnite, 1000, fmt).Width;
        float lInfo = g.MeasureString(info, fInfo, 1000, fmt).Width;
        float yInfo = yChiffres + 6;
        using (var b = new SolidBrush(accent)) g.DrawString(infoUnite, fInfoUnite, b, xd - lUnite, yInfo + 34, fmt);
        using (var b = new SolidBrush(Texte)) g.DrawString(info, fInfo, b, xd - lUnite - 5 - lInfo, yInfo, fmt);
        using (var b = new SolidBrush(Discret))
            g.DrawString(infoEtiq, fInfoEtiq, b, new RectangleF(r.X, yInfo + 74, xd - r.X + 2, 26), new StringFormat { Alignment = StringAlignment.Far });

        // Courbe d'utilisation
        var zone = new RectangleF(xg, r.Bottom - 58, r.Width - 36, 46);
        DessinerCourbe(g, zone, hist, accent);
    }

    void GrosChiffre(Graphics g, float x, float y, string valeur, string unite, Color couleur, Color couleurUnite)
    {
        var fmt = StringFormat.GenericTypographic;
        using (var b = new SolidBrush(couleur)) g.DrawString(valeur, fGrand, b, x, y, fmt);
        float l = g.MeasureString(valeur, fGrand, 1000, fmt).Width;
        using (var b = new SolidBrush(couleurUnite)) g.DrawString(unite, fUnite, b, x + l + 4, y + 40, fmt);
    }

    void DessinerCourbe(Graphics g, RectangleF z, List<float> hist, Color accent)
    {
        // lignes de repere a 50 % et 100 %
        using (var p = new Pen(Grille, 1) { DashStyle = DashStyle.Dot })
        {
            g.DrawLine(p, z.X, z.Y, z.Right, z.Y);
            g.DrawLine(p, z.X, z.Y + z.Height / 2, z.Right, z.Y + z.Height / 2);
        }
        using (var p = new Pen(Grille, 1)) g.DrawLine(p, z.X, z.Bottom, z.Right, z.Bottom);
        if (hist.Count < 2) return;

        float pas = z.Width / (Historique - 1);
        float x0 = z.Right - (hist.Count - 1) * pas;
        var pts = new PointF[hist.Count];
        for (int i = 0; i < hist.Count; i++)
            pts[i] = new PointF(x0 + i * pas, z.Bottom - z.Height * hist[i] / 100f);

        using (var aire = new GraphicsPath())
        {
            aire.AddLine(pts[0].X, z.Bottom, pts[0].X, pts[0].Y);
            aire.AddLines(pts);
            aire.AddLine(pts[pts.Length - 1].X, pts[pts.Length - 1].Y, pts[pts.Length - 1].X, z.Bottom);
            aire.CloseFigure();
            using (var b = new LinearGradientBrush(new RectangleF(z.X, z.Y - 1, z.Width, z.Height + 2), Color.FromArgb(90, accent), Color.FromArgb(0, accent), LinearGradientMode.Vertical))
                g.FillPath(b, aire);
        }
        using (var p = new Pen(accent, 2.2f) { LineJoin = LineJoin.Round, StartCap = LineCap.Round, EndCap = LineCap.Round })
            g.DrawLines(p, pts);
        var dernier = pts[pts.Length - 1];
        using (var b = new SolidBrush(accent)) g.FillEllipse(b, dernier.X - 4, dernier.Y - 4, 8, 8);
    }
}
