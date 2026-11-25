// Parametres (parametres.ini), lancement au demarrage, pilote PawnIO et fenetre "Parametres"
using System;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;

class Parametres
{
    public string Ecran = "auto";        // "auto" (ecran 640x480) ou nom Windows de l'ecran (\\.\DISPLAY2)
    public bool PleinEcran = true;       // plein ecran sur l'ecran choisi, sinon fenetre
    public int Largeur = 640, Hauteur = 480, X = 100, Y = 100;
    public float Seuil = 85;             // degres : au-dessus, la carte clignote en rouge
    public string Fond = "palmiers";     // decor : palmiers, ville, grille, jetee, aucun
    public bool PawnIORefuse = false;    // ne plus proposer l'installation de PawnIO
    public bool Bienvenue = false;       // message d'accueil deja affiche

    public static string Chemin
    {
        get { return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "parametres.ini"); }
    }

    public static Parametres Charger()
    {
        var p = new Parametres();
        try
        {
            if (!File.Exists(Chemin)) return p;
            foreach (string brut in File.ReadAllLines(Chemin))
            {
                string l = brut.Trim();
                if (l.Length == 0 || l.StartsWith(";") || l.StartsWith("#") || l.StartsWith("[")) continue;
                int i = l.IndexOf('=');
                if (i < 0) continue;
                string k = l.Substring(0, i).Trim().ToLowerInvariant(), v = l.Substring(i + 1).Trim();
                int n; float f;
                switch (k)
                {
                    case "ecran": p.Ecran = v.Length > 0 ? v : "auto"; break;
                    case "mode": p.PleinEcran = !v.Equals("fenetre", StringComparison.OrdinalIgnoreCase); break;
                    case "largeur": if (int.TryParse(v, out n)) p.Largeur = Math.Max(200, n); break;
                    case "hauteur": if (int.TryParse(v, out n)) p.Hauteur = Math.Max(150, n); break;
                    case "x": if (int.TryParse(v, out n)) p.X = n; break;
                    case "y": if (int.TryParse(v, out n)) p.Y = n; break;
                    case "seuil_alerte": if (float.TryParse(v.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out f)) p.Seuil = f; break;
                    case "fond": p.Fond = Array.IndexOf(Decor.Styles, v.ToLowerInvariant()) >= 0 ? v.ToLowerInvariant() : "palmiers"; break;
                    case "pawnio_refuse": p.PawnIORefuse = v == "1" || v.Equals("oui", StringComparison.OrdinalIgnoreCase); break;
                    case "bienvenue": p.Bienvenue = v == "1"; break;
                }
            }
        }
        catch { }
        return p;
    }

    public void Enregistrer()
    {
        try
        {
            File.WriteAllLines(Chemin, new[]
            {
                "; Parametres du Moniteur CPU / GPU (modifiables a la main, ou via clic droit sur l'icone > Parametres)",
                "[affichage]",
                "; ecran = auto (cherche un ecran 640x480) ou le nom Windows de l'ecran, par ex. \\\\.\\DISPLAY2",
                "ecran = " + Ecran,
                "; mode = plein (plein ecran sur l'ecran choisi) ou fenetre",
                "mode = " + (PleinEcran ? "plein" : "fenetre"),
                "; taille et position de la fenetre (mode fenetre)",
                "largeur = " + Largeur,
                "hauteur = " + Hauteur,
                "x = " + X,
                "y = " + Y,
                "; fond = palmiers, ville, grille, jetee ou aucun",
                "fond = " + Fond,
                "[alerte]",
                "seuil_alerte = " + Seuil.ToString(CultureInfo.InvariantCulture),
                "[divers]",
                "pawnio_refuse = " + (PawnIORefuse ? "1" : "0"),
                "bienvenue = " + (Bienvenue ? "1" : "0"),
            });
        }
        catch { }
    }
}

// Lancement au demarrage : tache planifiee "Moniteur CPU GPU" (droits admin, sans demande UAC)
static class Demarrage
{
    const string Nom = "Moniteur CPU GPU";

