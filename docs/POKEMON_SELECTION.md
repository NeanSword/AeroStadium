# Sélection Pokémon

Après l’écran titre, choisir un mode ouvre le vestiaire de sélection. Les quatre
entrées utilisent pour l’instant le même combat solo de démonstration.

- Les151 Pokémon de Kanto sont rangés par numéro,24 par page.
- Rechercher un nom (avec ou sans accents) ou un numéro ; le bouton de type
  parcourt les filtres. Les pages changent avec les flèches à droite.
- Le partenaire survolé ou sélectionné apparaît en3D avec son animation native,
  ses types, sa taille, ses six statistiques de base et ses capacités.
- Valider une carte ajoute le Pokémon. Valider un emplacement occupé le retire.
  Six espèces différentes sont nécessaires pour activer « COMBATTRE ».
- Le retour au menu garde le brouillon d’équipe pendant cette session.
- Clavier : flèches, Entrée pour valider, Échap pour revenir. Souris : survol/clic.
  Manette : stick/croix, validation/retour selon la famille connectée ; Xbox par
  défaut. Le curseur Poké Ball suit la sélection.

Les équipes de location combattent au niveau50, sans objet, avec IV31/EV0 et
nature neutre. Les statistiques affichées sur la fiche sont les bases de l’espèce,
pas les statistiques calculées du combattant. L’édition d’IV/EV reste à ajouter.
Le premier Pokémon de l’équipe entre dans l’arène, face à une équipe IA de même
taille. Après un K.O., le moteur envoie le prochain partenaire encore conscient ;
le modèle, le nom et les PV affichés suivent ce remplacement. Un changement
volontaire pendant le combat n’est pas ajouté par cet écran.

## Assets et validation

Le menu est construit par PokemonSelectionView ; GameBootstrap gère le parcours,
la texture d’aperçu et la transmission des six TeamMember au moteur. Une seule
figurine animée est chargée dans l’aperçu ; la grille utilise151portraits locaux.
Les modèles normaux, matériaux et animations natifs restent inchangés.

SelectionPortraitExport.Export génère les PNG256×256 sous
Assets/AeroStadium/Resources/UI/PokemonPortraits, sans compression ni mipmaps.
Ils restent locaux avec les autres illustrations. Rapports et planche contact
sous output/ui ; ces fichiers sont ignorés par Git.

Validation :20contrôles moteur et40tests Unity EditMode passent. L’option
--selection-test vérifie le parcours titre/menu, la manette virtuelle, le clic
souris, la recherche/pagination, la limite d’équipe et l’entrée au combat, puis
un remplacement après K.O. Les périphériques physiques sont suspendus uniquement
dans le frontal Unity durant ce test automatique, puis rétablis ; Windows reste
utilisable. Le parcours normal accepte les périphériques physiques.

Export des portraits suivant la documentation Unity :
https://docs.unity.com/en-us/engine/6000.0/manual/cameras/urp/multiple/user-render-requests
