# Méthode et limites

## Ce qui est mesuré

Sondes Echo ICMP via System.Net.NetworkInformation.Ping, charge utile de 32 octets. Valeurs par défaut : 1 seconde par cible, délai 800 ms, 4 cibles, maximum 8 ; chaque cible n’a qu’une sonde en vol. Horodatage UTC au début de la tentative ; RTT retourné par le système Windows (une réponse inférieure à la résolution peut valoir réellement 0 ms). Une absence de RTT reste null. IP effective et famille IPv4/IPv6 sont enregistrées. Pour un nom DNS, préférence IPv4 et repli IPv6 si aucune IPv4 ; une adresse IPv6 explicite est supportée. Il n’y a pas de test automatique de toutes les familles d’un même nom : ajouter des cibles séparées si souhaité.

L’interface est la première interface opérationnelle avec passerelle, ordonnée par index IPv4. Sur PC multi-homé/VPN, ce contexte ne prouve pas l’interface utilisée par chaque paquet : Windows décide du routage, les sockets ne sont pas liées à cette interface. La passerelle n’est jamais une IP Freebox codée en dur. Un changement d’interface ou de résolution est tracé et renouvelle la validation. L’absence de passerelle est traitée hors ligne pour le suivi Internet.

## Formules

- Éligibles : appels Ping ayant retourné Success ou TimedOut. Le timeout est une absence de réponse dans le délai, pas une preuve de perte de trafic du jeu.
- Succès reçus : Success ; échecs : TimedOut. `100 × échecs / éligibles`. Pas de dénominateur → valeur absente.
- Les statuts Unreachable sont conservés séparément et exclus par prudence : le Ping API seul ne permet pas de prouver si l’erreur provient du réseau local ou d’un routeur distant. DNS, Offline, LocalError, Cancelled et appels non lancés sont exclus. Les invalides et leur proportion sont visibles séparément.
- Validation par adresse et contexte d’interface : au moins 3 succès. Avant validation, le taux interprétable est absent ; zéro réponse après 10 sondes éligibles → non mesurable. Les nombres bruts restent exportés. Moins de 20 sondes éligibles → échantillon limité. Ces états ne sont pas un intervalle de confiance statistique.
- RTT : moyenne, médiane (moyenne des deux valeurs centrales si n pair), p95 au rang `ceil(0,95 × n)`, min/max des seuls succès avec RTT.
- Gigue : moyenne de `abs(RTT[i] − RTT[i−1])` entre succès consécutifs de même IP/interface. Échec/erreur coupe la série. Ce n’est ni la gigue RTP ni celle du jeu.

## Fenêtres, alertes, diagnostic

Fenêtre glissante 30 s. Ouverture après 3 échecs consécutifs sur cible validée, ou dépassement de 5 % / RTT p95 100 ms avec au moins 20 sondes et deux évaluations consécutives. Clôture après retour sous les seuils de la fenêtre puis 3 succès. Une seule alerte à l’ouverture, temporisation globale 60 s ; la fin est conservée sans spam. Seuils éditables, sans valeur universelle. L’hystérésis de la fenêtre peut laisser l’incident ouvert environ 30 secondes après le dernier timeout. Incident = événement par cible ; les rapports alignent leurs dates. Une coupure réseau/veille est un événement indépendant, pas une série de paquets imaginaires perdus.

Le tableau de bord analyse une même fenêtre pour toutes les cibles. Le rapport vérifie les fenêtres communes autour des incidents avant une hypothèse multi-destinations ; il ne déduit pas une simultanéité d’agrégats de plusieurs heures. Des anomalies de passerelle rendent le réseau local suspect. Une passerelle saine et plusieurs références perturbées rendent une anomalie au-delà de la passerelle plausible. Cela ne prouve jamais une panne Free, Cloudflare ou du serveur du jeu.

Le repli automatique est facultatif, désactivé par défaut. Si activé : après 60 s et au moins 30 sondes éligibles sans aucun succès, une cible externe peut être remplacée par 208.67.222.222 (OpenDNS), seulement après 3 succès du candidat. Une seule tentative par cible et lancement ; pas de duplication si OpenDNS est déjà actif ; nouveau target ID et événement explicite. Ces sondes de validation sont conservées avec leur propre identité. Pas de repli de passerelle ni de cible personnelle.

## Baseline et interruptions

Baseline `[début−15 min, début)` figée par bornes temporelles SQLite, jamais complétée artificiellement. Le rapport indique l’étendue réellement disponible globale et les fenêtres par adresse/interface ; complète seulement si chaque destination présente possède la couverture et pas de trou supérieur à 3 intervalles. Une coupure peut rendre la baseline incomplète même si son étendue atteint 15 minutes. Les sondes en vol à la frontière sont affectées selon leur début UTC. Pause, veille, DNS et interface hors ligne ne créent aucune perte fictive. Une interruption d’application rend la session incomplète. Les rapports de fin utilisent `[début, fin)` ; un incident peut se poursuivre au-delà de la session.

Ces mesures concernent **nos sondes indépendantes**. Le logiciel n’observe aucun paquet du jeu et affiche « Serveur de jeu non identifié ». Aucun diagnostic TCP/UDP ni contenu privé n’est inféré. Route/pathping, TCP connect et débit interface sont des options non implémentées dans cette version.
