using System;

namespace AeroStadium.Presentation
{
    /// <summary>
    /// Places the imported visual's anatomical front along actor-local -Z.
    /// Values come from posed local GLB geometry, facial material surface normals
    /// and joint topology. No reference animation poses or curves are copied.
    /// Apply yaw to both Model.localRotation and Model.localPosition before binding.
    /// </summary>
    public static class PokemonFacing
    {
        // Most payloads face +Z. Two imports have a posed head facing +X in
        // Unity coordinates: Onix (head chain endpoints), Flareon (facial patch).
        // Gengar and Geodude also face +Z: their jaw pivots sit behind the face,
        // so jaw-head alone gives the wrong result for those two species.
        public static float NormalizationYaw(int species)
        {
            ValidateSpecies(species);
            return species == 95 || species == 136 ? 90f : 180f;
        }

        /// <summary>Evidence level; 0 = fallback awaiting visual review, 1 = joint/silhouette inference, 2 = facial geometry or direct anatomical direction.</summary>
        public static int EvidenceLevel(int species)
        {
            ValidateSpecies(species);
            switch (species)
            {
                case 10: case 49: case 81: case 82: case 92: case 100:
                case 103: case 109: case 110: case 120: case 132: case 138: case 140:
                    return 0;
                case 11: case 15: case 25: case 41: case 46: case 77: case 121:
                case 131: case 137: case 143:
                    return 1;
                default:
                    return 2;
            }
        }

        private static void ValidateSpecies(int species)
        {
            if (species < 1 || species > 151)
                throw new ArgumentOutOfRangeException(nameof(species), species, "Kanto species 1–151 required.");
        }
    }
}
