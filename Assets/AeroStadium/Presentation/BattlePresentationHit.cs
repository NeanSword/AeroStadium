using System.Collections.Generic;
using AeroStadium.Core;

namespace AeroStadium.Presentation
{
    /// <summary>Matches visual contact to the resolved damage event of one move.</summary>
    public static class BattlePresentationHit
    {
        public static bool TryGetDirectHit(IReadOnlyList<BattleEvent> events, int moveUsedIndex, out int damageIndex)
        {
            damageIndex = -1;
            if (events == null || moveUsedIndex < 0 || moveUsedIndex >= events.Count) return false;
            BattleEvent move = events[moveUsedIndex];
            if (move == null || move.Kind != BattleEventKind.MoveUsed || move.Side < 0 || move.Side > 1) return false;
            for (int i = moveUsedIndex + 1; i < events.Count; i++)
            {
                BattleEvent next = events[i];
                if (next == null) continue;
                if (next.Kind == BattleEventKind.MoveUsed || next.Kind == BattleEventKind.Switched) break;
                if (next.Kind == BattleEventKind.Damage && next.Side == move.Side &&
                    next.TargetSide == 1 - move.Side && next.MoveId == move.MoveId && next.Amount > 0)
                {
                    damageIndex = i;
                    return true;
                }
            }
            return false;
        }
        /// <summary>Returns the multiplier recorded on the exact resolved contact event.</summary>
        public static bool TryGetDirectHitEffectiveness(IReadOnlyList<BattleEvent> events, int moveUsedIndex,
            out int damageIndex, out double effectiveness)
        {
            effectiveness = 0d;
            if (!TryGetDirectHit(events, moveUsedIndex, out damageIndex)) return false;
            effectiveness = GetDamageEffectiveness(events[damageIndex]);
            return true;
        }
        public static double GetDamageEffectiveness(BattleEvent damage)
        {
            return damage != null && damage.Kind == BattleEventKind.Damage && damage.Amount > 0
                ? damage.HitEffectiveness : 0d;
        }
        public static bool TryGetProtectActivation(IReadOnlyList<BattleEvent> events,int index,out int eventIndex)
            => FindProtection(events,index,true,out eventIndex,out _);
        public static bool TryGetBlockedAttack(IReadOnlyList<BattleEvent> events,int index,out int eventIndex,out int defender)
            => FindProtection(events,index,false,out eventIndex,out defender);
        static bool FindProtection(IReadOnlyList<BattleEvent> events,int index,bool activation,out int eventIndex,out int defender)
        {
            eventIndex=defender=-1;
            if(events==null||index<0||index>=events.Count)return false;
            var used=events[index];
            if(used==null||used.Kind!=BattleEventKind.MoveUsed||used.Side<0||used.Side>1)return false;
            if(activation!=(used.MoveId==182))return false;
            int side=activation?used.Side:1-used.Side;
            for(int i=index+1;i<events.Count;i++){
                var next=events[i];if(next==null)continue;
                if(next.Kind==BattleEventKind.MoveUsed||next.Kind==BattleEventKind.Switched)break;
                if(next.Kind==BattleEventKind.Failed||next.Kind==BattleEventKind.Miss||next.Kind==BattleEventKind.Immune)return false;
                if(next.Kind==BattleEventKind.Protected&&next.Side==side){eventIndex=i;defender=side;return true;}
            }
            return false;
        }
    }
}
