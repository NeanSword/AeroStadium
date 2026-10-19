# Croc Feu — Arcanin

La première mise en scène est limitée à Arcanin59 contre Tortank9, dans l’arène Conseil4 moderne. Le catalogue autorise déjà424 sur Arcanin ; les règles de dégâts et apprentissages restent celles du moteur de combat.

Arcanin utilise sa course native011 pour approcher et revenir. Le segment de mâchoire du mouvement physique005 est joué jusqu’à0,90s, puis repart en course avant la roulade générique de fin. Le déplacement horizontal de l’Origin animé est compensé extérieurement. Les états, temps, poids de couches et vitesse de l’Animator sont restaurés avec les positions.

Le vrai repère EffMouth01 et Jaw sont lus après l’Animator, l’esquive et le grounding, en LateUpdate110. Le contact exige une ouverture puis fermeture observée de la mâchoire et une distance de bouche inférieure à2cm du triangle courant, vérifié après correction. Les rayons réels sont bornés par dichotomie ; les bounds servent à orienter/borner la recherche, jamais de contact de secours. Abri utilise sa propre surface. Il ne s’agit pas d’une collision continue de toutes les dents et du corps.

Un raté conserve sa ligne d’attaque initiale et contrôle les croisements contre les triangles animés. Tortank se dégage avant l’arrivée ; pour424, le retour attend le retrait de l’attaquant. La distance tient compte de sa largeur. Le cas de présentation sans hit et sans Miss est un contrôle sans réaction de dégâts, pas une immunité Fire réellement produite par BattleEngine.

Le feu est original et généré localement : douze langues orangées courbes, huit crocs/cœurs jaune clair, texture douce, braises d’impact et éclairage chaud bref. Les éléments suivent les deux mâchoires ; la surface d’Abri borne le feu à l’impact. Sons d’ignition et claquement séparés ; seul le contact géométrique déclenche le claquement. Matériaux, maillages, texture, lumière et clips sont libérés. La vidéo est muette ; les WAV sont une référence de synthèse séparée, sans écoute des enceintes revendiquée.

La caméra suit l’approche à hauteur de combat, révèle la cible et recadre le contact, puis accompagne le retour. Entrée1s et récupération1,2s, contrôles de trajet/cadrage et limites de vitesse. Les vues rapprochées de surface sont des diagnostics séparés de la caméra jouée.

## Vérification du19octobre2026

v1 : métadonnée Unity mal formée, corrigée avant compilation. v2 : revue120s échouée, contact gauche manquant et deux croisements par esquive. Abri, interruptions, remplacement et orthographique passaient déjà. Les preuves échouées sont conservées.

v3 : passé=False, erreurs=0, durée=120.00s. Sept cas, cinq interruptions, remplacement=True, orthographique=True. Rapport : output/attacks/bite-20261019-v3/verified-summary.json.

Point de reprise : revoir le cadrage à la fermeture (silhouettes et gueule masquées), renforcer encore la lisibilité du feu au contact et contrôler à nouveau les deux côtés. Ne pas assouplir le seuil de cadrage pour faire passer le test.\n\nUne réussite technique ne vaut pas acceptation artistique. Les autres espèces, arènes, toutes les attaques, toutes les dents et la pénétration du corps entier ne sont pas validées par cette revue. Builds/Windows stable conservé ; expérimental WindowsReference. Les modèles et médias restent locaux et ignorés. Aucun crédit/API payant ni reset.

Demande suivante : allumage progressif pendant0,40s sur place avant la course, puis feu maintenu pendant l’approche. Correction postérieure à v3 ; nouvelle compilation et vérification visuelle à distinguer des résultats v3.
