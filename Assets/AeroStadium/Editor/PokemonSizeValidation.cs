using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AeroStadium.Presentation;
using UnityEditor;
using UnityEngine;

namespace AeroStadium.EditorTools
{
    /// <summary>Measure actual native body geometry after applying the arena's outer scale.</summary>
    public static class PokemonSizeValidation
    {
        [Serializable] sealed class Report { public int referenceSpecies, referenceForms, models; public bool passed; public Result[] results; public string[] errors; }
        [Serializable] sealed class Result
        {
            public int species, bodyVertices;
            public float pokedexMetres, groupMetres, displayMetres, measuredHeight, measuredFloor, maxHorizontal;
            public bool footprintLimited, passed;
            public string group;
        }
        [MenuItem("AeroStadium/Valider les tailles dans l'arène")]
        public static void VerifyAll()
        {
            var errors=new List<string>();var results=new List<Result>();
            if (PokemonDisplaySize.SpeciesCount<1025) throw new InvalidDataException("Tailles Pokédex incomplètes.");
            if (Mathf.Abs(PokemonDisplaySize.GroupFor(1.3f).displayMetres-1.55f)>.0001f
                || PokemonDisplaySize.GroupFor(1.3f).displayMetres!=PokemonDisplaySize.GroupFor(1.4f).displayMetres
                || PokemonDisplaySize.GroupFor(.2f).displayMetres<.7f
                || PokemonDisplaySize.GroupFor(20f).displayMetres>3.5f)
                throw new InvalidDataException("Les exemples de taille et limites de l'arène ne sont pas respectés.");
            float previous=0f;
            foreach (var entry in PokemonDisplaySize.Entries.OrderBy(e=>e.heightMetres))
            {
                float size=PokemonDisplaySize.GroupFor(entry.heightMetres).displayMetres;
                if (size<previous) throw new InvalidDataException("Inversion des groupes de taille.");
                previous=size;
            }
            for (int id=1;id<=151;id++)
            {
                GameObject actor=null;var result=new Result { species=id };
                try
                {
                    GameObject prefab=PokemonPrefabCatalog.Load(id);
                    if (prefab==null) throw new InvalidDataException("Modèle absent.");
                    actor=(GameObject)PrefabUtility.InstantiatePrefab(prefab);
                    var native=actor.GetComponent<NativePokemonModel>();
                    if (native==null) throw new InvalidDataException("Modèle natif absent.");
                    foreach (var animator in actor.GetComponentsInChildren<Animator>(true)) animator.enabled=false;
                    foreach (var grounding in actor.GetComponentsInChildren<NativeGrounding>(true)) grounding.enabled=false;
                    Transform normal=actor.transform.Find("NativeNormalization");
                    Vector3 referencePosition=normal.localPosition,referenceScale=normal.localScale;
                    Quaternion referenceRotation=normal.localRotation;
                    Bounds original=NativeBodyHeightValidation.MeasureVisibleBodyWorld(native.ModelRoot,out _,out _);
                    var size=PokemonDisplaySize.Apply(actor,id,native.RestBounds,native.ModelHeight);
                    Bounds measured=NativeBodyHeightValidation.MeasureVisibleBodyWorld(native.ModelRoot,out _,out int vertices);
                    result.pokedexMetres=size.pokedexMetres;result.groupMetres=size.groupMetres;
                    result.displayMetres=size.displayMetres;result.group=size.group;result.footprintLimited=size.footprintLimited;
                    result.measuredHeight=measured.size.y;result.measuredFloor=measured.min.y;
                    result.maxHorizontal=Mathf.Max(measured.size.x,measured.size.z);result.bodyVertices=vertices;
                    if (Mathf.Abs(measured.size.y-size.displayMetres)>.002f || Mathf.Abs(measured.min.y)>.002f
                        || result.maxHorizontal>5.502f) throw new InvalidDataException("Taille/sol/emprise mesurés incorrects.");
                    Vector3 expected=original.size*size.uniformScale;
                    if (Vector3.Distance(expected,measured.size)>.002f) throw new InvalidDataException("Proportions du modèle modifiées.");
                    if (normal.localPosition!=referencePosition || normal.localScale!=referenceScale
                        || Quaternion.Angle(normal.localRotation,referenceRotation)>.0001f)
                        throw new InvalidDataException("Normalisation native modifiée par la présentation.");
                    if (Mathf.Abs(native.WorldModelHeight-size.displayMetres)>.0001f)
                        throw new InvalidDataException("Hauteur mondiale des effets incorrecte.");
                    result.passed=true;
                }
                catch (Exception exception) { errors.Add(id.ToString("000")+": "+exception.Message); }
                finally { if (actor!=null) UnityEngine.Object.DestroyImmediate(actor); }
                results.Add(result);
            }
            if (Mathf.Abs(results.Find(r=>r.species==31).displayMetres-results.Find(r=>r.species==34).displayMetres)>.0001f)
                errors.Add("Nidoqueen/Nidoking doivent partager le même groupe.");
            var report=new Report { referenceSpecies=PokemonDisplaySize.SpeciesCount,referenceForms=PokemonDisplaySize.FormCount,
                models=results.Count,passed=errors.Count==0,results=results.ToArray(),errors=errors.ToArray() };
            string directory=Path.Combine(Directory.GetParent(Application.dataPath).FullName,"output","animations","pokedex-sizing");
            Directory.CreateDirectory(directory);
            File.WriteAllText(Path.Combine(directory,"unity-display-size-validation.json"),JsonUtility.ToJson(report,true));
            Debug.Log($"[pokemon-size-validation] references={report.referenceSpecies} forms={report.referenceForms} models={report.models} passed={report.passed}");
            if (!report.passed) throw new InvalidDataException(string.Join("\n",errors));
        }
    }
}
