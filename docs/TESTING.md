# Tests et validation sur Windows

## Automatisés

`dotnet test tests/FreeNetworkMonitor.Tests -c Release` sur Linux ou Windows. Les données artificielles sont uniquement dans les tests. Tests de taux 7 %, erreurs exclues, RTT/médiane/p95/gigue, séries vides, validation, incidents/déduplication/hystérésis, baseline 0/3/15 min, priorité/reprise de jeu, récupération après interruption, purge, JSON/CSV/HTML et échappement. Des tests d’intégration pilotent le vrai Runtime avec sondes factices explicitement injectées.

`dotnet run --project tools/FreeNetworkMonitor.ProbeCheck -- 127.0.0.1` produit de vraies sondes et une base temporaire, sans valeur de démonstration. Passer une IP publique en argument pour une cible réelle externe ; un échec n’est pas un résultat Internet extrapolable. Linux peut refuser ICMP sans privilèges et sans ping système : dans ce cas seul un LocalError est vérifiable.

Le pipeline `windows.yml` compile, teste, publie autonome, pilote l’application via UI Automation, mesure le loopback Windows, vérifie le lancement/arrêt manuel et automatique d’un processus configuré, contrôle les rapports et la récupération après arrêt forcé, compile Inno Setup puis teste installation/désinstallation silencieuses. Ses journaux et captures éventuelles sont de vrais artefacts du runner. Ce test n’atteste pas le réseau Freebox ni une session de jeu réelle.

## Checklist matérielle obligatoire (à remplir sur votre PC)

- [ ] Installer puis lancer depuis le menu Démarrer sur Windows 10 x64 et Windows 11 x64.
- [ ] Observer au moins 2 minutes les RTT réels passerelle et Internet ; comparer les sondes brutes et les agrégats.
- [ ] Débrancher Ethernet ou désactiver la carte : hors ligne, événement horodaté, aucune mesure artificielle ; reconnecter.
- [ ] Passer Ethernet → Wi-Fi, changer de passerelle et vérifier les nouvelles identités/interface ; tester IPv6 explicite et nom DNS.
- [ ] Entrer une cible qui ignore ICMP, une adresse DNS inexistante : non mesurable/DNS séparés, pas de panne Internet affirmée.
- [ ] Laisser ouvert 15 minutes puis lancer un jeu ; comparer avec une ouverture depuis seulement 3 minutes.
- [ ] Ajouter un exécutable de test, lancer deux instances, fermer une seule, redémarrer brièvement, vérifier l’antirebond.
- [ ] Essayer deux jeux simultanés et l’arrêt manuel pendant qu’un jeu reste actif.
- [ ] Vérifier rapports : dates locales/UTC, chaque IP/interface, compteurs, unités, graphes, CSV/JSON, impression PDF.
- [ ] Suspendre/reprendre manuellement, mettre Windows en veille/reprendre, fermer brutalement l’app et la relancer.
- [ ] Vérifier notifications activées/désactivées, rétention, démarrage Windows, sauvegarde complète et suppression consentie.
- [ ] Tester la navigation clavier, zoom graphique +/−, fenêtres à 1080p/1440p et DPI 100/150/200 %.
- [ ] Mesurer CPU et mémoire avec un vrai jeu, comparer une session avec/sans surveillance. Objectif <200 Mo non affirmé sans mesure.
- [ ] Désinstaller : programme supprimé, données conservées, entrée démarrage retirée. Réinstaller et retrouver l’historique.

Les performances réseau du runner et les données de votre PC ne doivent pas être rendues publiques sans votre décision. Les tests du pipeline utilisent uniquement loopback.
