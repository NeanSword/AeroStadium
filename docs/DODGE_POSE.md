# Esquives articulées — essai à reprendre

**Retour visuel du 18 octobre : essai trop rigide, rejeté.** Les réussites techniques ci-dessous concernent le timing, les collisions et la restauration ; la qualité du geste reste à refaire. Priorité aux appuis, transferts de poids, contre-mouvements et expressions faciales distincts selon le Pokémon. Aucune nouvelle expression faciale n’est encore intégrée.

Le raté de précision déclenche un geste anticipé : flexion, prise d’appui, impulsion latérale, réception puis retour en garde. La couche de pose articule le squelette natif et conserve l’animation courante. Tortank garde deux appuis et relève les bras ; Bulbizarre et Arcanin répartissent la poussée et la réception sur quatre pattes.

Le déplacement horizontal appartient au directeur d’action. Les pieds suivent des objectifs au sol et en l’air calculés avec les longueurs réelles des membres ; les objectifs hors de portée sont bornés. Le corps tourne et s’incline, la tête continue à regarder l’adversaire. Le geste reste une animation cinématique, pas une simulation physique complète des muscles ou des appuis.

L’ordre d’évaluation est natif80 → esquive90 → placement au sol95 → collision Plante100 → caméra150. Les modifications temporaires du squelette sont retirées avant chaque nouvelle évaluation native. Annulation, désactivation et remplacement restaurent la pose et le placement. Abri et une simple absence de dégât ne déclenchent pas d’esquive.

## Vérification du 18 octobre 2026

Revue de 120.00 s réussie, erreurs0. Six vrais ratés de Tranch’Herbe, sur les deux côtés, contre Tortank9, Bulbizarre1 et Arcanin59 : aucune réaction de dégâts, aucun dégât et aucun croisement entre les pointes des feuilles et les triangles animés. Le dégagement est mesuré 49 à 113 ms avant l’arrivée dans le volume initial immobile. Cette référence de timing est un volume de départ ; elle ne prétend pas calculer le contact exact d’un modèle hypothétique resté immobile.

Les témoins impact direct, Abri et absence d’événement raté passent. Cinq interruptions restaurent la couche articulée, les deux acteurs et la caméra. Les nombres flottants de dégâts sont retirés ; les barres de vie restent inchangées. Leur refonte est prévue pour une étape ultérieure.

Preuves locales : output/attacks/dodge-20261018-v1/verified-summary.json et runtime/demonstration.mp4. Vidéo muette, WAV séparé. Compilation expérimentale WindowsReference réussie et réglages restaurés. Cette revue ne valide pas les151espèces, les morphologies volantes/serpentines, toutes les arènes ni le timing de toutes les attaques. L’orthographique n’est pas couvert par cette nouvelle revue. Croc Feu424 reste à vérifier séparément.
