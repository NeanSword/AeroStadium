using UnityEngine;
namespace AeroStadium.Presentation
{
    [DefaultExecutionOrder(90)]
    public sealed class BattleDodgeMotionTick : MonoBehaviour
    {
        internal BattleActionDirector Owner;
        void LateUpdate(){if(Owner!=null)Owner.UpdateDodgeMotion();}
    }
}
