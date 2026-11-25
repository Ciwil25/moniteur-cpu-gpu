// Moniteur CPU / GPU pour petit ecran 640x480
// Application creee par Claude (Anthropic), avec Claude Code, a partir des idees de Ciwil25.
// Quitter : Echap ou double-clic. Argument optionnel : numero d'ecran (0, 1, 2...)
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;

static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += Resoudre;
        if (args.Length > 0 && args[0] == "--test") return Capteurs.Test();
        Application.EnableVisualStyles();
        int ecran = -1;
        foreach (string a in args)
        {
            if (a == "--demo") Capteurs.Demo = true;
            else int.TryParse(a, out ecran);
        }
        // une seule instance a la fois
        bool nouveau;
        using (var verrou = new Mutex(true, "MoniteurCpuGpu_Instance", out nouveau))
        {
            if (!nouveau && !Capteurs.Demo) return 0;
            if (!Capteurs.Demo) PawnIO.Proposer(Parametres.Charger());
            Application.Run(new Fenetre(ecran));
        }
        return 0;
    }

    // Les bibliotheques sont incluses dans l'exe : on les charge depuis les ressources.
    static readonly Dictionary<string, Assembly> charges = new Dictionary<string, Assembly>();
    static Assembly Resoudre(object s, ResolveEventArgs e)
    {
        string nom = new AssemblyName(e.Name).Name;
        lock (charges)
        {
            Assembly a;
            if (charges.TryGetValue(nom, out a)) return a;
            if (nom == "System.Management")
                a = Assembly.Load("System.Management, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b03f5f7f11d50a3a");
            else
            {
                using (Stream st = Assembly.GetExecutingAssembly().GetManifestResourceStream(nom + ".dll"))
                {
                    if (st == null) return null;
                    byte[] b = new byte[st.Length];
                    st.Read(b, 0, b.Length);
                    a = Assembly.Load(b);
                }
            }
            charges[nom] = a;
            return a;
        }
    }
}

class Mesures
{
    public string CpuNom = "CPU", GpuNom = "GPU", Erreur = null;
    public float? CpuTemp, CpuCharge, GpuTemp, GpuCharge, CpuGhz, GpuMemUtilisee, GpuMemTotale;
    public float? RamUtilisee, RamTotale, Fps;   // RAM en Go
    public string Jeu;                            // nom du jeu au premier plan (null si aucun)
}

static class Capteurs
{
    static object ordi;
    public static readonly Mesures M = new Mesures();
    public static bool Demo;

