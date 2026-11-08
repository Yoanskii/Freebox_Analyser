# Validation de la version candidate Windows

Développement et vérifications du 8 novembre 2026. Version candidate : l’implémentation est livrée, les essais matériels Freebox et jeu réel restent à effectuer.

## Résultats réellement observés

- **37 tests xUnit réussis sur Linux et Windows**, aucun ignoré : statistiques, taux 7 %, exclusions locales/DNS/injoignables, médiane/p95/gigue, incidents et hystérésis, corrélation prudente, baseline 0/3/15 minutes, transitions de sessions, récupération SQLite, purge, migration 1 → 2, sauvegarde, exports et sonde encore en vol à la fin d’une session.
- **Compilation de la solution : zéro erreur, zéro avertissement**, y compris WPF en compilation croisée Linux et sur le runner Windows.
- **Sondes ICMP réelles sur Windows**, vers loopback uniquement. Mesures réellement écrites dans SQLite par l’application. Les zéros milliseconde du loopback viennent de Ping.RoundtripTime, jamais d’un remplissage artificiel. Aucun résultat de qualité Internet n’en est déduit.
- **Interface réellement pilotée par Windows UI Automation** : fenêtre native, trois rubriques, démarrage/arrêt manuel, processus de test configuré détecté automatiquement, temporisation d’arrêt, rapport automatique et cohérence SQLite, baseline courte explicitement incomplète.
- **Arrêt forcé puis relance** : session incomplète récupérée, fenêtre restaurée, fermeture propre.
- **Installateur Inno Setup produit et testé** : installation silencieuse, lancement et répétition des scénarios sur l’exécutable réellement installé, puis désinstallation silencieuse réussie.
- Publication autonome win-x64 : pas de runtime .NET à installer sur le PC destinataire. Exécutable et installateur non signés Authenticode.

Le runner utilisé est Windows Server 2025 Datacenter (image GitHub Actions windows-latest). Cela ne remplace pas les essais matériels sur Windows 10/11.

## Traçabilité

- Code compilé : `9a7476199c8cc61bc2fd5739fda134a2659087f5`.
- [Exécution Windows finale et journaux](https://github.com/Yoanskii/Freebox_Analyser/actions/runs/42524209453).
- [Exécution complète précédente, avec installation et application installée](https://github.com/Yoanskii/Freebox_Analyser/actions/runs/42523907087).
- Les résultats TRX, journaux UI, rapports de sondes loopback et captures réelles se trouvent dans l’artefact `FreeNetworkMonitor-Windows` de l’exécution.
- Les fichiers de livraison sont dans [l’artefact Windows validé](https://github.com/Yoanskii/Freebox_Analyser/actions/runs/42524209453/artifacts/13634946294) ; les SHA-256 sont dans [SHA256SUMS.txt](SHA256SUMS.txt). L’authentification a refusé l’envoi des binaires à uploads.github.com comme pièces jointes de Release. Le téléchargement d’un artefact Actions nécessite une connexion GitHub ; l’application fonctionne sans compte. En cas d’expiration de l’artefact, relancer le workflow Windows.

## Corrections issues des essais

La restauration a initialement détecté une dépendance SQLite vulnérable ; mise à jour en Microsoft.Data.Sqlite 10.0.14, sans suppression d’avertissement. Les essais Windows ont révélé puis permis de corriger une fermeture WPF réentrante, un verrou sur la copie SQLite de sauvegarde, le fond de fenêtre resté clair, des nombres dynamiques dépendant de la langue du runner et une apostrophe dans un script PowerShell. Le pipeline vérifie désormais aussi la syntaxe de tous les scripts de livraison.

Les rapports attendent les sondes lancées avant leur borne de fin, figent les paramètres initiaux de session et gardent le rôle de la cible dans les échantillons. Un changement ultérieur des paramètres ne réécrit pas ces informations historiques.

## Limites et essais restant à réaliser

Les critères A01–A12 sont traités dans [ACCEPTANCE.md](ACCEPTANCE.md), avec séparation entre code, tests automatisés et validation matérielle.

Restent à vérifier sur votre PC : Freebox et références Internet, déconnexion/reconnexion Ethernet, Wi-Fi/IPv6/VPN, veille physique, session ARC Raiders réelle, catalogue d’exécutables effectifs, démarrage Windows, notifications visibles, affichages DPI 100–200 %, CPU/mémoire et impact en jeu. Aucune mesure de performance réelle en jeu ni objectif mémoire atteint n’est affirmé. Checklist : [TESTING.md](TESTING.md).

PDF natif, diagnostic de route, TCP connect, débit interface et identification du serveur du jeu ne sont pas implémentés. Le minimum PDF demandé est couvert par le HTML avec graphiques, imprimable en PDF depuis Windows. Aucun trafic UDP/TCP privé du jeu n’est inspecté.

Sous Linux, les 5 tentatives initiales de sondes réelles ont toutes produit LocalError : sockets ICMP interdites par l’hôte, ping système indisponible, RTT null et 0 sonde éligible. Les tests Windows réels ont ensuite permis de vérifier le moteur, sans simuler de mesures Internet.
