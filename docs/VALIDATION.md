# Résultats observés

Journal initial du développement, 8 novembre 2026. Les mises à jour finales et exécutions Windows sont identifiées dans l’historique Git et GitHub Actions.

- SDK .NET 10.0.403 installé localement dans l’environnement de développement Linux.
- Phase 1 : 7 tests réussis, compilation WPF Windows croisée réussie, zéro avertissement/erreur.
- Phase 2 : 14 tests réussis, compilation solution réussie, zéro avertissement/erreur.
- Phase 3 : 19 tests réussis, compilation solution réussie, zéro avertissement/erreur.
- Lancement ProbeCheck réel sous Linux : 5 tentatives loopback, toutes LocalError, RTT null, 0 sonde éligible. Pas de données synthétiques. Le système interdit SOCK_RAW, ping_group_range = `1 0`, et le fallback .NET ne trouve pas le ping système. Un binaire ping extrait localement confirme le refus de sockets même hors sandbox. Cela ne valide aucune qualité réseau.
- La restauration initiale a refusé une dépendance SQLite vulnérable (NU1903). Microsoft.Data.Sqlite a été mis à jour en 10.0.14 ; aucun avertissement supprimé.

Voir les résultats finaux ajoutés après les tests et le lien exact de l’exécution Actions. Les cases manuelles de TESTING.md restent à exécuter sur un PC Windows relié à la Freebox.
