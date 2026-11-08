# Free Network Monitor

Application de bureau **Windows 10/11 x64**, C# / .NET 10 / WPF, entièrement en français. Analyse locale avant et pendant les sessions de jeu. Dépôt publié sous le nom **Freebox_Analyser**.

> État de validation : voir [docs/VALIDATION.md](docs/VALIDATION.md) et [les exécutions Windows](../../actions). Une compilation réussie ne valide pas votre connexion Freebox ni les performances en jeu.

## Installer et lancer

Dans GitHub → **Actions → Windows — tests et installateur**, ouvrir une exécution réussie et télécharger l’artefact **FreeNetworkMonitor-Windows**. Extraire puis lancer `FreeNetworkMonitor-Setup-1.0.0.exe`. Installation par utilisateur sans administrateur. L’application n’est pas signée Authenticode ; Windows peut demander une confirmation SmartScreen. Le ZIP portable contient aussi `FreeNetworkMonitor.App.exe` et tous ses fichiers nécessaires (ne pas déplacer seulement l’EXE).

Lancer depuis le menu Démarrer. La surveillance débute immédiatement. La passerelle est détectée automatiquement ; les cibles externes par défaut sont 1.1.1.1, 8.8.8.8 et 9.9.9.9. Elles ne sont jamais considérées joignables par hypothèse. Sélectionner une ligne pour voir ses métriques et son graphique ; molette ou touches +/− pour zoomer, survol pour les détails.

**La croix ferme la fenêtre dans la zone de notification.** Menu de l’icône → Ouvrir / Suspendre / Quitter. Le bouton Quitter ferme réellement le moteur et sa base. Désinstaller conserve les mesures ; les supprimer depuis Paramètres si souhaité.

## Sessions de jeu

Paramètres → Ajouter un exécutable : choisir le véritable processus du jeu, pas Steam/Epic/launcher. Nom, processus et chemin sont éditables. ARC Raiders est prérempli avec `PioneerGame-Win64-Shipping`, **à vérifier sur votre installation**. Un chemin renseigné doit correspondre exactement ; si Windows en refuse la lecture, retirer le chemin et utiliser le nom seul ou le mode manuel.

Détection toutes les 2 secondes ; fin après 20 secondes d’absence par défaut. Un redémarrage bref ou plusieurs instances de même jeu conservent la session. Le premier jeu du catalogue gagne la priorité ; un jeu déjà actif la garde. Une seule session reçoit les mesures : pas de double comptage. Arrêter manuellement une session automatique empêche son redémarrage avant la disparition du processus.

Le moteur conserve les 15 minutes réellement disponibles avant le début. Un historique plus court ou troué reste signalé comme incomplet. Les mesures avant jeu restent des mesures indépendantes et sont associées par fenêtre `[début−15 min, début)` ; les mesures pendant jeu utilisent `[début, fin)`.

À la fin : rapport généré localement en HTML, JSON et CSV. Sessions → sélectionner → Ouvrir le rapport. Le HTML contient graphiques avant/pendant et chronologie ; navigateur → Imprimer → Microsoft Print to PDF. Aucun PDF natif n’est promis.

## Compiler sur votre PC Windows

