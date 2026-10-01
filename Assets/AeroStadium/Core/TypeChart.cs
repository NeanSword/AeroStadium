using System;
using System.Collections.Generic;

namespace AeroStadium.Core
{
    /// <summary>18 ordinary types, current chart; Stellar/Terastallization is outside this prototype.</summary>
    public static class TypeChart
    {
        // Matchups checked against https://github.com/smogon/pokemon-showdown/blob/master/data/typechart.ts
        private static readonly string[] Names = { "Normal", "Fire", "Water", "Electric", "Grass", "Ice", "Fighting", "Poison", "Ground", "Flying", "Psychic", "Bug", "Rock", "Ghost", "Dragon", "Dark", "Steel", "Fairy" };
        private static readonly Dictionary<string, int> Indices = CreateIndices();
        private static readonly double[,] Chart = CreateChart();
        public static IReadOnlyList<string> Types { get { return Array.AsReadOnly(Names); } }
        public static bool IsKnown(string type) { return type != null && Indices.ContainsKey(type); }
        public static double Effectiveness(string attackType, params string[] defenseTypes)
        {
            int attack = Index(attackType);
            if (defenseTypes == null || defenseTypes.Length == 0 || defenseTypes.Length > 2)
                throw new ArgumentException("A defender needs one or two types.");
            double result = 1;
            foreach (string defense in defenseTypes) result *= Chart[attack, Index(defense)];
            return result;
        }
        private static int Index(string type)
        {
            int result;
            if (type == null || !Indices.TryGetValue(type, out result)) throw new ArgumentException("Unknown type: " + type);
            return result;
        }
        private static Dictionary<string, int> CreateIndices()
        {
            var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < Names.Length; i++) result.Add(Names[i], i);
            return result;
        }
        private static double[,] CreateChart()
        {
            var chart = new double[18, 18];
            for (int a = 0; a < 18; a++) for (int d = 0; d < 18; d++) chart[a, d] = 1;
            Set(chart, "Normal", "", "Rock Steel", "Ghost");
            Set(chart, "Fire", "Grass Ice Bug Steel", "Fire Water Rock Dragon", "");
            Set(chart, "Water", "Fire Ground Rock", "Water Grass Dragon", "");
            Set(chart, "Electric", "Water Flying", "Electric Grass Dragon", "Ground");
            Set(chart, "Grass", "Water Ground Rock", "Fire Grass Poison Flying Bug Dragon Steel", "");
            Set(chart, "Ice", "Grass Ground Flying Dragon", "Fire Water Ice Steel", "");
            Set(chart, "Fighting", "Normal Ice Rock Dark Steel", "Poison Flying Psychic Bug Fairy", "Ghost");
            Set(chart, "Poison", "Grass Fairy", "Poison Ground Rock Ghost", "Steel");
            Set(chart, "Ground", "Fire Electric Poison Rock Steel", "Grass Bug", "Flying");
            Set(chart, "Flying", "Grass Fighting Bug", "Electric Rock Steel", "");
            Set(chart, "Psychic", "Fighting Poison", "Psychic Steel", "Dark");
            Set(chart, "Bug", "Grass Psychic Dark", "Fire Fighting Poison Flying Ghost Steel Fairy", "");
            Set(chart, "Rock", "Fire Ice Flying Bug", "Fighting Ground Steel", "");
            Set(chart, "Ghost", "Psychic Ghost", "Dark", "Normal");
            Set(chart, "Dragon", "Dragon", "Steel", "Fairy");
            Set(chart, "Dark", "Psychic Ghost", "Fighting Dark Fairy", "");
            Set(chart, "Steel", "Ice Rock Fairy", "Fire Water Electric Steel", "");
            Set(chart, "Fairy", "Fighting Dragon Dark", "Fire Poison Steel", "");
            return chart;
        }
        private static void Set(double[,] chart, string attack, string strong, string resisted, string immune)
        {
            foreach (string type in strong.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) chart[Index(attack), Index(type)] = 2;
            foreach (string type in resisted.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) chart[Index(attack), Index(type)] = .5;
            foreach (string type in immune.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)) chart[Index(attack), Index(type)] = 0;
        }
    }
}