    [StructLayout(LayoutKind.Sequential)]
    class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX));
        public uint dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX m);

    static System.Diagnostics.PerformanceCounter perfCpu;
    static float baseMhz;
    static bool perfIndispo;

    static float? FrequenceGestionnaire()
    {
        if (perfIndispo) return null;
        try
        {
            if (perfCpu == null)
            {
                using (var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0"))
                    baseMhz = Convert.ToSingle(k.GetValue("~MHz"));
                perfCpu = new System.Diagnostics.PerformanceCounter("Processor Information", "% Processor Performance", "_Total");
                perfCpu.NextValue();   // la premiere lecture vaut toujours 0
                return null;
            }
            float pct = perfCpu.NextValue();
            if (pct <= 0 || baseMhz <= 0) return null;
            return baseMhz * pct / 100f / 1000f;
        }
        catch { perfIndispo = true; return null; }
    }

    // % CPU comme le Gestionnaire des taches ("% Processor Utility", plafonne a 100)
    static System.Diagnostics.PerformanceCounter utilCpu;
    static bool utilIndispo;
    static float? ChargeCpuGestionnaire()
    {
        if (utilIndispo) return null;
        try
        {
            if (utilCpu == null)
            {
                utilCpu = new System.Diagnostics.PerformanceCounter("Processor Information", "% Processor Utility", "_Total");
                utilCpu.NextValue();
                return null;
            }
            return Math.Min(100f, Math.Max(0f, utilCpu.NextValue()));
        }
        catch { utilIndispo = true; return null; }
    }

    // % GPU comme le Gestionnaire des taches : pour chaque moteur du GPU (3D, copie, video...),
    // on additionne l'activite de tous les programmes ; le GPU affiche le moteur le plus occupe.
    static readonly Dictionary<string, System.Diagnostics.CounterSample> echantGpu = new Dictionary<string, System.Diagnostics.CounterSample>();
    static readonly System.Text.RegularExpressions.Regex reMoteur =
        new System.Text.RegularExpressions.Regex(@"luid_(0x[0-9a-f]+_0x[0-9a-f]+)_phys_(\d+)_eng_(\d+)_", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
    static bool gpuIndispo, gpuPret;
    static float? ChargeGpuGestionnaire()
    {
        if (gpuIndispo) return null;
        try
        {
            var data = new System.Diagnostics.PerformanceCounterCategory("GPU Engine").ReadCategory()["Utilization Percentage"];
            var parMoteur = new Dictionary<string, float>();
            var vus = new HashSet<string>();
            foreach (System.Diagnostics.InstanceData inst in data.Values)
            {
                var cur = inst.Sample;
                vus.Add(inst.InstanceName);
                System.Diagnostics.CounterSample prec;
                if (echantGpu.TryGetValue(inst.InstanceName, out prec))
                {
                    var mm = reMoteur.Match(inst.InstanceName);
                    if (mm.Success)
                    {
                        string cle = mm.Groups[1].Value + "|" + mm.Groups[2].Value + "|" + mm.Groups[3].Value;
                        float v = System.Diagnostics.CounterSample.Calculate(prec, cur);
                        float t; parMoteur.TryGetValue(cle, out t);
                        parMoteur[cle] = t + v;
                    }
                }
                echantGpu[inst.InstanceName] = cur;
            }
            // oublie les programmes fermes
            foreach (var k in echantGpu.Keys.Where(k => !vus.Contains(k)).ToList()) echantGpu.Remove(k);
            if (!gpuPret) { gpuPret = true; return null; }
            float max = 0;
            foreach (var v in parMoteur.Values) if (v > max) max = v;
            return Math.Min(100f, max);
        }
        catch { gpuIndispo = true; return null; }
    }

    static void LireRam(Mesures m)
    {
        var s = new MEMORYSTATUSEX();
        if (!GlobalMemoryStatusEx(s)) return;
        const float Go = 1024f * 1024f * 1024f;
        m.RamTotale = s.ullTotalPhys / Go;
        m.RamUtilisee = (s.ullTotalPhys - s.ullAvailPhys) / Go;
    }

    // Valeurs simulees pour la maquette : surchauffe CPU, jeu en cours
    static void RemplirDemo(Mesures m)
    {
        var r = new Random();
        m.CpuNom = "AMD Ryzen 7 9700X"; m.GpuNom = "AMD Radeon RX 9070 XT";
        m.CpuTemp = 88; m.CpuCharge = 70 + r.Next(10); m.CpuGhz = 5.4f;
        m.GpuTemp = 72; m.GpuCharge = 94 + r.Next(5); m.GpuMemUtilisee = 11.8f * 1024; m.GpuMemTotale = 16304;
        m.RamUtilisee = 14f + r.Next(6) / 10f; m.RamTotale = 32; m.Fps = 140 + r.Next(8); m.Jeu = "Mon jeu";
    }

    public static void Demarrer()
    {
        Thread t = new Thread(Boucle);
        t.IsBackground = true;
        t.Start();
    }

    static void Boucle()
    {
        if (Demo) { while (true) { RemplirDemo(M); Thread.Sleep(1000); } }
        bool ok = true;
        try { Ouvrir(); }
        catch (Exception ex) { M.Erreur = "Capteurs indisponibles : " + Racine(ex).Message; ok = false; }
        Fps.Demarrer();
        while (true)
        {
            try { LireRam(M); } catch { }
            try { string jeu; M.Fps = Fps.Lire1s(out jeu); M.Jeu = M.Fps.HasValue ? jeu : null; } catch { M.Fps = null; M.Jeu = null; }
            if (ok)
            {
                try { Lire(M); M.Erreur = null; }
                catch (Exception ex) { M.Erreur = Racine(ex).Message; }
            }
            Thread.Sleep(1000);
        }
    }

    static Exception Racine(Exception ex)
    {
        while (ex.InnerException != null) ex = ex.InnerException;
        return ex;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Ouvrir()
    {
        var c = new LibreHardwareMonitor.Hardware.Computer();
        c.IsCpuEnabled = true;
        c.IsGpuEnabled = true;
        c.Open();
        ordi = c;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void Fermer()
    {
        Fps.Arreter();
        var c = ordi as LibreHardwareMonitor.Hardware.Computer;
        if (c != null) try { c.Close(); } catch { }
    }

    static bool EstGpuIntegre(string n)
    {
        return n.Contains("Radeon(TM) Graphics") || n.EndsWith("Radeon Graphics") || n.Contains("UHD") || n.Contains("Iris");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void Lire(Mesures m)
    {
        var c = (LibreHardwareMonitor.Hardware.Computer)ordi;
        LibreHardwareMonitor.Hardware.IHardware cpu = null, gpu = null, gpuIntegre = null;
        foreach (var h in c.Hardware)
        {
            var t = h.HardwareType;
            if (t == LibreHardwareMonitor.Hardware.HardwareType.Cpu) { if (cpu == null) cpu = h; }
            else if (t == LibreHardwareMonitor.Hardware.HardwareType.GpuAmd || t == LibreHardwareMonitor.Hardware.HardwareType.GpuNvidia || t == LibreHardwareMonitor.Hardware.HardwareType.GpuIntel)
            {
                if (EstGpuIntegre(h.Name)) { if (gpuIntegre == null) gpuIntegre = h; }
                else if (gpu == null) gpu = h;
            }
        }
        if (gpu == null) gpu = gpuIntegre;

        if (cpu != null)
        {
            cpu.Update();
            m.CpuNom = cpu.Name;
            m.CpuTemp = Choisir(cpu, LibreHardwareMonitor.Hardware.SensorType.Temperature, "Core (Tctl/Tdie)", "Tctl", "Tdie", "CPU Package", "Package", "Core");
            m.CpuCharge = ChargeCpuGestionnaire() ?? Choisir(cpu, LibreHardwareMonitor.Hardware.SensorType.Load, "CPU Total", "Total");
            // frequence : meme calcul que le Gestionnaire des taches
            // (frequence de base x "% de performances du processeur")
            m.CpuGhz = FrequenceGestionnaire();
            if (!m.CpuGhz.HasValue)
            {
                // secours : moyenne des coeurs donnee par LibreHardwareMonitor
                var moy = cpu.Sensors.FirstOrDefault(s => s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Clock && s.Name == "Cores (Average)");
                if (moy != null && moy.Value.HasValue && moy.Value.Value > 0) m.CpuGhz = moy.Value.Value / 1000f;
            }
        }
        if (gpu != null)
        {
            gpu.Update();
            m.GpuNom = gpu.Name;
            m.GpuTemp = Choisir(gpu, LibreHardwareMonitor.Hardware.SensorType.Temperature, "GPU Core", "Core", "Edge");
            m.GpuCharge = ChargeGpuGestionnaire() ?? Choisir(gpu, LibreHardwareMonitor.Hardware.SensorType.Load, "GPU Core", "Core", "D3D 3D", "3D");
            // memoire video en Mo (SmallData)
            m.GpuMemUtilisee = Choisir(gpu, LibreHardwareMonitor.Hardware.SensorType.SmallData, "GPU Memory Used", "D3D Dedicated Memory Used", "Memory Used");
            m.GpuMemTotale = Choisir(gpu, LibreHardwareMonitor.Hardware.SensorType.SmallData, "GPU Memory Total", "D3D Dedicated Memory Total", "Memory Total");
        }
    }

    static float? Choisir(LibreHardwareMonitor.Hardware.IHardware h, LibreHardwareMonitor.Hardware.SensorType type, params string[] noms)
    {
        var liste = new List<LibreHardwareMonitor.Hardware.ISensor>();
        var tous = new List<LibreHardwareMonitor.Hardware.ISensor>(h.Sensors);
        foreach (var sub in h.SubHardware) { sub.Update(); tous.AddRange(sub.Sensors); }
        bool estTemp = type == LibreHardwareMonitor.Hardware.SensorType.Temperature;
        foreach (var s in tous)
            if (s.SensorType == type && s.Value.HasValue && (!estTemp || s.Value.Value > 0)) liste.Add(s);
        foreach (string n in noms)
        {
            foreach (var s in liste) if (s.Name == n) return s.Value;
            foreach (var s in liste) if (s.Name.Contains(n)) return s.Value;
        }
        return liste.Count > 0 ? liste[0].Value : null;
    }

    // Mode diagnostic : Moniteur.exe --test  -> ecrit capteurs.txt a cote de l'exe
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static int Test()
    {
        var sb = new StringBuilder();
        try
        {
            Ouvrir();
            var c = (LibreHardwareMonitor.Hardware.Computer)ordi;
            foreach (var h in c.Hardware)
            {
                h.Update();
                sb.AppendLine(h.HardwareType + " : " + h.Name);
                foreach (var s in h.Sensors)
                    if (s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Temperature || s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Load || s.SensorType == LibreHardwareMonitor.Hardware.SensorType.Clock || s.SensorType == LibreHardwareMonitor.Hardware.SensorType.SmallData)
                        sb.AppendLine("   " + s.SensorType + " | " + s.Name + " = " + s.Value);
            }
            for (int i = 0; i < 4; i++)
            {
                Thread.Sleep(1000);
                Lire(M);
                sb.AppendLine("CHOIX -> CPU " + M.CpuTemp + " C / " + M.CpuCharge + " % / " + M.CpuGhz + " GHz   GPU " + M.GpuTemp + " C / " + M.GpuCharge + " %");
            }
            Fermer();
        }
        catch (Exception ex) { sb.AppendLine("ERREUR : " + ex); }
        File.WriteAllText(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "capteurs.txt"), sb.ToString());
        return 0;
    }
}