Prérequis : Windows 10/11 x64, SDK .NET 10 (https://dotnet.microsoft.com/download/dotnet/10.0), Git ; Inno Setup 6 pour l’installateur (https://jrsoftware.org/isinfo.php). PowerShell 5.1 ou 7.

```powershell
git clone https://github.com/Yoanskii/Freebox_Analyser.git
cd Freebox_Analyser
dotnet restore FreeNetworkMonitor.sln
dotnet build FreeNetworkMonitor.sln -c Release
dotnet test tests/FreeNetworkMonitor.Tests -c Release
dotnet run --project src/FreeNetworkMonitor.App -c Release
```

```powershell
.\build\publish-win-x64.ps1
.\build\package-windows.ps1 -SkipPublish
# Si Inno Setup est installé ailleurs :
.\build\package-windows.ps1 -SkipPublish -IsccPath 'C:\Outils\Inno Setup 6\ISCC.exe'
```

Résultats : `artifacts/publish/win-x64/`, `artifacts/FreeNetworkMonitor-win-x64.zip`, `artifacts/installer/FreeNetworkMonitor-Setup-1.0.0.exe`. Publication autonome : .NET n’est pas requis sur le PC destinataire. Installer non signé ; signature et certificat de distribution non fournis.

## Vérifier Windows

```powershell
dotnet run --project tools/FreeNetworkMonitor.WindowsSmoke -c Release -- "$PWD\artifacts\publish\win-x64\FreeNetworkMonitor.App.exe" "$PWD\artifacts\windows-smoke"
.\build\test-installer.ps1
# Vérification réelle indépendante, sans aucune donnée simulée :
dotnet run --project tools/FreeNetworkMonitor.ProbeCheck -- 127.0.0.1
```

Le test Windows ouvre réellement WPF, clique les commandes via UI Automation, lance un processus de test sans injection, contrôle les sondes loopback et rapports persistés, provoque un arrêt forcé puis vérifie la récupération. Il utilise un dossier de données isolé. Le loopback **ne prouve pas la qualité Internet**. La checklist Freebox / Ethernet / Wi-Fi / veille / jeu réel reste dans [docs/TESTING.md](docs/TESTING.md).

## Arbre du projet

```text
FreeNetworkMonitor.sln
src/
  FreeNetworkMonitor.App/            WPF, MVVM, thème, graphique, zone Windows
  FreeNetworkMonitor.Core/           modèles, métriques, incidents, transitions
  FreeNetworkMonitor.Network/        Ping ICMP et découverte des interfaces
  FreeNetworkMonitor.GameDetection/  inspection périodique des processus
  FreeNetworkMonitor.Persistence/    SQLite WAL, migrations, récupération
  FreeNetworkMonitor.Reporting/      comparaison et exports CSV / JSON / HTML
  FreeNetworkMonitor.Infrastructure/  orchestration, configuration, journal
 tests/FreeNetworkMonitor.Tests/      tests unitaires et d’intégration
 tools/                              vérifications réelles et smoke Windows
 build/                              PowerShell et installateur Inno Setup
 docs/                               architecture, méthode, tests, acceptation
 .github/workflows/windows.yml        compilation / tests / packaging Windows
```

## Confidentialité et dépannage

Données : `%LOCALAPPDATA%\FreeNetworkMonitor\monitor.db`, `reports/`, `logs/`. Rétention des mesures et sessions : 30 jours par défaut. Les rapports exportés sont conservés séparément jusqu’à suppression explicite ; aucun envoi à un tiers. La purge conserve une baseline qui chevauche la limite pour une session retenue. Paramètres → Exporter toutes les données produit une sauvegarde SQLite cohérente ; Supprimer toutes les données requiert une confirmation et arrête l’application. Les exports personnels hors de ce dossier restent à supprimer par l’utilisateur.

- **Non mesurable** : vérifier la cible, le pare-feu et le protocole. Ne signifie pas panne Internet. Trois succès sont nécessaires à la validation ICMP par adresse/interface.
- **Hors ligne** : vérifier interface/passerelle ; les sondes non lancées n’entrent pas dans les pertes.
- **Pas de jeu détecté** : vérifier le nom réel dans le Gestionnaire des tâches, le chemin et la case Actif ; le mode manuel fonctionne pour tout jeu.
- **Erreur SQLite/espace disque** : consulter `logs/monitor.log`. Les écritures sont réessayées ; après 50 000 sondes en attente le moteur cesse de lancer de nouvelles sondes. Le journal conserve un fichier de 1 Mo et sa rotation.
- **Déjà ouvert** : restaurer depuis l’icône Windows. Une instance par dossier de données.
- Tests isolés : `FreeNetworkMonitor.App.exe --data-dir "C:\Temp\FNM-test"`.

Aucun pilote, service système, hook anti-cheat, capture de paquets, scan de ports ni optimisation réseau automatique. Pas de mesure directe des pertes du jeu. Voir [méthode](docs/MEASUREMENT_METHODOLOGY.md), [limites et acceptation](docs/ACCEPTANCE.md) et [licences](THIRD_PARTY_NOTICES.md).
