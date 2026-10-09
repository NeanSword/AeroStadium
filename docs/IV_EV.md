# IV et EV par Pokémon

Les membres des équipes portent désormais leurs propres `ivs` et `evs`, sous forme de six valeurs : hp, attack, defense, specialAttack, specialDefense et speed. Deux Pokémon de la même espèce peuvent donc avoir des caractéristiques différentes.

IV :0 à31 par statistique. EV :0 à252 par statistique, total maximum510. Les profils illégaux sont rejetés à la création de la bataille, sans correction silencieuse. Seul un profil null/absent déclenche le défaut IV31/EV0 ; un IV explicitement nul est conservé. Les valeurs sont copiées au début du combat et les propriétés de lecture renvoient des copies.

Calcul pour le niveau L : plancher((2×base+IV+plancher(EV/4))×L/100), puis +L+10 pour les PV, +5 pour les autres statistiques. Les natures restent neutres. Les six valeurs calculées alimentent les dégâts physiques/spéciaux, les PV et la Vitesse après priorité des capacités.

Exemple de membre d’équipe en C# :

```csharp
new TeamMember(6, "none") {
    ivs = new StatValues(31),
    evs = new StatValues { hp = 4, specialAttack = 252, speed = 252 }
};
```

Au niveau50, ce Dracaufeu possède154PV,104Attaque,98Défense,161AttaqueSpéciale,105DéfenseSpéciale et152Vitesse. Les anciennes équipes sans profil conservent leur comportement précédent. Les champs sont sérialisables pour les futures sauvegardes et équipes.

Intégration actuelle : paramétrage de l’équipe et calcul du combat. Le prototype ne possède pas encore de menu d’édition des IV/EV ni de progression persistante qui distribue automatiquement des EV après un K.O. Le moteur ne génère pas aléatoirement les IV à chaque lancement.

Validation :20/20 contrôles du moteur réussis, avec limites, total510/511, IV0, arrondis3/4EV, valeurs de référence Dracaufeu, isolation des profils, sérialisation, influence des dégâts et de l’initiative. Compilation du code et lecture du catalogue Unity réussies (sortie0). Aucun nouveau Windows.exe construit à cette étape. Rapports : output/mechanics/iv-ev-20261009/core-checks.log et Logs/iv-ev-unity-validation-20261009.log.

Référence primaire consultée : code de [Pokémon Showdown](https://github.com/smogon/pokemon-showdown), notamment [pokemon.ts](https://github.com/smogon/pokemon-showdown/blob/master/sim/pokemon.ts) pour les profils individuels et [team-validator.ts](https://github.com/smogon/pokemon-showdown/blob/master/sim/team-validator.ts) pour les contrôles des équipes. L’implémentation C# et ses tests sont propres à AeroStadium.
