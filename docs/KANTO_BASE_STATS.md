# Statistiques de base des 151 Pokémon de Kanto

Les six statistiques actuelles (PV, Attaque, Défense, Attaque Spéciale, Défense Spéciale, Vitesse) des formes normales sont intégrées dans `Assets/AeroStadium/Resources/Data/catalog.json`. Il ne s’agit pas des anciennes valeurs de Rouge/Bleu : les changements ultérieurs sont conservés. Les variantes régionales, Méga-Évolutions et formes Gigamax ne sont pas utilisées.

Source de données : [PokéAPI](https://pokeapi.co/docs/v2#pokemon), dépôt au commit [2fe95532d27a9bf340575253aff50868319d8182](https://github.com/PokeAPI/pokeapi/tree/2fe95532d27a9bf340575253aff50868319d8182/data/v2/csv). Les trois fichiers CSV source, leurs SHA-256, la copie du catalogue avant vérification et le rapport sont conservés localement dans `output/data/kanto-base-stats-20261009`.

Vérification du 9 octobre 2026 :151 espèces, ID1–151 uniques,906 valeurs comparées ; 0 espèce(s) modifiée(s). Tous les autres champs du catalogue sont conservés. La liste complète consultable est [KANTO_BASE_STATS.csv](KANTO_BASE_STATS.csv).

Le moteur utilise ces valeurs pour calculer les caractéristiques au niveau choisi. Les IV/EV sont désormais propres à chaque Pokémon : IV31/EV0 par défaut, profils configurables et nature neutre. Voir [IV_EV.md](IV_EV.md). Les dégâts physiques utilisent Attaque/Défense ; les dégâts spéciaux utilisent Attaque Spéciale/Défense Spéciale. La Vitesse contribue à l’initiative après la priorité des capacités.

Les statistiques déjà présentes provenaient de l’import PokéAPI réalisé par `Tools/prepare_generation_one.py`. Cette vérification ne recrée pas les modèles et ne modifie pas les attaques, objets ou tailles.
