using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    [Serializable]
    public sealed class BiteTorsoTelemetry
    {
        public bool baselineCaptured, finite = true, restored = true, nativeAnimatorPreserved = true;
        public string baselineState;
        public int posedFrames, articulatedFrames, supportFrames;
        public float maxJointDegrees, maxSupportError, maxCrouch, maxForwardReach;
        public float maxNativeHeadError, maxNativeJawError, maxRestorePositionError, maxRestoreAngleError;
    }

    /// <summary>Temporary 059 bite support and torso performance; native Head/Jaw keep their authored motion.</summary>
    [DefaultExecutionOrder(92)]
    [DisallowMultipleComponent]
    public sealed class BiteTorsoPose : MonoBehaviour
    {
        sealed class Joint
        {
            public Transform bone;
            public Vector3 position, referencePosition;
            public Quaternion rotation, referenceRotation;
            public float shape;
        }
        sealed class Limb
        {
            public Joint upper, middle, tip;
            public Vector3 referenceFoot, referenceBend;
            public Quaternion referenceOrientation;
            public bool front;
            public float side;
        }
        static readonly int IdleState = Animator.StringToHash("Base Layer.Native_003");
        static readonly int BiteState = Animator.StringToHash("Base Layer.Native_005");
        readonly List<Joint> joints = new List<Joint>();
        readonly List<Limb> limbs = new List<Limb>();
        Transform actor, waist, head, jaw;
        Animator animator;
        RuntimeAnimatorController controller;
        Joint hip;
        Vector3 facingLocal, biteFacing, biteRight, biteHome;
        float height, releasedAt, lastTime;
        bool active, biting, releasing, applied, animatorEnabled;
        public BiteTorsoTelemetry Stats { get; private set; } = new BiteTorsoTelemetry();
        public bool IsClean => !active && !applied;

        public static BiteTorsoPose Begin(Transform source, NativePokemonModel native)
        {
            if (source == null || native == null || native.Species != 59 || native.ModelRoot == null) return null;
            var layer = source.GetComponent<BiteTorsoPose>();
            if (layer == null) layer = source.gameObject.AddComponent<BiteTorsoPose>();
            layer.Configure(source, native);
            return layer;
        }

        void Configure(Transform source, NativePokemonModel native)
        {
            End(); joints.Clear(); limbs.Clear();
            Stats = new BiteTorsoTelemetry(); actor = source;
            animator = native.ModelRoot.GetComponent<Animator>();
            if (animator == null) return;
            controller = animator.runtimeAnimatorController; animatorEnabled = animator.enabled;
            height = Mathf.Max(.2f, native.WorldModelHeight);
            var bones = new Dictionary<string, Transform>(StringComparer.Ordinal);
            foreach (var bone in native.ModelRoot.GetComponentsInChildren<Transform>(true))
                if (!bones.ContainsKey(bone.name)) bones.Add(bone.name, bone);
            Transform Find(string name) => bones.TryGetValue(name, out var bone) ? bone : null;
            waist = Find("Waist"); head = Find("Head"); jaw = Find("Jaw");
            Transform mouth = Find("EffMouth01");
            if (waist == null || head == null || jaw == null || mouth == null) return;

            // Exact waitA01/003 t=0 references. 000 is a landing clip, not the idle stance.
            hip = Add("Waist", new Quaternion(.48964405f,.5194848f,-.5055281f,.484588f), .88f);
            hip.referencePosition = new Vector3(-.017524004f,.87881476f,-.13839959f);
            Add("Spine1", Quaternion.identity, .92f);
            Add("Spine2", new Quaternion(-.002483357f,-.24894588f,.03177766f,.9679927f), .92f);
            Add("Neck", new Quaternion(.0034354744f,-.37319314f,-.007384663f,.9277179f), .88f);
            Add("Hips", new Quaternion(.015182478f,0,.9998847f,0), .84f);
            AddLimb("LArm","LForeArm","LHand", true,
                new Quaternion(-.05339352f,-.1352944f,.16004822f,.9763345f),
                new Quaternion(0,.30253977f,0,.95313674f),
                new Quaternion(-.09991787f,.58262956f,.006634205f,.8065452f));
            AddLimb("RArm","RForeArm","RHand", true,
                new Quaternion(.054225218f,-.009099857f,-.23326206f,.9708582f),
                new Quaternion(0,.20605263f,0,.9785409f),
                new Quaternion(.023712996f,.57106596f,-.018732587f,.8203477f));
            AddLimb("LThigh","LLeg","LFoot", false,
                new Quaternion(-.13648462f,.67172396f,-.07686043f,.7240521f),
                new Quaternion(0,-.27813515f,0,.96054196f),
                new Quaternion(-.06362887f,.8952904f,.086894624f,.43226823f));
            AddLimb("RThigh","RLeg","RFoot", false,
                new Quaternion(.06708066f,.66897076f,.025111077f,.7398295f),
                new Quaternion(0,-.37527785f,0,.92691237f),
                new Quaternion(.026411664f,.9096054f,-.024197951f,.41392627f));
            if (joints.Count != 17 || limbs.Count != 4) { joints.Clear(); limbs.Clear(); return; }
            joints.Sort((a,b) => Depth(a.bone).CompareTo(Depth(b.bone)));
            facingLocal = actor.InverseTransformDirection(Vector3.ProjectOnPlane(mouth.position-waist.position,Vector3.up).normalized);
            if (facingLocal.sqrMagnitude < .5f) facingLocal = Vector3.back;
            // Obtain support goals in the exact authored idle reference without evaluating or changing Animator state.
            SaveLayer();
            foreach (var joint in joints) joint.bone.SetLocalPositionAndRotation(joint.referencePosition,joint.referenceRotation);
            CaptureSupports(); RestoreLayer();
            Stats.baselineState = "Native_003 authored waitA01 t=0";
            active = true; biting = releasing = false; Stats.restored = false; enabled = true;
            CaptureIdle();

            Joint Add(string name, Quaternion reference, float shape)
            {
                Transform bone = Find(name); if (bone == null) return null;
                var joint = new Joint { bone=bone, referencePosition=bone.localPosition, referenceRotation=reference, shape=shape };
                joints.Add(joint); return joint;
            }
            void AddLimb(string upper, string middle, string tip, bool front, Quaternion a, Quaternion b, Quaternion c)
            {
                Joint u=Add(upper,a,.80f), m=Add(middle,b,.80f), t=Add(tip,c,.80f);
                if (u != null && m != null && t != null && m.bone.IsChildOf(u.bone) && t.bone.IsChildOf(m.bone))
                    limbs.Add(new Limb { upper=u, middle=m, tip=t, front=front });
            }
        }

        // Animator evaluates after Update. Never feed the previous frame's temporary pose back into it.
        void Update() { RestoreLayer(); }

        void CaptureIdle()
        {
            if (biting || animator == null || animator.IsInTransition(0) || animator.GetCurrentAnimatorStateInfo(0).fullPathHash != IdleState) return;
            foreach (var joint in joints)
            { joint.referencePosition=joint.bone.localPosition; joint.referenceRotation=joint.bone.localRotation; }
            CaptureSupports(); Stats.baselineCaptured=true; Stats.baselineState="Native_003 observed during ignition";
        }
        void CaptureSupports()
        {
            Vector3 right=Vector3.Cross(Vector3.up,actor.TransformDirection(facingLocal)).normalized;
            foreach (var limb in limbs)
            {
                limb.referenceFoot=actor.InverseTransformPoint(limb.tip.bone.position);
                limb.referenceBend=actor.InverseTransformDirection(limb.middle.bone.position-limb.upper.bone.position);
                limb.referenceOrientation=Quaternion.Inverse(actor.rotation)*limb.tip.bone.rotation;
                limb.side=Vector3.Dot(limb.tip.bone.position-waist.position,right)<0 ? -1 : 1;
            }
        }
        public void BeginBite()
        {
            if (!active) return;
            biting=true; releasing=false; lastTime=0;
            biteHome=actor.position;
            biteFacing=Vector3.ProjectOnPlane(actor.TransformDirection(facingLocal),Vector3.up).normalized;
            biteRight=Vector3.Cross(Vector3.up,biteFacing).normalized;
        }
        public void BeginReturn()
        {
            if (!active || !biting) return;
            releasing=true; releasedAt=Time.time;
        }

        void LateUpdate()
        {
            if (!active) return;
            if (actor == null || animator == null || !actor.gameObject.activeInHierarchy) { End(); return; }
            if (!biting) { CaptureIdle(); return; }
            float release=releasing?Smooth(0,.16f,Time.time-releasedAt):0;
            if (release >= 1) { End(); return; }
            var state=animator.GetCurrentAnimatorStateInfo(0);
            if (animator.IsInTransition(0))
            {var next=animator.GetNextAnimatorStateInfo(0);if(next.fullPathHash==BiteState)state=next;}
            if (state.fullPathHash==BiteState) lastTime=state.normalizedTime*state.length;
            else if (!releasing) return;
            float weight=Smooth(0,.14f,lastTime)*(1-release);
            if (weight <= .0001f) return;
            Apply(lastTime,weight,release);
        }

        void Apply(float t,float weight,float release)
        {
            SaveLayer();
            Quaternion nativeHead=head.localRotation, nativeJaw=jaw.localRotation;
            Vector3 nativeHeadPosition=head.localPosition, nativeJawPosition=jaw.localPosition;
            // Settle, extend through the close, then withdraw. This shapes only the distributed body curl.
            float load=Smooth(.03f,.22f,t)*(1-Smooth(.63f,.88f,t));
            float thrust=Smooth(.20f,.48f,t)*(1-Smooth(.60f,.86f,t));
            float recoil=Smooth(.59f,.73f,t)*(1-Smooth(.79f,.94f,t));
            float crouch=height*(.022f*load+.014f*thrust);
            Vector3 offset=biteFacing*(height*(.045f*thrust-.018f*recoil))-Vector3.up*crouch;
            foreach (var joint in joints)
                joint.bone.localRotation=Quaternion.Slerp(joint.rotation,joint.referenceRotation,joint.shape*weight);
            waist.localPosition=Vector3.Lerp(hip.position,hip.referencePosition+waist.parent.InverseTransformVector(offset),.97f*weight);
            waist.rotation=Quaternion.AngleAxis((2.5f*thrust-1.5f*recoil)*weight,biteRight)*waist.rotation;
            foreach (var limb in limbs)
            {
                // Four independent support chains keep the lowered chest above its paws. Forepaws catch
                // the lunge while hind paws push; feet are goals, never translated bones or scaled limbs.
                Vector3 stance=actor.TransformPoint(limb.referenceFoot);
                Vector3 planted=stance-(actor.position-biteHome);
                Vector3 goal=Vector3.Lerp(planted,stance,release);
                goal+=biteFacing*height*(limb.front?.060f*Smooth(.12f,.36f,t):-.012f*thrust);
                goal+=biteRight*height*.010f*limb.side*load;
                float lift=limb.front?height*.014f*Smooth(.10f,.18f,t)*(1-Smooth(.24f,.35f,t)):0;
                goal.y=stance.y+lift;
                Solve(limb,goal,weight);
                limb.tip.bone.rotation=Quaternion.Slerp(limb.tip.bone.rotation,actor.rotation*limb.referenceOrientation,weight*.94f);
            }
            int articulated=0;
            foreach (var joint in joints)
            {
                float angle=Quaternion.Angle(joint.rotation,joint.bone.localRotation);
                Stats.maxJointDegrees=Mathf.Max(Stats.maxJointDegrees,angle);
                if (angle>.5f) articulated++;
                Stats.finite &= Finite(joint.bone.localPosition) && Finite(joint.bone.localRotation);
            }
            Stats.maxNativeHeadError=Mathf.Max(Stats.maxNativeHeadError,Quaternion.Angle(nativeHead,head.localRotation),Vector3.Distance(nativeHeadPosition,head.localPosition));
            Stats.maxNativeJawError=Mathf.Max(Stats.maxNativeJawError,Quaternion.Angle(nativeJaw,jaw.localRotation),Vector3.Distance(nativeJawPosition,jaw.localPosition));
            Stats.maxCrouch=Mathf.Max(Stats.maxCrouch,crouch*weight); Stats.maxForwardReach=Mathf.Max(Stats.maxForwardReach,Vector3.Dot(offset,biteFacing)*weight);
            Stats.nativeAnimatorPreserved &= animator.runtimeAnimatorController==controller && animator.enabled==animatorEnabled;
            Stats.posedFrames++; if (articulated>=3) Stats.articulatedFrames++; Stats.supportFrames++;
            if (!Stats.finite) End();
        }

        void Solve(Limb limb,Vector3 requested,float weight)
        {
            Transform upper=limb.upper.bone,middle=limb.middle.bone,tip=limb.tip.bone;
            Vector3 root=upper.position,knee=middle.position,end=tip.position;
            float a=Vector3.Distance(root,knee),b=Vector3.Distance(knee,end);
            Vector3 vector=requested-root; float length=vector.magnitude;
            if (a<.00001f || b<.00001f || length<.00001f) return;
            Vector3 aim=vector/length;
            float reach=Mathf.Clamp(length,Mathf.Abs(a-b)+.00001f,(a+b)*.995f);
            Vector3 goal=root+aim*reach;
            Vector3 bend=Vector3.ProjectOnPlane(actor.TransformDirection(limb.referenceBend),aim);
            if (bend.sqrMagnitude<.000001f) bend=Vector3.ProjectOnPlane(knee-root,aim);
            if (bend.sqrMagnitude<.000001f) return;
            float along=(a*a+reach*reach-b*b)/(2*reach);
            Vector3 hinge=root+aim*along+bend.normalized*Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
            upper.rotation=Quaternion.Slerp(upper.rotation,Quaternion.FromToRotation(knee-root,hinge-root)*upper.rotation,weight);
            middle.rotation=Quaternion.Slerp(middle.rotation,Quaternion.FromToRotation(tip.position-middle.position,goal-middle.position)*middle.rotation,weight);
            Stats.maxSupportError=Mathf.Max(Stats.maxSupportError,Vector3.Distance(tip.position,requested)*weight);
        }
        void SaveLayer()
        {
            foreach (var joint in joints)
            {joint.position=joint.bone.localPosition;joint.rotation=joint.bone.localRotation;}
            applied=true;
        }
        void RestoreLayer()
        {
            if (!applied) return;
            foreach (var joint in joints)
            {
                if (joint.bone==null) continue;
                joint.bone.SetLocalPositionAndRotation(joint.position,joint.rotation);
                Stats.maxRestorePositionError=Mathf.Max(Stats.maxRestorePositionError,Vector3.Distance(joint.position,joint.bone.localPosition));
                Stats.maxRestoreAngleError=Mathf.Max(Stats.maxRestoreAngleError,Quaternion.Angle(joint.rotation,joint.bone.localRotation));
            }
            applied=false;
        }
        public void End()
        {
            active=biting=releasing=false; RestoreLayer(); Stats.restored=true;
            if (enabled) enabled=false;
        }
        static int Depth(Transform bone) { int n=0;while(bone.parent!=null){n++;bone=bone.parent;}return n; }
        static float Smooth(float a,float b,float t)=>Mathf.SmoothStep(0,1,Mathf.InverseLerp(a,b,t));
        static bool Finite(Vector3 v)=>Finite(v.x)&&Finite(v.y)&&Finite(v.z);
        static bool Finite(Quaternion q)=>Finite(q.x)&&Finite(q.y)&&Finite(q.z)&&Finite(q.w);
        static bool Finite(float v)=>!float.IsNaN(v)&&!float.IsInfinity(v);
        void OnDisable() { End(); }
        void OnDestroy() { End(); }
    }
}
