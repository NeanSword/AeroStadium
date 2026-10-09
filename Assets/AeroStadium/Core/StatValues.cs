using System;

namespace AeroStadium.Core
{
    /// <summary>Six individual or effort values; zero is an explicit valid value.</summary>
    [Serializable]
    public sealed class StatValues
    {
        public int hp, attack, defense, specialAttack, specialDefense, speed;
        public StatValues() { }
        public StatValues(int value)
        {
            hp=attack=defense=specialAttack=specialDefense=speed=value;
        }
        public StatValues Copy()
        {
            return new StatValues { hp=hp, attack=attack, defense=defense,
                specialAttack=specialAttack, specialDefense=specialDefense, speed=speed };
        }
        internal void Validate(int maximum, int totalLimit, string parameter)
        {
            int[] values={hp,attack,defense,specialAttack,specialDefense,speed};
            int total=0;
            foreach(int value in values) {
                if(value<0||value>maximum)
                    throw new ArgumentOutOfRangeException(parameter,"Each value must be between 0 and "+maximum+".");
                total+=value;
            }
            if(totalLimit>=0&&total>totalLimit)
                throw new ArgumentException("The total cannot exceed "+totalLimit+".",parameter);
        }
    }
}