    static int Schtasks(string args)
    {
        var psi = new ProcessStartInfo("schtasks.exe", args) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        using (var p = Process.Start(psi)) { p.StandardOutput.ReadToEnd(); p.StandardError.ReadToEnd(); p.WaitForExit(); return p.ExitCode; }
    }

    public static bool Actif { get { try { return Schtasks("/Query /TN \"" + Nom + "\"") == 0; } catch { return false; } } }

    public static bool Activer()
    {
        try
        {
            string exe = Application.ExecutablePath;
            string utilisateur = System.Security.Principal.WindowsIdentity.GetCurrent().Name;
            string xml =
"<?xml version=\"1.0\" encoding=\"UTF-16\"?>\r\n" +
"<Task version=\"1.3\" xmlns=\"http://schemas.microsoft.com/windows/2004/02/mit/task\">\r\n" +
"  <Triggers><LogonTrigger><Enabled>true</Enabled><UserId>" + Securiser(utilisateur) + "</UserId><Delay>PT15S</Delay></LogonTrigger></Triggers>\r\n" +
"  <Principals><Principal id=\"Author\"><UserId>" + Securiser(utilisateur) + "</UserId><LogonType>InteractiveToken</LogonType><RunLevel>HighestAvailable</RunLevel></Principal></Principals>\r\n" +
"  <Settings><DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries><StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>" +
"<ExecutionTimeLimit>PT0S</ExecutionTimeLimit><MultipleInstancesPolicy>IgnoreNew</MultipleInstancesPolicy><Enabled>true</Enabled></Settings>\r\n" +
"  <Actions Context=\"Author\"><Exec><Command>\"" + Securiser(exe) + "\"</Command></Exec></Actions>\r\n" +
"</Task>";
            string tmp = Path.Combine(Path.GetTempPath(), "moniteur-tache.xml");
            File.WriteAllText(tmp, xml, System.Text.Encoding.Unicode);
            int r = Schtasks("/Create /TN \"" + Nom + "\" /XML \"" + tmp + "\" /F");
            try { File.Delete(tmp); } catch { }
            return r == 0;
        }
        catch { return false; }
    }

    public static bool Desactiver()
    {
        try { return Schtasks("/Delete /TN \"" + Nom + "\" /F") == 0; } catch { return false; }
    }

    static string Securiser(string s) { return System.Security.SecurityElement.Escape(s); }
}

// PawnIO : petit pilote libre necessaire pour lire la temperature et la frequence du CPU
static class PawnIO
{
    const string Url = "https://github.com/namazso/PawnIO.Setup/releases/latest/download/PawnIO_setup.exe";

