# Sélection Pokémon

Après l’écran titre, choisir un mode ouvre le vestiaire de sélection. Les quatre
entrées utilisent pour l’instant le même combat solo de démonstration.

- Les 151 Pokémon de Kanto sont rangés par numéro, 24 par page.
- Rechercher un nom (avec ou sans accents) ou un numéro ; le bouton de type
  parcourt les filtres. Les pages changent avec les flèches à droite.
- Le partenaire survolé ou sélectionné apparaît en 3D avec ses animations natives,
  ses types, sa taille, ses six statistiques de base et ses capacités.
- Valider une carte ajoute le Pokémon. Valider un emplacement occupé le retire.
  Six espèces différentes sont nécessaires pour activer « COMBATTRE ».
- Le retour au menu garde le brouillon d’équipe pendant cette session.
- Clavier : flèches, Entrée pour valider, Échap pour revenir. Souris : survol/clic.
  Manette : stick/croix, validation/retour selon la famille connectée ; Xbox par
  défaut. Le curseur Poké Ball suit la sélection.

L’habillage associe bleu royal, ivoire, or et rouge, capsules, motifs Poké Ball et
accents liés au type du partenaire. Les cartes entrent progressivement ; le focus,
l’énergie de type et l’ajout d’un partenaire réagissent avec des animations. Six voyants
suivent le remplissage de l’équipe et six jauges accompagnent les valeurs des
statistiques. Une notification dispose de sa propre bande au-dessus des commandes.
Quand l’équipe est complète, l’invitation au combat s’illumine.

Le modèle arrive avec une transition courte ; son cadrage stable réserve le haut
de l’aperçu au nom et aux types. Le focus et l’ajout déclenchent la présentation
native lorsqu’elle existe, puis le contrôleur revient à l’attente. Le même modèle
est réutilisé lors d’un ajout sans changement d’espèce. Sur les 151 espèces,
136 disposent d’une animation native de présentation. Les 15 autres utilisent
l’attente : 014, 023, 038, 058, 059, 060, 061, 062, 063, 064, 065, 079, 126, 133 et 138.

Les équipes de location combattent au niveau 50, sans objet, avec IV 31/EV 0 et
nature neutre. Les statistiques affichées sur la fiche sont les bases de l’espèce,
pas les statistiques calculées du combattant. L’édition d’IV/EV reste à ajouter.
Le premier Pokémon de l’équipe entre dans l’arène, face à une équipe IA de même
taille. Après un K.O., le moteur envoie le prochain partenaire encore conscient ;
le modèle, le nom et les PV affichés suivent ce remplacement. Un changement
volontaire pendant le combat n’est pas ajouté par cet écran.

## Assets et validation

Le menu est construit par PokemonSelectionView ; GameBootstrap gère le parcours,
la texture d’aperçu et la transmission des six TeamMember au moteur. Un seul
modèle animé est affiché dans l’aperçu ; la grille utilise 151 portraits locaux.
Les couleurs, la géométrie et les courbes des animations natives sont conservées.
L’organisation et la libération des assets sont décrites dans
[MEMORY_OPTIMIZATION.md](MEMORY_OPTIMIZATION.md).

SelectionPortraitExport.Export génère les PNG 256×256 sous
Assets/AeroStadium/Resources/UI/PokemonPortraits, sans compression ni mipmaps.
Ils restent locaux avec les autres illustrations. Rapports et planche contact
sous output/ui ; ces fichiers sont ignorés par Git.

Le 11 octobre 2026, les 20 contrôles moteur et les 49 tests Unity EditMode passent.
Les neuf nouveaux cas vérifient les sept familles d’énergie, le budget de dessin,
les limites de la zone du portrait, l’absence d’interception de la navigation et
le repos des cartes après transition. Première tentative corrigée : l’appel par
réflexion du test distinguait mal les surcharges OnPopulateMesh ; aucun échec de
sélection dans les 40 contrôles existants.

Les 151 portraits natifs sont rendus en RGBA transparent à 256×256. Tous les
pixels opaques ont les mêmes valeurs RGB que les portraits précédents. Les carrés
sombres ont disparu. PokemonCardEnergy dessine une aura latérale, deux anneaux
segmentés en contre-rotation et six particules adaptées au type : braises,
feuilles, bulles, éclairs, volutes, étoiles ou facettes. Le focus arrive en 0,18 s ;
l’ajout produit une impulsion de 0,72 s. Le portrait, le nom, les types et le badge
gardent leur lisibilité. Aucune bande diagonale mobile ne subsiste sur les cartes.
L’énergie suit aussi le partenaire inspecté au survol souris, indépendamment du
bouton précédemment sélectionné ; ce comportement possède un contrôle dédié.

Le dessin actif est plafonné à 30 Hz et 912 sommets, sans allocation de tableau
par image. Les cartes inactives ont un dessin vide, ou 32 sommets statiques pour
les repères d’équipe. Les purges Editor se font par cinq pendant l’export.

Compilation WindowsReference réussie, zéro erreur/avertissement ; revue visible
120 secondes réussie avec manette/souris, recherche, pagination, six partenaires,
aperçus, combat, remplacement après K.O. et retour à l’équipe. Pic de mémoire du
processus observé : 0.670 Gio RAM / 1.166 Gio privée. Preuves sous
output/ui/card-effects-20261011 et LastProgressLogs.md.

L’option --selection-test vérifie le parcours titre/menu, la manette virtuelle,
le clic souris, la recherche/pagination, la limite d’équipe, l’entrée au combat,
un remplacement après K.O. et le retour à la sélection. Les périphériques
physiques sont suspendus uniquement dans le frontal Unity durant ce test
automatique, puis rétablis ; Windows reste utilisable. Le test ignore également
les changements de focus pour ses périphériques virtuels, puis restaure cette
politique à la sortie. Le parcours normal accepte les périphériques physiques.

Export des portraits suivant la documentation Unity :
https://docs.unity.com/en-us/engine/6000.0/manual/cameras/urp/multiple/user-render-requests
