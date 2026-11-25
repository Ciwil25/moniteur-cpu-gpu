// FPS du jeu au premier plan, via PresentMon (Intel, libre) lance en arriere-plan.
// PresentMon.exe doit etre a cote de Moniteur.exe. Necessite les droits admin.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

static class Fps
{
    static Process pm;
    static IntPtr job;
    static readonly object verrou = new object();
    // horodatages (ms) des images recues, par processus
    static readonly Dictionary<int, Queue<long>> images = new Dictionary<int, Queue<long>>();
    static readonly Stopwatch horloge = Stopwatch.StartNew();
    static readonly int monPid = Process.GetCurrentProcess().Id;
    // Applis qui ne sont pas des jeux : jamais de FPS ni de nom affiche pour elles
    static readonly HashSet<string> ignores = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        // Windows
        "explorer", "dwm", "ShellExperienceHost", "StartMenuExperienceHost", "SearchHost", "TextInputHost", "ApplicationFrameHost", "SystemSettings", "Taskmgr", "LockApp",
        // ce moniteur et Claude
        "Moniteur", "MoniteurTest", "Claude",
        // navigateurs
        "chrome", "msedge", "msedgewebview2", "firefox", "opera", "opera_gx", "brave", "vivaldi", "iexplore", "arc",
        // discussion / musique / video / streaming
        "Discord", "DiscordPTB", "DiscordCanary", "Spotify", "Teams", "ms-teams", "Zoom", "Telegram", "WhatsApp", "Slack",
        "vlc", "mpc-hc64", "mpc-be64", "PotPlayerMini64", "wmplayer", "Microsoft.Media.Player", "obs64", "obs32", "streamlabs",
        // lanceurs de jeux
        "steam", "steamwebhelper", "EADesktop", "EABackgroundService", "Origin", "EpicGamesLauncher", "UbisoftConnect", "upc",
        "Battle.net", "GalaxyClient", "RiotClientServices", "RiotClientUx", "XboxPcApp", "GamingServices", "RadeonSoftware", "AMDRSServ"
    };

    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int GetWindowText(IntPtr h, System.Text.StringBuilder s, int max);

    public static void Demarrer()
    {
        try
        {
            string exe = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "PresentMon.exe");
            if (!File.Exists(exe)) return;
            var psi = new ProcessStartInfo(exe,
                "--output_stdout --no_console_stats --v1_metrics --session_name MoniteurFPS --stop_existing_session --no_track_input --no_track_gpu --no_track_display --exclude dwm.exe")
            {
                UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
            };
            pm = Process.Start(psi);
            AttacherAuJob(pm);
            pm.ErrorDataReceived += (s, e) => { };
            pm.BeginErrorReadLine();
            var t = new Thread(Lire) { IsBackground = true };
            t.Start();
        }
        catch { pm = null; }
    }

    public static void Arreter()
    {
        try { if (pm != null && !pm.HasExited) pm.Kill(); } catch { }
    }

    static void Lire()
    {
        int colApp = -1, colPid = -1;
        string ligne;
        try
        {
            while ((ligne = pm.StandardOutput.ReadLine()) != null)
            {
                string[] c = ligne.Split(',');
                if (colPid < 0)
                {
                    for (int i = 0; i < c.Length; i++)
                    {
                        if (c[i].Equals("ProcessID", StringComparison.OrdinalIgnoreCase)) colPid = i;
                        if (c[i].Equals("Application", StringComparison.OrdinalIgnoreCase)) colApp = i;
                    }
                    continue;
                }
                int pid;
                if (c.Length <= colPid || !int.TryParse(c[colPid], out pid)) continue;
                long maintenant = horloge.ElapsedMilliseconds;
                lock (verrou)
                {
                    Queue<long> q;
                    if (!images.TryGetValue(pid, out q)) { q = new Queue<long>(); images[pid] = q; }
                    q.Enqueue(maintenant);
                    while (q.Count > 0 && maintenant - q.Peek() > 1000) q.Dequeue();
                }
            }
        }
        catch { }
    }

    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);

    // Nombre d'images affichees dans la derniere seconde par l'application au premier plan
    public static float? Lire1s(out string nom)
    {
        nom = null;
        if (pm == null) return null;
        uint pid;
        IntPtr fenetre = GetForegroundWindow();
        GetWindowThreadProcessId(fenetre, out pid);
        if (pid == 0 || pid == monPid) return null;
        string nomProcessus;
        try { nomProcessus = Process.GetProcessById((int)pid).ProcessName; } catch { return null; }
        if (ignores.Contains(nomProcessus)) return null;
        // nom du jeu : titre de la fenetre, sinon nom du programme
        var sb = new System.Text.StringBuilder(256);
        GetWindowText(fenetre, sb, sb.Capacity);
        string titre = sb.ToString().Trim();
        nom = titre.Length > 0 ? titre : nomProcessus;
        long maintenant = horloge.ElapsedMilliseconds;
        lock (verrou)
        {
            // nettoyage des processus inactifs
            var morts = new List<int>();
            foreach (var kv in images)
            {
                while (kv.Value.Count > 0 && maintenant - kv.Value.Peek() > 1000) kv.Value.Dequeue();
                if (kv.Value.Count == 0) morts.Add(kv.Key);
            }
            foreach (int m in morts) images.Remove(m);

            Queue<long> q;
            if (!images.TryGetValue((int)pid, out q) || q.Count < 5) return null;
            return q.Count;
        }
    }

    // --- Job Windows : PresentMon se ferme automatiquement avec le moniteur ---
    [StructLayout(LayoutKind.Sequential)]
    struct BASIC { public long a, b; public uint LimitFlags; public UIntPtr c, d; public uint e; public UIntPtr f; public uint g, h; }
    [StructLayout(LayoutKind.Sequential)]
    struct IOC { public ulong a, b, c, d, e, f; }
    [StructLayout(LayoutKind.Sequential)]
    struct EXTENDED { public BASIC Basic; public IOC Io; public UIntPtr a, b, c, d; }
    [DllImport("kernel32.dll")] static extern IntPtr CreateJobObject(IntPtr a, string nom);
    [DllImport("kernel32.dll")] static extern bool SetInformationJobObject(IntPtr job, int classe, ref EXTENDED info, int taille);
    [DllImport("kernel32.dll")] static extern bool AssignProcessToJobObject(IntPtr job, IntPtr process);

    static void AttacherAuJob(Process p)
    {
        job = CreateJobObject(IntPtr.Zero, null);
        var info = new EXTENDED();
        info.Basic.LimitFlags = 0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
        SetInformationJobObject(job, 9, ref info, Marshal.SizeOf(typeof(EXTENDED)));
        AssignProcessToJobObject(job, p.Handle);
    }
}
