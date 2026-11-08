# Matrice d’acceptation

Le code livré implémente les fonctions ci-dessous ; **implémenté n’est pas synonyme de validé sur matériel réel**. Les résultats automatisés observés sont dans VALIDATION.md et GitHub Actions. Aucun critère nécessitant Freebox, Wi-Fi, veille physique ou vrai jeu n’est déclaré validé sans essai correspondant.

| Critère | Implémentation et preuve prévue | Validation matérielle restante |
|---|---|---|
| A01 | Publication autonome x64, Inno Setup par utilisateur, installation / application installée / désinstallation validées en CI | Installation interactive Win10/11, conservation données personnelles |
| A02 | Ping réel horodaté et SQLite ; ProbeCheck et loopback Windows CI | Passerelle/Internet Freebox, câble retiré/reconnecté |
| A03 | Test déterministe 100 éligibles / 7 timeouts = 7 %, invalides exclues | Comparaison UI/export sur votre PC |
| A04 | 3 succès requis ; cible sans réponse → non mesurable ; tests dédiés | Cible qui filtre ICMP sur votre connexion |
| A05 | Fenêtre SQLite 15 minutes, tests 0/3/15, couverture complète/incomplète | Session après 15 min de collecte réelle |
| A06 | Catalogue éditable, nom/chemin, mode manuel, antirebond, multi-processus | ARC Raiders réel et anti-cheat |
| A07 | Rapports auto par IP/interface, baseline/en-jeu, compteurs et limites, graphiques | Lecture ergonomique et impression sur PC |
| A08 | JSON structuré, CSV UTF-8 avec unités/UTC, HTML échappé imprimable ; tests | Microsoft Print to PDF |
| A09 | Incidents par cible, preuves, fermeture/hystérésis, notifications temporisées ; tests | Notifications Windows et incidents matériels |
| A10 | WAL/transactions/récupération, annulation, événements réseau/veille, test arrêt forcé CI | Veille physique, changement carte, saturation disque |
| A11 | WPF natif français, trois rubriques, dark mode, chart zoom/survol, UI Automation CI | DPI 100–200 %, clavier et 1080p/1440p |
| A12 | 37 tests xUnit réussis Linux/Windows, tests SQLite/rapports/règles/runtime ; pipeline Windows complet réussi | Compléter les scénarios matériels ci-dessus |

## Fonctions non implémentées / limites explicites

- PDF natif : remplacé par HTML avec graphes imprimable en PDF, conformément au minimum demandé.
- Diagnostic de route, TCP connect, débit d’interface : options avancées non implémentées.
- Identification automatique du serveur de jeu : non implémentée ; toujours indiquée non identifiée. Aucun hook/capture/injection.
- Graphique WPF développé dans le projet plutôt qu’une bibliothèque externe ; ses interactions sont zoom temporel, touches +/− et survol. Pas de sélection libre d’un rectangle.
- Pas de signature Authenticode ni de mécanisme de mises à jour automatiques.
- Détection d’interface par passerelle/index : sur plusieurs cartes ou VPN, ne garantit pas le routage effectif du paquet ; contexte explicitement nommé « observé ».
- Les rapports volumineux sont assemblés en mémoire ; très longues sessions à forte fréquence peuvent dépasser l’objectif mémoire indicatif. Les performances en vrai jeu restent à mesurer.
- Le catalogue ARC Raiders est une valeur initiale à confirmer, pas une détection universelle prouvée.
- Les événements réseau déconnectés et les trous de mesures sont détaillés dans la chronologie, pas agrégés en un chiffre censé mesurer une indisponibilité absolue d’Internet.
