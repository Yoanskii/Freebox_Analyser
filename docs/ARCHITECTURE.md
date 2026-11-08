# Architecture

La composition est explicite dans `MainWindow` : Store → Runtime → ViewModel ; INetworkProvider et IProbeClient sont injectables. Les projets métier ne référencent pas WPF. Les commandes asynchrones et notifications INotifyPropertyChanged séparent l’affichage et la logique ; le code fenêtre gère uniquement le cycle Windows, les dialogues, l’icône et les ponts OS.

Runtime possède un seul worker métier. Tick 200 ms, 8 cibles au maximum, au plus une sonde en vol par cible ; échéances décalées pour éviter une rafale de départ. Une sonde lente n’en déclenche pas une seconde. Les changements et actions UI passent par une file de commandes. Résolution DNS et Ping sont asynchrones avec délai et annulation. Hors ligne : événement distinct et aucun ping synthétique. Les tâches en vol sont terminées ou annulées à la fermeture.

SQLite utilise WAL, délais de contention et transactions de lots toutes les 2 secondes. La perte possible au crash est limitée aux sondes non encore écrites (environ 2 secondes normalement). Tables `probes`, `sessions`, `network_events`, `incidents`, `settings`, `schema_migrations` et index temps/cible. Les objets JSON versionnés via migration 1 évitent des duplications ; les cibles/jeux sont dans settings, les mesures gardent leur identité/IP/interface d’origine. Les sessions utilisent la fenêtre temps et ne dupliquent pas les sondes. BackupDatabase produit une copie cohérente sans copier un WAL incomplet.

Au démarrage, les sessions ouvertes sont marquées incomplètes avec fin au dernier échantillon réellement enregistré ; aucune durée hors application n’est inventée. Les incidents ouverts sont clôturés et marqués après interruption. Les rapports restent reconstructibles hors ligne depuis SQLite.

L’UI interroge en arrière-plan les 15 dernières minutes toutes les secondes. Le graphique est un contrôle WPF dessiné ; décimation par colonne uniquement visuelle. Les exports conservent les échantillons bruts. Aucun rendu navigateur dans l’interface native. Le HTML est seulement un format de rapport imprimable.

Les statistiques d’un rapport sont séparées par cible/IP/interface/protocole. Les diagnostics multi-destinations utilisent une fenêtre commune horodatée ; l’origine exacte n’est jamais attribuée. Les incidents conservent les preuves à leur ouverture, les sondes brutes permettant de vérifier le reste.
