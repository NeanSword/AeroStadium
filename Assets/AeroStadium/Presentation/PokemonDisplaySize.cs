using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Factual Pokedex dimensions and arena presentation dimensions stay separate.</summary>
    public static class PokemonDisplaySize
    {
        [Serializable] public sealed class HeightEntry { public int species; public string name; public float heightMetres; }
        [Serializable] sealed class Heights { public int schemaVersion; public HeightEntry[] species, forms; }
        [Serializable] public sealed class Band { public float maximumPokedexMetres, displayMetres; public string name; }
        [Serializable] sealed class Policy { public int schemaVersion; public float maximumHorizontalMetres; public Band[] bands; }
        public readonly struct AppliedSize
        {
            public readonly float pokedexMetres, groupMetres, displayMetres, uniformScale;
            public readonly string group;
            public readonly bool footprintLimited;
            internal AppliedSize(float factual, Band band, float height, float scale, bool limited)
            { pokedexMetres=factual; groupMetres=band.displayMetres; displayMetres=height; uniformScale=scale; group=band.name; footprintLimited=limited; }
        }
        static Heights heights;
        static Policy policy;
        static Dictionary<int,HeightEntry> bySpecies;
        public static int SpeciesCount { get { EnsureLoaded(); return heights.species.Length; } }
        public static int FormCount { get { EnsureLoaded(); return heights.forms.Length; } }
        public static IReadOnlyList<HeightEntry> Entries { get { EnsureLoaded(); return heights.species; } }

        static void EnsureLoaded()
        {
            if (bySpecies != null) return;
            TextAsset source=Resources.Load<TextAsset>("Data/pokemon-heights");
            TextAsset sizing=Resources.Load<TextAsset>("Data/pokemon-display-size-policy");
            if (source==null || sizing==null) throw new InvalidOperationException("Données de taille des Pokémon absentes.");
            var data=JsonUtility.FromJson<Heights>(source.text);
            var settings=JsonUtility.FromJson<Policy>(sizing.text);
            if (data==null || data.schemaVersion!=1 || data.species==null || data.forms==null
                || settings==null || settings.schemaVersion!=1 || settings.bands==null || settings.bands.Length==0)
                throw new InvalidOperationException("Format des tailles Pokémon invalide.");
            float maximum=0f,display=0f;
            foreach (Band band in settings.bands)
            {
                if (band==null || !Finite(band.maximumPokedexMetres) || !Finite(band.displayMetres)
                    || band.maximumPokedexMetres<=maximum || band.displayMetres<display || band.displayMetres<=0f)
                    throw new InvalidOperationException("Groupes de taille Pokémon invalides.");
                maximum=band.maximumPokedexMetres;display=band.displayMetres;
            }
            if (!Finite(settings.maximumHorizontalMetres) || settings.maximumHorizontalMetres<=0f)
                throw new InvalidOperationException("Limite du terrain invalide.");
            var index=new Dictionary<int,HeightEntry>();
            foreach (HeightEntry entry in data.species)
            {
                if (entry==null || entry.species<=0 || index.ContainsKey(entry.species)
                    || string.IsNullOrWhiteSpace(entry.name) || !Finite(entry.heightMetres) || entry.heightMetres<=0f)
                    throw new InvalidOperationException("Taille Pokédex invalide.");
                index.Add(entry.species,entry);
            }
            heights=data;policy=settings;bySpecies=index;
        }

        public static HeightEntry Reference(int species, string formName=null)
        {
            EnsureLoaded();
            if (!string.IsNullOrEmpty(formName))
            {
                foreach (HeightEntry form in heights.forms)
                    if (form.species==species && string.Equals(form.name,formName,StringComparison.Ordinal)) return form;
                throw new ArgumentException("Forme Pokémon absente des tailles : "+formName);
            }
            if (!bySpecies.TryGetValue(species,out HeightEntry entry))
                throw new ArgumentOutOfRangeException(nameof(species),"Espèce absente des tailles Pokédex.");
            return entry;
        }

        public static Band GroupFor(float pokedexHeight)
        {
            EnsureLoaded();
            if (!Finite(pokedexHeight) || pokedexHeight<=0f) throw new ArgumentOutOfRangeException(nameof(pokedexHeight));
            foreach (Band band in policy.bands) if (pokedexHeight<=band.maximumPokedexMetres+.00001f) return band;
            return policy.bands[policy.bands.Length-1];
        }

        public static AppliedSize Calculate(int species, Bounds localBounds, float importedHeight, string formName=null)
        {
            if (!Finite(importedHeight) || importedHeight<=0f || !Finite(localBounds.size.x)
                || !Finite(localBounds.size.z) || localBounds.size.y<=0f) throw new ArgumentException("Dimensions importées invalides.");
            HeightEntry entry=Reference(species,formName);Band band=GroupFor(entry.heightMetres);
            float scale=band.displayMetres/importedHeight;
            float horizontal=Mathf.Max(localBounds.size.x,localBounds.size.z)*scale;
            bool limited=horizontal>policy.maximumHorizontalMetres;
            if (limited) scale*=policy.maximumHorizontalMetres/horizontal;
            return new AppliedSize(entry.heightMetres,band,importedHeight*scale,scale,limited);
        }

        public static AppliedSize Apply(GameObject actor,int species,Bounds localBounds,float importedHeight,string formName=null)
        {
            if (actor==null) throw new ArgumentNullException(nameof(actor));
            AppliedSize size=Calculate(species,localBounds,importedHeight,formName);
            // The outer actor is unanimated. NativeNormalization and cached grounding stay intact.
            actor.transform.localScale=Vector3.one*size.uniformScale;
            return size;
        }

        public static Bounds WorldBounds(Transform actor,Bounds local)
        {
            Bounds world=new Bounds(actor.TransformPoint(local.center),Vector3.zero);
            for (int i=0;i<8;i++) world.Encapsulate(actor.TransformPoint(local.center+Vector3.Scale(local.extents,
                new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            return world;
        }
        static bool Finite(float value)=>!float.IsNaN(value)&&!float.IsInfinity(value);
    }
}
