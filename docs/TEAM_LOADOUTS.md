# Fiches d’équipe et apprentissages récents

L’atelier de chaque Pokémon se trouve dans **Personnaliser**, à côté d’**Ajouter à l’équipe**. Les six emplacements de l’équipe ouvrent également leur fiche. Les portraits détourés, effets des cartes et modèle animé de l’écran de sélection sont conservés.

## Capacités

Quatre emplacements acceptent une à quatre capacités différentes. La liste proposée vient exclusivement de `Catalog.GetLearnableMoves(speciesId)` ; aucun choix hors de l’apprentissage de l’espèce n’est accepté, y compris par l’API de configuration. Toutes les méthodes d’apprentissage du catalogue sont réunies. Le catalogue de données indique la provenance récente, dont Légendes Z-A pour les apprentissages de base demandés par l’utilisateur.

La liste est paginée (sept lignes), recherchable par nom français ou type, et affiche type, catégorie physique / spéciale / statut, puissance, précision et PP. Les valeurs non applicables sont présentées par un tiret. Une capacité ne peut pas occuper deux emplacements. Vider un emplacement compacte la liste ; au moins une capacité reste nécessaire avant l’enregistrement.

Les effets spécialisés qui restent incomplets sont signalés dans la liste. L’atelier n’ajoute aucune animation de capacité et ne présente pas une mécanique incomplète comme terminée.

## IV et EV

Chaque Pokémon possède six IV indépendants de **0 à 31** et six EV indépendants de **0 à 252**, avec un maximum global de **510 EV**. Les boutons changent les IV par unité et les EV par quatre ; la saisie numérique permet les valeurs exactes, notamment les six derniers points d’un partage 252 / 252 / 6. Les valeurs au-delà des limites sont refusées sans changer la fiche.

Les valeurs de départ sont IV 31 partout et EV 0 partout. Deux préréglages d’IV et une remise à zéro des EV sont disponibles. Les statistiques recalculées au **niveau 50**, avec une nature neutre pour ce prototype, sont affichées à chaque modification. Le budget d’EV disponible et six jauges restent visibles.

## Objet tenu

Le sac propose les objets marqués comme pouvant être tenus en combat. Les objets importants et autres objets non équipables ne sont pas disponibles dans ce sélecteur. La recherche porte sur les noms français ; le choix **Aucun objet** retire l’objet tenu. Les autres objets du catalogue restent disponibles aux systèmes de données du jeu, sans être assimilés à des objets tenus.

## Brouillons, navigation et persistance

L’atelier travaille sur une **copie**. **Enregistrer** valide puis remplace la fiche en une seule opération ; **Annuler**, **Fermer** et la touche Retour ne changent ni les capacités, ni l’objet, ni l’entraînement. La navigation et les clics de la grille sont bloqués pendant que l’atelier est ouvert. Le curseur Poké Ball suit les contrôles de l’atelier à la manette et les liens de navigation restent dans la fenêtre.

Une fiche peut être préparée avant d’ajouter le Pokémon. Le bouton **Retirer** libère l’emplacement et efface sa fiche ; un nouvel ajout de cette espèce repart du profil de location. Les profils sauvegardés survivent au retour des combats et à la reconstruction de l’écran lorsqu’ils sont transmis dans `savedProfiles`. Toutes les sorties publiques sont copiées, y compris les tableaux de capacités et les IV / EV.

### API de présentation

- `PokemonSelectionView.OpenEditor(speciesId)`, `EditorOpen`, `LoadoutEditor`, `HandleBack()`.
- `GetMemberConfiguration(speciesId)`, `TryConfigureMember(TeamMember)` et `GetTeam()`.
- `Build(..., int[] savedTeam = null, TeamMember[] savedProfiles = null)` préserve la compatibilité de l’ancien ordre d’équipe et accepte les profils complets.
- `PokemonLoadoutEditor.SetMove(slot, moveId)`, `SetIndividualValue(stat, value)`, `SetEffortValue(stat, value)`, `SetHeldItem(itemId)`, `SetTab(tab)`, `Save()`, `Cancel()`.
- Indices de statistique : PV, Attaque, Défense, Attaque spéciale, Défense spéciale, Vitesse. Onglets : Capacités, IV / EV, Objet.

## Vérification

Les contrôles de présentation couvrent l’annulation atomique, l’enregistrement, l’isolement des copies, les capacités illégales et dupliquées, les bornes IV / EV, le budget 510, l’exclusion des objets importants, la suppression d’un profil, la restauration après reconstruction et la configuration des 151 espèces de Kanto. La compilation, les contrôles et l’observation en jeu sont orchestrés à la fin de l’intégration globale ; leurs résultats sont consignés dans `LastProgressLogs.md`.