    public static bool Installe
    {
        get
        {
            try { using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\PawnIO")) return k != null; }
            catch { return true; }
        }
    }

    // Au premier lancement : propose d'installer PawnIO s'il manque (telechargement officiel)
    public static void Proposer(Parametres p)
    {
        if (p.PawnIORefuse || Installe) return;
        var rep = MessageBox.Show(
            "Pour afficher la température et la fréquence du processeur, le moniteur a besoin du petit pilote gratuit PawnIO.\r\n\r\n" +
            "Le télécharger (environ 3 Mo, depuis sa page officielle GitHub) et l'installer maintenant ?\r\n\r\n" +
            "Sans lui, tout le reste fonctionne (utilisation, GPU, mémoire, FPS).",
            "Moniteur CPU / GPU", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (rep != DialogResult.Yes) { p.PawnIORefuse = true; p.Enregistrer(); return; }
        try
        {
            ServicePointManager.SecurityProtocol = (SecurityProtocolType)3072; // TLS 1.2
            string fichier = Path.Combine(Path.GetTempPath(), "PawnIO_setup.exe");
            Cursor.Current = Cursors.WaitCursor;
            using (var wc = new WebClient()) wc.DownloadFile(Url, fichier);
            Cursor.Current = Cursors.Default;
            using (var pr = Process.Start(new ProcessStartInfo(fichier) { UseShellExecute = true })) pr.WaitForExit();
            try { File.Delete(fichier); } catch { }
            if (!Installe) MessageBox.Show("PawnIO ne semble pas installé. Vous pourrez réessayer en supprimant la ligne pawnio_refuse du fichier parametres.ini.", "Moniteur CPU / GPU");
        }
        catch (Exception ex)
        {
            Cursor.Current = Cursors.Default;
            MessageBox.Show("Impossible de télécharger PawnIO : " + ex.Message + "\r\n\r\nVous pouvez l'installer vous-même depuis github.com/namazso/PawnIO.Setup.", "Moniteur CPU / GPU");
        }
    }
}

// Fenetre "Parametres" (clic droit sur l'icone violette)
class FenetreParametres : Form
{
    readonly ComboBox listeEcrans = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    readonly RadioButton radioPlein = new RadioButton { Text = "Plein écran sur l'écran choisi", AutoSize = true };
    readonly RadioButton radioFenetre = new RadioButton { Text = "Fenêtre de taille fixe :", AutoSize = true };
    readonly NumericUpDown largeur = new NumericUpDown { Minimum = 200, Maximum = 7680, Width = 70 };
    readonly NumericUpDown hauteur = new NumericUpDown { Minimum = 150, Maximum = 4320, Width = 70 };
    readonly NumericUpDown seuil = new NumericUpDown { Minimum = 50, Maximum = 110, Width = 70 };
    readonly CheckBox demarrage = new CheckBox { Text = "Lancer le moniteur au démarrage de Windows", AutoSize = true };
    readonly ComboBox listeFonds = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    readonly PictureBox apercu = new PictureBox { Width = 160, Height = 120, SizeMode = PictureBoxSizeMode.Zoom, BorderStyle = BorderStyle.FixedSingle, Margin = new Padding(12, 0, 0, 0) };
    readonly Screen[] ecrans = Screen.AllScreens;
    readonly Parametres p;

    public FenetreParametres(Parametres p)
    {
        this.p = p;
        Text = "Paramètres du moniteur";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        TopMost = true;
        Font = new Font("Segoe UI", 10);
        AutoSize = true; AutoSizeMode = AutoSizeMode.GrowAndShrink;
        Padding = new Padding(14);

        listeEcrans.Items.Add("Automatique (écran en 640x480)");
        for (int i = 0; i < ecrans.Length; i++)
            listeEcrans.Items.Add("Écran " + (i + 1) + " — " + ecrans[i].Bounds.Width + "x" + ecrans[i].Bounds.Height + (ecrans[i].Primary ? " (principal)" : ""));
        int sel = Array.FindIndex(ecrans, s => s.DeviceName == p.Ecran);
        listeEcrans.SelectedIndex = sel >= 0 ? sel + 1 : 0;
        radioPlein.Checked = p.PleinEcran; radioFenetre.Checked = !p.PleinEcran;
        largeur.Value = Math.Min(largeur.Maximum, Math.Max(largeur.Minimum, p.Largeur));
        hauteur.Value = Math.Min(hauteur.Maximum, Math.Max(hauteur.Minimum, p.Hauteur));
        seuil.Value = (decimal)Math.Min(110, Math.Max(50, p.Seuil));
        demarrage.Checked = Demarrage.Actif;

        var t = new TableLayoutPanel { ColumnCount = 1, AutoSize = true, Dock = DockStyle.Fill };
        t.Controls.Add(new Label { Text = "Écran", AutoSize = true, Font = new Font("Segoe UI Semibold", 10) });
        t.Controls.Add(listeEcrans);
        t.Controls.Add(new Label { Text = "Affichage", AutoSize = true, Font = new Font("Segoe UI Semibold", 10), Margin = new Padding(0, 12, 0, 0) });
        t.Controls.Add(radioPlein);
        var ligneTaille = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        ligneTaille.Controls.Add(radioFenetre); ligneTaille.Controls.Add(largeur);
        ligneTaille.Controls.Add(new Label { Text = "x", AutoSize = true, Margin = new Padding(3, 6, 3, 0) });
        ligneTaille.Controls.Add(hauteur);
        t.Controls.Add(ligneTaille);
        t.Controls.Add(new Label { Text = "L'affichage s'adapte à la taille en gardant ses proportions.", AutoSize = true, ForeColor = Color.Gray });
        t.Controls.Add(new Label { Text = "Fond d'écran", AutoSize = true, Font = new Font("Segoe UI Semibold", 10), Margin = new Padding(0, 12, 0, 0) });
        foreach (string n in Decor.Noms) listeFonds.Items.Add(n);
        listeFonds.SelectedIndex = Math.Max(0, Array.IndexOf(Decor.Styles, p.Fond));
        listeFonds.SelectedIndexChanged += (s, e) => apercu.Image = Decor.Image(Decor.Styles[listeFonds.SelectedIndex]);
        apercu.Image = Decor.Image(Decor.Styles[listeFonds.SelectedIndex]);
        var ligneFond = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        ligneFond.Controls.Add(listeFonds); ligneFond.Controls.Add(apercu);
        t.Controls.Add(ligneFond);
        t.Controls.Add(new Label { Text = "Alerte", AutoSize = true, Font = new Font("Segoe UI Semibold", 10), Margin = new Padding(0, 12, 0, 0) });
        var ligneSeuil = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        ligneSeuil.Controls.Add(new Label { Text = "Carte en rouge au-dessus de", AutoSize = true, Margin = new Padding(0, 6, 3, 0) });
        ligneSeuil.Controls.Add(seuil);
        ligneSeuil.Controls.Add(new Label { Text = "°C", AutoSize = true, Margin = new Padding(3, 6, 0, 0) });
        t.Controls.Add(ligneSeuil);
        t.Controls.Add(new Label { Text = "Démarrage", AutoSize = true, Font = new Font("Segoe UI Semibold", 10), Margin = new Padding(0, 12, 0, 0) });
        t.Controls.Add(demarrage);
        t.Controls.Add(new Label { Text = "Créé par Claude (Anthropic) — github.com/Ciwil25/moniteur-cpu-gpu", AutoSize = true, ForeColor = Color.Gray, Margin = new Padding(0, 14, 0, 0) });
        var boutons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, Margin = new Padding(0, 16, 0, 0) };
        var ok = new Button { Text = "Enregistrer", AutoSize = true, DialogResult = DialogResult.OK };
        var annuler = new Button { Text = "Annuler", AutoSize = true, DialogResult = DialogResult.Cancel };
        boutons.Controls.Add(annuler); boutons.Controls.Add(ok);
        t.Controls.Add(boutons);
        Controls.Add(t);
        AcceptButton = ok; CancelButton = annuler;

        EventHandler maj = (s, e) => { largeur.Enabled = hauteur.Enabled = radioFenetre.Checked; };
        radioFenetre.CheckedChanged += maj; maj(null, null);
        ok.Click += (s, e) => Enregistrer();
    }

    void Enregistrer()
    {
        p.Ecran = listeEcrans.SelectedIndex <= 0 ? "auto" : ecrans[listeEcrans.SelectedIndex - 1].DeviceName;
        p.PleinEcran = radioPlein.Checked;
        p.Largeur = (int)largeur.Value; p.Hauteur = (int)hauteur.Value;
        p.Seuil = (float)seuil.Value;
        p.Fond = Decor.Styles[Math.Max(0, listeFonds.SelectedIndex)];
        if (!p.PleinEcran && p.Ecran != "auto")
        {
            // fenetre : on la place en haut a gauche de l'ecran choisi
            var e = ecrans[listeEcrans.SelectedIndex - 1].WorkingArea;
            p.X = e.X + 20; p.Y = e.Y + 20;
        }
        p.Enregistrer();
        bool actif = Demarrage.Actif;
        if (demarrage.Checked && !actif && !Demarrage.Activer())
            MessageBox.Show("Impossible d'activer le lancement au démarrage.", Text);
        if (!demarrage.Checked && actif) Demarrage.Desactiver();
    }
}
