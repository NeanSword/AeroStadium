using System;
using System.IO;
using UnityEditor;

namespace AeroStadium.EditorTools
{
    /// <summary>Keep the old fallback catalogue locally, but exclude it from a complete native build.</summary>
    sealed class NativeOnlyBuildScope : IDisposable
    {
        const string Legacy = "Assets/AeroStadium/Resources/LocalModels";
        const string Hold = "Assets/AeroStadium/LegacyModelsBuildHold";
        bool moved;
        public NativeOnlyBuildScope()
        {
            if (!Directory.Exists(Legacy)) return;
            if (!Directory.Exists("Assets/AeroStadium/Resources/NativeModels")) return;
            for (int id = 1; id <= 151; id++)
                if (!File.Exists($"Assets/AeroStadium/Resources/NativeModels/{id:000}/Pokemon.prefab"))
                    throw new InvalidDataException("Catalogue natif incomplet ; conserver le catalogue de secours.");
            if (Directory.Exists(Hold)) throw new IOException("Ancienne compilation interrompue : restaurer LegacyModelsBuildHold avant de poursuivre.");
            string error = AssetDatabase.MoveAsset(Legacy, Hold);
            if (!string.IsNullOrEmpty(error)) throw new IOException(error);
            moved = true;
            try { NativeAssetMemoryOptimization.ReleaseEditorCache(); }
            catch (Exception purgeFailure)
            {
                // A throwing constructor never reaches the caller's using/finally.
                // Restore the catalogue here, preserving both failures if restoration also fails.
                try { Dispose(); }
                catch (Exception restoreFailure)
                {
                    throw new AggregateException("Purge et restauration du catalogue local impossibles.", purgeFailure, restoreFailure);
                }
                throw;
            }
        }
        public void Dispose()
        {
            if (!moved) return;
            string error = AssetDatabase.MoveAsset(Hold, Legacy);
            if (!string.IsNullOrEmpty(error)) throw new IOException("Restauration du catalogue local impossible : " + error);
            moved = false;
        }
    }
}
