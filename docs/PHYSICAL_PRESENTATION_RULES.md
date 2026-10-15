# Règle globale de cohérence physique et de mise en scène

Demande utilisateur du15octobre2026, applicable à toute la physique du jeu et à tous les prochains lots.

- Les attaques et capacités restent dans le style Pokémon. Leur matière et leur mouvement doivent avoir une logique propre, sans effet générique censé tout représenter.
- Contact : le geste atteint réellement la portée du corps adverse. Une morsure implique une approche et une fermeture de mâchoire ; les lianes restent attachées et peuvent atteindre à distance. Le type et l’anatomie déterminent l’origine, la portée et la réaction, pas seulement la catégorie Physique.
- Eau : premier contact à la surface, séparation/écoulement autour du volume touché, éclaboussures, gravité, puis retombée au sol. Aucun arrêt sur un disque invisible ou faisceau rigide réfléchi. Les proxys de collision utilisés doivent être explicités ; ne pas prétendre à un solveur de fluide ou à une collision exacte sur chaque triangle animé sans preuve.
- Feu, feuilles, roches, glace, énergie, fumée et sol : réponse adaptée à leur matière, leur masse, leur cible et aux obstacles. Ne pas appliquer automatiquement le ruissellement de l’eau aux autres matières.
- Un raté de précision/esquive résolu peut déclencher une vraie esquive corporelle hors trajectoire. Une immunité, un échec et Abri restent distincts. Les visuels ne changent pas le résultat du moteur de combat.
- Caméra : accompagne anticipation, déplacement, contact et récupération ; conserve la lisibilité, l’orientation gauche/droite et évite les obstacles. Contrôles conservés pour la manette.
- Déplacements : appuis cohérents avec le terrain, aucun passage volontaire dans les corps/obstacles, retour ou repositionnement explicite. Toujours restaurer les transformations temporaires et la caméra en interruption.
- Validation par attaque et espèce réellement testées : origine anatomique, portée/contact, matière, raté/immunité/bouclier, réaction unique, son, récupération, mémoire et nettoyage. Le registre distingue implémentation et validation. Les méthodes/proxys de cette reprise ne prouvent pas une physique complète pour919attaques ou151espèces.

Cette règle s’ajoute au programme des animations, sans modifier les formules, apprentissages ou données de combat. Travail uniquement sur quota, zéro crédit supplémentaire.
