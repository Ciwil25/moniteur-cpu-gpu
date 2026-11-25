# CLAUDE.md — Moniteur CPU / GPU

Ce fichier est lu automatiquement par Claude Code. Il résume ce qu'il faut savoir pour modifier le projet.
L'utilisateur est souvent francophone et non développeur : répondre en français, simplement, et montrer
une capture avant d'installer un changement visuel.

## Le projet

Application Windows (WinForms, C#) qui affiche CPU / GPU / RAM / FPS sur un petit écran secondaire
(pensé pour 640x480). Un seul exécutable `Moniteur.exe` : les bibliothèques sont intégrées comme ressources.
Créée par Claude (Anthropic) avec Claude Code, à partir des idées de Ciwil25.

## Fichiers

| Fichier | Rôle |
|---|---|
| `src/Moniteur.cs` | `Main` (instance unique, proposition PawnIO), chargement des DLL intégrées (`AssemblyResolve`), classe `Capteurs` (lecture LibreHardwareMonitor + compteurs Windows, RAM, mode `--demo`, mode `--test`) |
| `src/Fenetre.cs` | Fenêtre et dessin : cartes CPU/GPU, gros chiffres, courbes 60 s, ligne du bas (RAM, jeu + FPS, heure avec chiffres qui défilent), alerte, icône de la zone de notification, anti-réduction |
| `src/Fond.cs` | Les fonds d'écran (`Decor`) : palmiers, ville, jetee, voiture, route, neon, aucun — tous dessinés en code |
| `src/Fps.cs` | FPS via `PresentMon.exe` (lancé en arrière-plan, attaché à un Job Windows) + nom du jeu au premier plan + liste des applis ignorées |
| `src/Options.cs` | `Parametres` (`parametres.ini`), `Demarrage` (tâche planifiée), `PawnIO` (téléchargement à la demande), fenêtre `FenetreParametres` |
| `src/admin.manifest` | Demande les droits administrateur (nécessaires aux capteurs) |
| `build/dependances.ps1` | Télécharge LibreHardwareMonitorLib 0.9.6 et ses dépendances (nuget.org) dans `build/deps` |
| `build/compiler.ps1` | Compile `bin/Moniteur.exe` avec `csc.exe` de .NET Framework (fourni avec Windows) |
| `build/paquet.ps1` | Compile + récupère PresentMon signé Intel + crée `dist/Moniteur-CPU-GPU.zip` |

## Compiler et tester

```powershell
.\build\dependances.ps1        # une fois
.\build\compiler.ps1           # -> bin\Moniteur.exe
.\build\paquet.ps1             # -> dist\Moniteur-CPU-GPU.zip
```

- `Moniteur.exe --demo` : valeurs simulées (surchauffe CPU, jeu « Mon jeu »), pratique pour les captures.
- `Moniteur.exe --test` : écrit `capteurs.txt` avec tous les capteurs lus.
- Pour tester sans droits admin, compiler sans `/win32manifest` (la température et la fréquence CPU seront vides : normal).
- Une seule instance tourne à la fois (mutex), sauf en `--demo`.
- Mode fenêtre pour les captures : `parametres.ini` avec `mode = fenetre`, `x`, `y`, `largeur`, `hauteur`.

## Pièges connus

- **C# 5 seulement** : `csc.exe` de .NET Framework ne connaît pas l'interpolation `$"..."`, `?.`, `nameof`, les membres `=>`, etc.
- **Tout est dessiné en 640x480** (`BaseL`, `BaseH`) puis mis à l'échelle d'un bloc dans `OnPaint` : garder les coordonnées dans ce repère.
- Polices en **pixels** (`GraphicsUnit.Pixel`) ; textes alignés sur une ligne de base avec `Morceaux(...)`.
- Les **% CPU et GPU et la fréquence** sont calculés **comme le Gestionnaire des tâches** (`% Processor Utility`,
  moteurs `GPU Engine`, fréquence de base × `% Processor Performance`) ; LibreHardwareMonitor sert de secours.
  Ne pas revenir aux valeurs brutes du pilote : elles ne correspondent pas à ce que voit l'utilisateur.
- Température et fréquence du CPU nécessitent les **droits admin** et le pilote **PawnIO**.
- Pour remplacer un `Moniteur.exe` en cours d'utilisation, il faut d'abord le **quitter** (clic droit sur l'icône → Quitter).
- Le lancement au démarrage est une **tâche planifiée** « Moniteur CPU GPU » ; le chemin de l'exe doit être un vrai
  chemin Windows. Avec l'app de bureau Claude, ce qui est écrit dans `AppData` peut être virtualisé et invisible pour Windows :
  installer l'appli ailleurs (par ex. dans Documents).
- La fenêtre se remet toute seule en place si un jeu plein écran la réduit (`Surveiller`, `WndProc`) : ne pas casser ce comportement.
- `PresentMon.exe` doit être à côté de `Moniteur.exe` ; sans lui, pas de FPS (le reste marche).

## Règles à garder

- **Aucune marque** (nom, logo, image tirée d'un jeu ou d'un produit) dans les fonds ou l'interface : seulement des
  ambiances génériques dessinées en code.
- Palette : violet, magenta, rose, orange ; la couleur de température reste un signal (turquoise < 60 °C < orange < 80 °C < rouge).
- Garder les **crédits** : « créé par Claude (Anthropic) » et Ciwil25 (README, LISEZMOI, fenêtre Paramètres, licence).
- Garder les licences des composants tiers à jour dans `THIRD-PARTY-NOTICES.md` si une dépendance change.
- Ne jamais publier d'infos personnelles (chemins `C:\Users\...`, métadonnées de photos : convertir/nettoyer avant).
