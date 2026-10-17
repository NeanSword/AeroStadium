# Moteur de combat

Le moteur est écrit en C# 9 / .NET Standard 2.1, sans référence à Unity. Les vérifications en console compilent les mêmes sources avec .NET 10, sans paquet externe. Cette page décrit les principes et le périmètre de la première base du moteur ; les mécaniques ajoutées ensuite et leurs limites sont détaillées dans [les règles de combat](../../../docs/BATTLE_RULES.md) et le [journal du projet](../../../LastProgressLogs.md).

## Catalogue et lancement d'un combat

`Catalog` contient les tableaux `species`, `moves` et `items`, sérialisables en JSON. Après chargement, `Validate()` vérifie les données. Les identifiants des espèces et capacités sont des entiers stables, indépendants de leur position dans le tableau ; le moteur n'impose aucune limite de 255 espèces. Les objets utilisent des identifiants texte. Une catégorie, un type, un identifiant ou un effet inconnu produit une erreur de validation. Une fois validées, les définitions doivent rester immuables.

```csharp
var battle = new BattleEngine(catalog,
    new[] { new TeamMember(250, "lifeorb"), new TeamMember(152, "leftovers") },
    new[] { new TeamMember(700) }, seed: 2026);
var events = battle.ResolveTurn(BattleChoice.Move(0), battle.ChooseAi(1));
```

Les camps sont numérotés 0 pour le joueur et 1 pour l'adversaire. Les emplacements de capacités et d'équipe commencent à zéro. `Active(side)`, `Team(side)` et `ActiveIndex(side)` donnent accès à l'état courant. `Turn` commence à zéro. `IsFinished` et `Winner` indiquent l'issue : 0 pour le joueur, 1 pour l'adversaire, −1 pour une égalité ; `Winner` reste nullable avant le résultat.

`BattleEvent.Kind` utilise `BattleEventKind` ; `Message` contient le texte français affichable. Selon l'événement, `Side`, `TargetSide`, `Amount`, `MoveId` et `TeamIndex` précisent la source, la cible, la valeur, la capacité et l'emplacement concerné. La présentation Unity transmet les choix au moteur, puis anime les événements reçus. Le temps des animations ne change jamais le résultat du combat.

## Déroulement d'un tour

Les deux actions sont validées avant toute modification d'état ou consommation d'aléatoire. Un changement de Pokémon a la priorité 6, consomme le tour et réinitialise les variations temporaires de statistiques. La priorité de capacité passe avant la Vitesse ; une égalité de Vitesse est départagée par une suite aléatoire reproductible, indépendante du runtime.

Une action en attente est ignorée si son Pokémon tombe K.O. Le premier remplaçant conscient entre automatiquement après les deux actions et les objets de fin de tour ; il ne reçoit aucune action gratuite. Une équipe peut comprendre de un à six membres, ce qui permet aussi les duels à trois Pokémon prévus pour le projet.

## Périmètre de la première base

- Chaque capacité possède sa catégorie physique, spéciale ou de statut, indépendamment de son type. Le physique utilise Attaque/Défense ; le spécial utilise Attaque Spéciale/Défense Spéciale.
- Les 18 types ordinaires, les doubles types, le STAB, la précision, les PP, la priorité et un jet de dégâts reproductible entre 85 et 100 % sont pris en charge.
- Le profil initial utilise un niveau fixe (50 par défaut), une nature neutre, 31 IV et aucun EV, avec des statistiques de base strictement positives. La formule entière de base est suivie de multiplicateurs en virgule flottante : les arrondis ne reproduisent pas exactement ceux de la génération 9.
- Nitrocharge donne +1 en Vitesse après un coup infligeant des dégâts ; Danse Lames donne +2 en Attaque ; Soin rend la moitié des PV maximaux. Abri est présent, mais son utilisation consécutive échoue systématiquement dans cette base, sans probabilité dégressive officielle. Les augmentations plafonnent à +6 et sont réinitialisées au changement.
- `RecoilThird` retire un tiers des PV réellement enlevés, arrondi à l'inférieur avec un minimum de un. Restes rend 1/16 des PV maximaux en fin de tour ; Orbe Vie multiplie les dégâts par 1,3 et retire 1/10 des PV maximaux ; Charbon et les bonus de type configurables utilisent 1,2 par défaut.
- Quand toutes les capacités sont à zéro PP, Lutte inflige des dégâts physiques sans type et retire 1/4 des PV maximaux après un coup réussi.

Cette base est un prototype. Elle ne couvre pas les critiques, talents, altérations de statut majeures, météo, terrains, pièges, personnalisation des EV/natures, formes particulières, Téracristallisation/Stellaire, attaques multiples, combats doubles ou tous les effets d'objets et de capacités. Chaque ajout doit être implémenté explicitement. L'IA estime simplement les dégâts et utilise quelques actions de soin ou de préparation ; elle n'est pas compétitive. Le catalogue initial n'est pas un Pokédex complet. Les sauvegardes persistantes et un format de replay compatible entre versions ne sont pas implémentés.

La table des types a été vérifiée avec [la source Pokémon Showdown](https://github.com/smogon/pokemon-showdown/blob/master/data/typechart.ts). Cette source comprend Stellaire ; le périmètre décrit ici se limite aux 18 types ordinaires.

## Vérifications

Depuis la racine du projet :

```powershell
dotnet build Assets/AeroStadium/Core/AeroStadium.Core.csproj --artifacts-path Tests/CoreChecks/artifacts
dotnet run --project Tests/CoreChecks/CoreChecks.csproj -- Assets/AeroStadium/Resources/Data/catalog.json
```
