using System;

namespace AeroStadium.Presentation
{
    /// <summary>
    /// Motion anatomy of the regular Kanto form. Float describes a hovering or
    /// unsupported body; Shell describes a rigid body with small appendages.
    /// These are animation families, not biological categories or rig names.
    /// </summary>
    public enum PokemonAnatomy
    {
        Biped, Quadruped, Bird, Bat, Insect, Serpent, Fish, Float, Plant, Blob, Shell
    }

    /// <summary>
    /// Original authored motion directions for the 151 regular Kanto species.
    /// Rates are cycles per second; rotation sizes are degrees; durations are
    /// seconds. FloatHeight is a fraction of the fitted model height. Weight
    /// is an artistic inertia coefficient, not a mass in kilograms.
    /// No reference clips, captured poses, or animation curves are embedded.
    /// </summary>
    public readonly struct PokemonMotionProfile
    {
        public const int SpeciesCount = 151;

        public readonly PokemonAnatomy Anatomy;
        public readonly float FloatHeight;
        public readonly float BreathRate;
        public readonly float BreathSize;
        public readonly float LookSize;
        public readonly float TailSize;
        public readonly float EarSize;
        public readonly float WingRate;
        public readonly float WingSize;
        public readonly float AttackDuration;
        public readonly float HitDuration;
        public readonly float FaintDuration;
        public readonly float Weight;

        static readonly PokemonMotionProfile[] Profiles = BuildProfiles();

        public PokemonMotionProfile(PokemonAnatomy anatomy, float floatHeight,
            float breathRate, float breathSize, float lookSize, float tailSize,
            float earSize, float wingRate, float wingSize, float attackDuration,
            float hitDuration, float faintDuration, float weight)
        {
            Anatomy = anatomy;
            FloatHeight = floatHeight;
            BreathRate = breathRate;
            BreathSize = breathSize;
            LookSize = lookSize;
            TailSize = tailSize;
            EarSize = earSize;
            WingRate = wingRate;
            WingSize = wingSize;
            AttackDuration = attackDuration;
            HitDuration = hitDuration;
            FaintDuration = faintDuration;
            Weight = weight;
        }

        public static PokemonMotionProfile ForSpecies(int species)
        {
            if (species < 1 || species > SpeciesCount)
                throw new ArgumentOutOfRangeException(nameof(species), species,
                    "A Kanto motion profile requires a species number from 1 to 151.");
            return Profiles[species];
        }

        static PokemonMotionProfile[] BuildProfiles()
        {
            var profiles = new PokemonMotionProfile[SpeciesCount + 1];

            // Family helpers establish grounded, restrained idles. Individual
            // weights, pace, appendage sizes and hover heights then distinguish
            // every base species. Larger evolutions settle more slowly.
            profiles[1] = Quadruped(.65f, 1.10f); // Bulbasaur: bulb follows a low, four-footed breath.
            profiles[2] = Quadruped(1.05f, .97f); // Ivysaur.
            profiles[3] = Plant(2.10f, .74f, .85f); // Venusaur: heavy body, restrained flower sway.
            profiles[4] = Biped(.56f, 1.16f, tail: 10f); // Charmander.
            profiles[5] = Biped(.95f, 1.02f, tail: 8f); // Charmeleon.
            profiles[6] = Biped(1.72f, .85f, tail: 9f, wingRate: .48f, wings: 5f); // Charizard stays grounded; wings settle.
            profiles[7] = Biped(.68f, 1.08f, tail: 5f); // Squirtle.
            profiles[8] = Biped(.95f, .98f, tail: 7f, ears: 3.5f); // Wartortle.
            profiles[9] = Biped(2.00f, .77f, tail: 2.5f); // Blastoise: planted feet and cannon recoil capacity.
            profiles[10] = Serpent(.40f, 1.21f, tail: 6f); // Caterpie: a short creeping body, not a floating tail.
            profiles[11] = Shell(.51f, .86f); // Metapod: tiny settling motion, no imaginary limbs.
            profiles[12] = Insect(.62f, 1.10f, wingRate: 1.12f, wings: 25f, hover: .09f); // Butterfree.
            profiles[13] = Serpent(.42f, 1.22f, tail: 5f); // Weedle.
            profiles[14] = Shell(.54f, .84f); // Kakuna.
            profiles[15] = Insect(.87f, 1.15f, wingRate: 2.2f, wings: 12f, hover: .10f); // Beedrill: faster, shallow wing beats.
            profiles[16] = Bird(.48f, 1.17f, wings: 3f); // Pidgey: grounded, folded-wing idle.
            profiles[17] = Bird(.93f, .98f, wings: 18f, hover: .055f); // Pidgeotto.
            profiles[18] = Bird(1.45f, .86f, wings: 22f, hover: .065f); // Pidgeot.
            profiles[19] = Quadruped(.46f, 1.25f, tail: 10f, ears: 6f); // Rattata.
            profiles[20] = Quadruped(.89f, 1.04f, tail: 6f, ears: 3.5f); // Raticate.
            profiles[21] = Bird(.51f, 1.21f, wings: 4f); // Spearow.
            profiles[22] = Bird(1.16f, .93f, wings: 23f, hover: .075f); // Fearow.
            profiles[23] = Serpent(.68f, 1.08f, tail: 11f); // Ekans: supported coil, head checks the opponent.
            profiles[24] = Serpent(1.48f, .83f, tail: 9f); // Arbok: deliberate hood and coil motion.
            profiles[25] = Biped(.59f, 1.19f, tail: 9f, ears: 6f); // Pikachu.
            profiles[26] = Biped(.95f, 1.02f, tail: 12f, ears: 4.5f); // Raichu.
            profiles[27] = Biped(.62f, 1.08f, tail: 4f, ears: 2f); // Sandshrew.
            profiles[28] = Biped(1.12f, .92f, tail: 5f, ears: 2f); // Sandslash.
            profiles[29] = Quadruped(.49f, 1.14f, ears: 6f); // Nidoran female.
            profiles[30] = Quadruped(.89f, .98f, ears: 4.5f); // Nidorina.
            profiles[31] = Biped(1.86f, .80f, tail: 4f, ears: 2f); // Nidoqueen.
            profiles[32] = Quadruped(.51f, 1.20f, ears: 6.5f); // Nidoran male.
            profiles[33] = Quadruped(1.02f, 1.00f, ears: 4.5f); // Nidorino.
            profiles[34] = Biped(1.98f, .83f, tail: 6f, ears: 2f); // Nidoking.
            profiles[35] = Biped(.60f, 1.03f, tail: 3f, ears: 4f); // Clefairy.
            profiles[36] = Biped(.95f, .90f, tail: 3.5f, ears: 3f, wingRate: .38f, wings: 1.5f); // Clefable's small wings settle.
            profiles[37] = Quadruped(.55f, 1.08f, tail: 9f, ears: 5f); // Vulpix.
            profiles[38] = Quadruped(1.13f, .82f, tail: 7f, ears: 3.5f); // Ninetales: slower fan-tail motion.
            profiles[39] = Biped(.55f, 1.01f, ears: 3.5f); // Jigglypuff: small feet stay on the floor.
            profiles[40] = Biped(.91f, .93f, ears: 5.5f); // Wigglytuff.
            profiles[41] = Bat(.43f, 1.20f, wings: 29f, hover: .115f); // Zubat.
            profiles[42] = Bat(.93f, .93f, wings: 24f, hover: .105f); // Golbat.
            profiles[43] = Plant(.42f, 1.10f, 2.4f); // Oddish: grounded feet, separate leaf sway.
            profiles[44] = Plant(.71f, .91f, 1.8f); // Gloom.
            profiles[45] = Plant(1.07f, .85f, 1.2f); // Vileplume: flower motion limited by its broad crown.
            profiles[46] = Insect(.52f, 1.06f); // Paras: grounded six-legged gait family, no wing motion.
            profiles[47] = Insect(1.21f, .82f); // Parasect: heavy mushroom settles over planted legs.
            profiles[48] = Insect(.71f, 1.13f); // Venonat: fuzzy grounded body, not moth flight.
            profiles[49] = Insect(.81f, 1.08f, wingRate: 1.35f, wings: 24f, hover: .105f); // Venomoth.
            profiles[50] = Blob(.45f, 1.13f, .50f); // Diglett: ground anchor remains fixed.
            profiles[51] = Blob(.83f, .96f, .45f); // Dugtrio: subtle independent head accents.
            profiles[52] = Biped(.53f, 1.19f, tail: 10f, ears: 5f); // Meowth.
            profiles[53] = Quadruped(1.08f, .91f, tail: 9f, ears: 3.5f); // Persian.
            profiles[54] = Biped(.73f, 1.03f, tail: 2f); // Psyduck.
            profiles[55] = Biped(1.25f, .92f, tail: 4f); // Golduck.
            profiles[56] = Biped(.66f, 1.23f, tail: 9f, ears: 4f); // Mankey: alert, light, short breath cycle.
            profiles[57] = Biped(1.16f, 1.04f, ears: 2f); // Primeape: no invented tail.
            profiles[58] = Quadruped(.81f, 1.07f, tail: 9f, ears: 4f); // Growlithe.
            profiles[59] = Quadruped(1.98f, .81f, tail: 7f, ears: 3f); // Arcanine.
            profiles[60] = Biped(.47f, 1.14f, tail: 10f); // Poliwag has a swimming tail but grounded feet.
            profiles[61] = Biped(.83f, 1.03f); // Poliwhirl.
            profiles[62] = Biped(1.42f, .88f); // Poliwrath.
            profiles[63] = Floating(.50f, .77f, hover: .07f, tail: 4f, ears: 2f); // Abra: small, sleepy levitation.
            profiles[64] = Biped(.95f, .91f, tail: 6f, ears: 2f); // Kadabra.
            profiles[65] = Biped(1.27f, .85f, ears: 2f); // Alakazam: calm grounded focus.
            profiles[66] = Biped(.65f, 1.10f, tail: 3f); // Machop.
            profiles[67] = Biped(1.39f, .92f); // Machoke.
            profiles[68] = Biped(1.94f, .82f); // Machamp: shared planted stance, four arms bind separately.
            profiles[69] = Plant(.43f, 1.12f, 2.8f); // Bellsprout: supple stem with fixed root-like feet.
            profiles[70] = Plant(.76f, .98f, 2f, hover: .04f); // Weepinbell: light suspension, restrained mouth.
            profiles[71] = Plant(1.29f, .85f, 1.5f, hover: .045f); // Victreebel.
            profiles[72] = Fish(.61f, 1.02f, hover: .08f, tail: 5f); // Tentacool: soft tentacles beneath floating cap.
            profiles[73] = Fish(1.70f, .79f, hover: .055f, tail: 4f); // Tentacruel: longer, slower trailing limbs.
            profiles[74] = Floating(.76f, 1.03f, hover: .09f); // Geodude: suspended rock with independent arms.
            profiles[75] = Biped(1.75f, .83f); // Graveler: feet carry its four-armed weight.
            profiles[76] = Biped(2.25f, .74f); // Golem.
            profiles[77] = Quadruped(.90f, 1.08f, tail: 8f, ears: 3f); // Ponyta.
            profiles[78] = Quadruped(1.56f, .92f, tail: 8f, ears: 2.5f); // Rapidash.
            profiles[79] = Quadruped(1.04f, .73f, tail: 7f, ears: 1.5f); // Slowpoke: slow, asymmetrical attention.
            profiles[80] = Biped(1.61f, .72f, tail: 2f, ears: 1.5f); // Slowbro: tail shell remains heavy.
            profiles[81] = Floating(.47f, 1.03f, hover: .15f); // Magnemite.
            profiles[82] = Floating(1.00f, .87f, hover: .13f); // Magneton: group orientation, no invented breath bend.
            profiles[83] = Bird(.72f, 1.08f, wings: 3f); // Farfetch'd: grounded, leek arm remains an arm.
            profiles[84] = Bird(.93f, 1.15f, wings: 0f); // Doduo: flightless, both heads lead the grounded body.
            profiles[85] = Bird(1.48f, 1.02f, wings: 0f); // Dodrio.
            profiles[86] = Fish(.88f, .94f, tail: 7f); // Seel: supported belly and flippers, no air hover.
            profiles[87] = Fish(1.48f, .82f, tail: 8f); // Dewgong.
            profiles[88] = Blob(.88f, .99f, 1.8f); // Grimer: compliant body over a fixed floor contact.
            profiles[89] = Blob(1.89f, .76f, 1.4f); // Muk: heavier and slower than Grimer.
            profiles[90] = Shell(.63f, 1.07f); // Shellder: opening hinge, not whole-shell stretching.
            profiles[91] = Shell(1.55f, .83f); // Cloyster.
            profiles[92] = Floating(.43f, 1.09f, hover: .145f); // Gastly: drifting core rather than limbs.
            profiles[93] = Floating(.78f, 1.03f, hover: .12f); // Haunter: hovering torso and separate hands.
            profiles[94] = Biped(1.17f, .99f); // Gengar is grounded despite its ghost typing.
            profiles[95] = Serpent(2.50f, .66f, tail: 4f); // Onix: ground-supported rock chain, low-frequency turns.
            profiles[96] = Biped(.95f, .91f, ears: 2f); // Drowzee.
            profiles[97] = Biped(1.31f, .83f, ears: 2f); // Hypno.
            profiles[98] = Insect(.58f, 1.12f); // Krabby: low, grounded crustacean family.
            profiles[99] = Insect(1.52f, .90f); // Kingler: claw weight represented by slower responses.
            profiles[100] = Shell(.65f, 1.08f); // Voltorb: grounded rigid sphere; no rubber breathing.
            profiles[101] = Shell(1.10f, .94f); // Electrode.
            profiles[102] = Blob(.60f, 1.00f, .70f); // Exeggcute: grouped grounded eggs, subtle head gestures.
            profiles[103] = Plant(1.93f, .76f, 1.1f); // Exeggutor: stable trunk and slow crown motion.
            profiles[104] = Biped(.63f, 1.03f, tail: 5f); // Cubone.
            profiles[105] = Biped(1.01f, .93f, tail: 5f); // Marowak regular form only.
            profiles[106] = Biped(1.07f, 1.06f); // Hitmonlee: grounded elastic stance without idle foot sliding.
            profiles[107] = Biped(1.12f, 1.11f); // Hitmonchan: sharper attention and quick guarded breath.
            profiles[108] = Biped(1.18f, .90f, tail: 5f); // Lickitung.
            profiles[109] = Floating(.71f, .98f, hover: .115f); // Koffing.
            profiles[110] = Floating(1.23f, .81f, hover: .095f); // Weezing regular two-core form.
            profiles[111] = Quadruped(1.86f, .83f, tail: 2f, ears: 1.5f); // Rhyhorn.
            profiles[112] = Biped(2.12f, .76f, tail: 4f, ears: 1.5f); // Rhydon.
            profiles[113] = Biped(1.12f, .86f, tail: 3f, ears: 3f); // Chansey.
            profiles[114] = Plant(.89f, 1.00f, 1.8f); // Tangela: planted shoes and gentle vine follow-through.
            profiles[115] = Biped(1.97f, .80f, tail: 5f, ears: 2f); // Kangaskhan: grounded carrier, no independent pouch translation.
            profiles[116] = Fish(.42f, 1.14f, hover: .085f, tail: 7f); // Horsea.
            profiles[117] = Fish(.83f, 1.00f, hover: .075f, tail: 6f); // Seadra.
            profiles[118] = Fish(.58f, 1.12f, hover: .085f, tail: 12f); // Goldeen.
            profiles[119] = Fish(1.12f, .96f, hover: .075f, tail: 10f); // Seaking.
            profiles[120] = Floating(.62f, 1.07f, hover: .075f); // Staryu: rigid radial body, gentle suspension.
            profiles[121] = Floating(1.03f, .94f, hover: .075f); // Starmie.
            profiles[122] = Biped(.93f, 1.05f); // Mr. Mime regular form.
            profiles[123] = Insect(1.19f, 1.13f, wingRate: .61f, wings: 4f); // Scyther: planted legs, small wing settling.
            profiles[124] = Biped(1.16f, .87f); // Jynx: dress base stays supported.
            profiles[125] = Biped(1.29f, 1.08f, tail: 5f, ears: 2f); // Electabuzz.
            profiles[126] = Biped(1.39f, .98f, tail: 6f); // Magmar.
            profiles[127] = Insect(1.46f, .93f); // Pinsir: grounded rigid pincers; no wing flap.
            profiles[128] = Quadruped(1.70f, .96f, tail: 11f, ears: 2.5f); // Tauros regular form, three trailing tails.
            profiles[129] = Fish(.47f, 1.21f, hover: .035f, tail: 13f); // Magikarp: small supported lift and short tail beats.
            profiles[130] = Serpent(2.32f, .76f, tail: 10f, hover: .04f); // Gyarados: long chain, restrained head and body follow-through.
            profiles[131] = Fish(2.01f, .76f, hover: .025f, tail: 4f); // Lapras: heavy shell stays stable over flippers.
            profiles[132] = Blob(.51f, 1.05f, 1.5f); // Ditto: small original body compression, fixed floor contact.
            profiles[133] = Quadruped(.46f, 1.17f, tail: 10f, ears: 6f); // Eevee.
            profiles[134] = Quadruped(.90f, .99f, tail: 12f, ears: 2.5f); // Vaporeon.
            profiles[135] = Quadruped(.79f, 1.21f, tail: 3f, ears: 4.5f); // Jolteon.
            profiles[136] = Quadruped(.88f, 1.04f, tail: 8f, ears: 4f); // Flareon.
            profiles[137] = Floating(.75f, .90f, hover: .065f, tail: 2f); // Porygon: restrained rigid-body hover.
            profiles[138] = Shell(.50f, 1.05f); // Omanyte.
            profiles[139] = Shell(1.01f, .91f); // Omastar.
            profiles[140] = Shell(.57f, 1.10f); // Kabuto: ground contact under a stable carapace.
            profiles[141] = Biped(1.27f, 1.02f, tail: 3f); // Kabutops.
            profiles[142] = Bat(1.56f, .85f, wings: 23f, hover: .09f); // Aerodactyl: membranous forelimb wings.
            profiles[143] = Biped(2.50f, .66f); // Snorlax: low-amplitude, long breathing with planted feet.
            profiles[144] = Bird(1.78f, .79f, wings: 25f, hover: .075f, tail: 8f); // Articuno.
            profiles[145] = Bird(1.84f, .87f, wings: 21f, hover: .075f, tail: 3f); // Zapdos.
            profiles[146] = Bird(1.82f, .83f, wings: 24f, hover: .075f, tail: 6f); // Moltres.
            profiles[147] = Serpent(.43f, 1.10f, tail: 10f); // Dratini.
            profiles[148] = Serpent(.95f, .86f, tail: 11f, hover: .035f); // Dragonair: slight suspension, no large root rise.
            profiles[149] = Biped(2.11f, .79f, tail: 7f, ears: 2f, wingRate: .43f, wings: 4f); // Dragonite rests on its feet.
            profiles[150] = Biped(1.53f, .84f, tail: 12f, ears: 1.5f); // Mewtwo: controlled gaze and independent tail.
            profiles[151] = Floating(.41f, 1.06f, hover: .105f, tail: 14f, ears: 2f); // Mew: light suspended body and supple tail.

            // An incomplete species table is an authoring error, never a silent
            // fallback to the generic biped used for a different Pokémon.
            for (int species = 1; species <= SpeciesCount; species++)
                if (profiles[species].BreathRate <= 0f)
                    throw new InvalidOperationException("Missing authored Kanto motion profile: " + species);
            return profiles;
        }

        static PokemonMotionProfile Biped(float weight, float pace, float tail = 0f,
            float ears = 0f, float wingRate = 0f, float wings = 0f)
        {
            return Make(PokemonAnatomy.Biped, weight, pace, .95f, 4f, tail, ears, wingRate, wings);
        }

        static PokemonMotionProfile Quadruped(float weight, float pace, float tail = 0f, float ears = 0f)
        {
            return Make(PokemonAnatomy.Quadruped, weight, pace, .80f, 3.6f, tail, ears);
        }

        static PokemonMotionProfile Bird(float weight, float pace, float wings,
            float hover = 0f, float tail = 4f)
        {
            // Grounded birds make a small settling gesture; airborne birds use
            // their slower full wing cycle. Flightless birds have no wing role.
            float rate = wings <= 0f ? 0f : hover > 0f ? .86f * pace : .30f * pace;
            return Make(PokemonAnatomy.Bird, weight, pace, .75f, 4.4f, tail, 0f, rate, wings, hover);
        }

        static PokemonMotionProfile Bat(float weight, float pace, float wings, float hover)
        {
            return Make(PokemonAnatomy.Bat, weight, pace, .60f, 3.2f, 2f, 0f, 1.05f * pace, wings, hover);
        }

        static PokemonMotionProfile Insect(float weight, float pace, float wingRate = 0f,
            float wings = 0f, float hover = 0f)
        {
            return Make(PokemonAnatomy.Insect, weight, pace, .55f, 3.4f, 0f, 0f, wingRate, wings, hover);
        }

        static PokemonMotionProfile Serpent(float weight, float pace, float tail, float hover = 0f)
        {
            return Make(PokemonAnatomy.Serpent, weight, pace, .70f, 3.6f, tail, 0f, 0f, 0f, hover);
        }

        static PokemonMotionProfile Fish(float weight, float pace, float hover = 0f, float tail = 8f)
        {
            return Make(PokemonAnatomy.Fish, weight, pace, .65f, 2.8f, tail, 0f, 0f, 0f, hover);
        }

        static PokemonMotionProfile Floating(float weight, float pace, float hover,
            float tail = 0f, float ears = 0f)
        {
            return Make(PokemonAnatomy.Float, weight, pace, .45f, 3.6f, tail, ears, 0f, 0f, hover);
        }

        static PokemonMotionProfile Plant(float weight, float pace, float sway, float hover = 0f)
        {
            return Make(PokemonAnatomy.Plant, weight, pace, sway, 2.4f, 0f, 0f, 0f, 0f, hover);
        }

        static PokemonMotionProfile Blob(float weight, float pace, float compression)
        {
            return Make(PokemonAnatomy.Blob, weight, pace, compression, 2.4f);
        }

        static PokemonMotionProfile Shell(float weight, float pace)
        {
            return Make(PokemonAnatomy.Shell, weight, pace, .30f, 1.8f);
        }

        static PokemonMotionProfile Make(PokemonAnatomy anatomy, float weight, float pace,
            float breathSize, float look, float tail = 0f, float ears = 0f,
            float wingRate = 0f, float wings = 0f, float hover = 0f)
        {
            // Inertia gently reduces whole-body rotation without flattening the
            // chosen tail/ear/wing silhouette. Attack timing remains readable.
            float inertia = 1f / (1f + .22f * Math.Max(0f, weight - 1f));
            return new PokemonMotionProfile(anatomy, hover, .55f * pace,
                breathSize * inertia, look * inertia, tail, ears, wingRate, wings,
                .93f + .22f * weight, .48f + .12f * weight, 1.10f + .28f * weight, weight);
        }
    }
}
