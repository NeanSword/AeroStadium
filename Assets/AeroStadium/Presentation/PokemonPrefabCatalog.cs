using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>The isolated native import takes precedence while the old local catalog remains available.</summary>
    public static class PokemonPrefabCatalog
    {
        public static GameObject Load(int species)
        {
            if (species < 1 || species > 151) return null;
            return Resources.Load<GameObject>($"NativeModels/{species:000}/Pokemon")
                ?? Resources.Load<GameObject>($"LocalModels/{species}/Pokemon");
        }
    }
}
