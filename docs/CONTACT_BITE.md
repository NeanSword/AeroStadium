# Croc Feu — Arcanin

La mise en scène actuelle est limitée à Arcanin59 contre Tortank9, dans l’arène Conseil4 moderne. Aucun changement aux dégâts ou aux apprentissages.

La gueule s’enflamme progressivement pendant0,40s sur place avant le départ. Le feu reste vivant pendant la course et la morsure : couches courbes orangées, cœur jaune clair, crocs embrasés, braises et lumière chaude. La phase visuelle utilise une horloge continue, distincte de l’enveloppe d’allumage ; la course ne fige plus les flammes.

Le premier bruitage a été rejeté. La lecture utilise maintenant trois fichiers issus d’un vrai souffle de combustion : embrasement, boucle, poussée de feu à l’impact. Le souffle commence avant le départ et s’atténue en fin de morsure. Pas de couche de croquement. Matière sonore hnhnh, fire-whoosh.wav, CC0 : https://freesound.org/people/hnhnh/sounds/244926/. Provenance, transformations et mesures dans FIRE_FANG_AUDIO_PROVENANCE.json. Les WAV restent locaux sous Assets/AeroStadium/Resources/Audio/Attacks/CrocFeu/ ; ils sont ignorés par Git. Les anciens générateurs PCM restent des API de compatibilité et ne produisent plus ce bruitage en jeu.

Arcanin utilise sa course native011 et le segment de mâchoire du mouvement005, avec retour avant la roulade générique de fin. L’Origin horizontal est compensé extérieurement. La référence de fermeture est le quaternion local réellement écrit dans005.anim à0,53333336s et0,55s : (0,0.76258624,0,0.6468866). La référence n’est plus relevée sur une pose déjà en train de s’ouvrir. La mâchoire doit avoir réellement ouvert puis revenir à4° ou moins de cette pose fermée.

En LateUpdate110, après Animator, esquive et grounding, le contact exige un triangle courant et un écart final de bouche inférieur à2cm. La recherche axiale peut être complétée par un petit déplacement3D du corps quand la tête native sort de la silhouette de la cible ; le point final est à nouveau vérifié sur les vrais triangles. Les bounds ne deviennent jamais un contact de secours. Le retour interpole toutes les coordonnées jusqu’au point initial. Il ne s’agit pas d’un solveur de collision de toutes les dents et du corps entier.

Abri conserve sa surface propre et borne le feu à l’impact. Un vrai raté garde sa ligne initiale, contrôle les croisements avec le mesh animé, n’émet pas de réaction de dégâts et attend le retrait d’Arcanin avant le retour du défenseur. Le contrôle sans hit/sans Miss n’est pas une immunité Fire produite par le moteur.

La caméra utilise les vrais maillages animés, mesurés une fois par frame avec des buffers réutilisés, et la tête réelle. Les anciennes bounds de repos pouvaient se déplacer avec la compensation extérieure alors que le modèle visible restait au contact. Les mesures et ressources de cadrage sont libérées en sortie et interruption. La caméra reste de trois quarts, à hauteur de combat, avec entrée et retour progressifs ; seuil95% de cadrage et limites de mouvement conservés.

## Vérification du19octobre2026

v5 a montré les flammes et le son environ0,36s avant le départ, mais a révélé une référence de mâchoire trop tardive et des défauts de cadrage. Cette série échouée reste conservée. Le bruitage testé à ce stade a été rejeté par le propriétaire.

v6 : passé=True, erreurs=0, durée=120.00s. 0/direct-hit: True; 0/protect: True; 0/accuracy-miss: True; 1/direct-hit: True; 1/protect: True; 1/accuracy-miss: True; 0/non-hit-control: True. Cinq interruptions par phase ; remplacement=True, orthographique=True. Avance minimale observée feu=0.339s, audio=0.382s. Preuves output/attacks/bite-20261019-v6/verified-summary.json. Les gros plans de surface sont des diagnostics distincts de la vraie caméra jouée. Les WAV de revue sont des références séparées ; aucune écoute subjective des enceintes n’est revendiquée.

La revue technique réussit, mais la pose de contact est visuellement insuffisante : le mouvement natif recroqueville trop le torse et masque la gueule. Origin reste à rotation identité ; le pli est réparti entre Spine1, Spine2, Neck et Head. Verrouiller seulement Origin ne corrige pas le geste. Prochaine étape : une pose de morsure du torse avec limitation progressive du pli, en conservant la mâchoire et en recalant les appuis, puis nouvelle revue. Statut needs_work conservé.

Une réussite technique ne vaut pas acceptation artistique définitive. Builds/Windows stable conservé ; expérimental WindowsReference. Les barres de vie restent inchangées et les dégâts flottants retirés. Travail uniquement sur quota, aucun crédit/API payant/reset.
