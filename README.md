# Persona 5: The Phantom X - Launcher FR (Windows 11)

Launcher personnalisé pour **Persona 5: The Phantom X (P5X)** sur PC, développé en C# avec **WinUI 3 (Windows App SDK)** et **.NET 8**.

Conçu pour la communauté francophone, il permet de lancer le jeu directement en un clic tout en basculant facilement entre le jeu original (vanilla) et la traduction française via BepInEx / Doorstop, le tout dans une interface moderne au design Windows 11 Fluent (fond Mica, coins arrondis, Snap Layouts).

---

## Fonctionnalités Principales

* **Bascule BepInEx réversible en 1 clic** :
  * Active ou désactive la traduction française sans jamais supprimer aucun fichier.
  * Méthode sécurisée : renommage instantané de `winhttp.dll` en `winhttp.dll.disabled` (et synchronisation de `doorstop_config.ini`).
  * Réversible à tout moment, laissant le dossier du jeu dans un état propre même si le launcher est fermé brutalement.
* **Bouton JOUER & Protection anti-doublon** :
  * Détecte si le jeu est déjà en cours d'exécution pour empêcher les doubles lancements accidentels.
  * Lance le processus avec son répertoire de travail et ses arguments personnalisés.
* **Détection automatique du jeu** :
  * Analyse des bibliothèques Steam multi-disques (via le registre Windows et `libraryfolders.vdf`).
  * Vérification des dossiers d'installation par défaut.
  * Sélecteur de dossier manuel fluide (`FolderPicker`) avec validation de l'exécutable (`P5X.exe`).
* **Design Windows 11 Fluent poussé** :
  * Fond **Mica Alt** (`MicaBackdrop`) natif avec fallback acrylique.
  * Barre de titre personnalisée intégrée (`ExtendsContentIntoTitleBar`), coins arrondis et compatibilité totale avec les **Snap Layouts**.
  * Palette Persona 5 Crimson Red (`#E60012`) et Velvet Gold (`#F5A623`).
  * Thème Sombre / Clair / Système et support bilingue (Français / Anglais).
* **Robustesse & Autonomie** :
  * Exécutable unique **Single-File** autonome (sans MSIX obligatoire, double-clic direct).
  * Mutex d'instance unique (ramène la fenêtre au premier plan si déjà ouverte).
  * Journal d'activité rotatif dans `logs/launcher.log` sans messages d'erreurs bruts pour l'utilisateur.

---

## Compilation et Publication

### Prérequis
* Windows 10 (version 1809+) ou **Windows 11 (build 22000+)**
* SDK .NET 8.0 ou supérieur (`dotnet --version`)

### Compilation en mode Débogage
```powershell
dotnet build LauncherP5XFr.csproj -p:Platform=x64
```

### Publication en exécutable unique précompilé (Single-File + ReadyToRun)
```powershell
dotnet publish LauncherP5XFr.csproj `
  -c Release `
  -r win-x64 `
  --self-contained true `
  -p:PublishSingleFile=true `
  -p:PublishReadyToRun=true `
  -o ./publish
```

Le fichier exécutable produit se trouve dans `./publish/LauncherP5XFr.exe` (~150 Mo, incluant l'ensemble des dépendances et du runtime .NET).

---

## Configuration (`config.json`)

Le fichier `config.json` est situé à côté de l'exécutable :

```json
{
  "gamePath": "D:\\SteamLibrary\\steamapps\\common\\P5X",
  "gameExeName": "P5X.exe",
  "translationEnabled": true,
  "useDoorstopConfigToggle": false,
  "theme": "Default",
  "language": "fr-FR",
  "translationRepo": "VotrePseudo/P5X-Traduction-FR",
  "launcherRepo": "VotrePseudo/LauncherP5XFr",
  "bepInExRepo": "BepInEx/BepInEx",
  "autoInstallBepInEx": true,
  "translationAssetPattern": "(?i)translation.*\\.zip",
  "gitHubToken": "",
  "customArguments": ""
}
```

---

## Gestion des Releases & Architecture Modulaire

### 1. Installation automatique de BepInEx (Premier lancement)
Le launcher s'occupe de tout : vous n'avez pas besoin de fournir BepInEx !
* Au premier lancement, s'il détecte que BepInEx n'est pas encore présent dans le dossier du jeu, il propose de l'installer en **1 clic**.
* Il télécharge la dernière version officielle stable de BepInEx x64 depuis le dépôt officiel (`BepInEx/BepInEx`) et l'extrait dans le jeu avec Doorstop.

### 2. Dépôt de Traduction (`translationRepo`)
Votre release de traduction ne contient **que votre mod de traduction** (très léger, pas besoin d'inclure BepInEx) :
* **Asset attaché** : `translation.zip`
* **Contenu du ZIP** :
  ```text
  translation.zip
  └── BepInEx/
      ├── plugins/       <-- Vos DLLs / scripts de mod
      ├── config/        <-- Fichiers de configuration du mod
      └── Translation/   <-- Vos textes, dialogues et textures FR
  ```

---

## Mentions Légales
* Persona 5: The Phantom X est une marque déposée de SEGA / ATLUS / Perfect World.
* Ce launcher est un projet amateur communautaire, sans affiliation officielle et distribué à des fins d'accessibilité linguistique.
