using System.Collections;
using UnityEngine;

namespace AeroStadium.Presentation
{
    [System.Serializable] public sealed class SurfCameraTelemetry
    {
        public bool planned,entered,recovered,interrupted,safe=true;
        public int followFrames,rearFrames,actorVisibleFrames;
        public float maxSpeed,maxAngularSpeed,maxFovSpeed,travel,entrySeconds,recoverySeconds;
    }

    /// <summary>One continuous, clearance-checked rear tracking shot. No actor or camera ownership.</summary>
    internal sealed class SurfCameraTrack
    {
        readonly ArenaView arena;readonly Camera camera;readonly int side;
        readonly Transform rider;
        readonly Vector3 home,homeFocus,pivot,forward,lateral;
        readonly Quaternion homeRotation;readonly float homeFov;
        readonly Collider[] nearby=new Collider[32];readonly RaycastHit[] hits=new RaycastHit[32];
        Vector3 rear,entryFocus,look,positionVelocity,lookVelocity;
        float height,distance,fovVelocity;
        bool canceled,transition;
        public bool Transitioning=>transition;
        public SurfCameraTelemetry Stats {get;}=new SurfCameraTelemetry();

        public SurfCameraTrack(ArenaView view,int attackerSide,Transform source)
        {
            arena=view;side=attackerSide;rider=source;camera=view.ArenaCamera;
            home=camera.transform.position;homeRotation=camera.transform.rotation;homeFov=camera.fieldOfView;
            Bounds body=view.PokemonWorldBounds(side),victim=view.PokemonWorldBounds(1-side);
            forward=Vector3.ProjectOnPlane(victim.center-body.center,Vector3.up).normalized;
            if(forward.sqrMagnitude<.5f)forward=side==0?view.BattleAxis:-view.BattleAxis;
            lateral=Vector3.Cross(Vector3.up,view.BattleAxis).normalized;
            pivot=body.center+Vector3.up*.35f;
            homeFocus=home+camera.transform.forward*Vector3.Distance(home,pivot);
            distance=Mathf.Clamp(Mathf.Max(body.extents.x,body.extents.z)*2.7f+2.8f,6f,8f);
            entryFocus=Focus(body,victim);
            for(int attempt=0;attempt<4;attempt++)
            {
                height=2.1f+attempt*.8f;
                rear=body.center-forward*distance+lateral*1.8f+Vector3.up*height;
                if(Clear(rear,entryFocus)&&ArcClear(home,rear,homeFocus,entryFocus)) {Stats.planned=true;break;}
            }
        }

        Vector3 Focus(Bounds body,Bounds victim)
        {
            // Look beyond the rider, above the centre of the water mass; keep its back in the foreground.
            return Vector3.Lerp(body.center,victim.center,.53f)+Vector3.up*.70f;
        }
        static float Ease(float t){t=Mathf.Clamp01(t);return t*t*t*(t*(t*6-15)+10);}
        Vector3 Arc(Vector3 from,Vector3 to,float t)
        {
            Vector3 a=from-pivot,b=to-pivot;
            return pivot+Vector3.Slerp(a.normalized,b.normalized,t)*Mathf.Lerp(a.magnitude,b.magnitude,t);
        }
        bool ArcClear(Vector3 from,Vector3 to,Vector3 fromFocus,Vector3 toFocus)
        {
            Vector3 previous=from;
            for(int i=1;i<=24;i++)
            {
                float q=i/24f;Vector3 p=Arc(from,to,q),focus=Vector3.Lerp(fromFocus,toFocus,q);
                if(!Clear(p,focus)||!TravelClear(previous,p))return false;
                previous=p;
            }return true;
        }
        public IEnumerator Enter()
        {
            if(!Stats.planned||canceled)yield break;
            transition=true;float began=Time.unscaledTime;
            try
            {
                for(float t=0;t<1.65f&&!canceled;t+=Time.unscaledDeltaTime)
                {
                    float q=Ease(t/1.65f);Vector3 p=Arc(home,rear,q),focus=Vector3.Lerp(homeFocus,entryFocus,q);
                    Quaternion rotation=Quaternion.Slerp(homeRotation,Quaternion.LookRotation(focus-p,Vector3.up),q);
                    Apply(p,rotation,Mathf.Lerp(homeFov,52,q));yield return null;
                }
                if(!canceled){Apply(rear,Quaternion.LookRotation(entryFocus-rear,Vector3.up),52);look=entryFocus;Stats.entered=true;}
            }
            finally{Stats.entrySeconds=Time.unscaledTime-began;transition=false;positionVelocity=lookVelocity=Vector3.zero;fovVelocity=0;}
        }
        public void Follow(float dt)
        {
            if(canceled||transition||!Stats.entered||rider==null)return;
            Bounds body=arena.PokemonWorldBounds(side),victim=arena.PokemonWorldBounds(1-side);
            Vector3 focus=Focus(body,victim);
            Vector3 desired=body.center-forward*distance+lateral*1.8f+Vector3.up*height;
            Vector3 next=Vector3.SmoothDamp(camera.transform.position,desired,ref positionVelocity,.16f,24,dt);
            Vector3 nextFocus=Vector3.SmoothDamp(look,focus,ref lookVelocity,.18f,18,dt);
            if(Clear(next,nextFocus)&&TravelClear(camera.transform.position,next))
            {
                look=nextFocus;
                Apply(next,Quaternion.LookRotation(look-next,Vector3.up),Mathf.SmoothDamp(camera.fieldOfView,52,ref fovVelocity,.25f,8,dt));
            }
            else Stats.safe=false;
            Stats.followFrames++;
            if(Vector3.Dot(camera.transform.position-body.center,forward)<-2)Stats.rearFrames++;
            Vector3 screen=camera.WorldToViewportPoint(body.center);
            if(screen.z>0&&screen.x>.03f&&screen.x<.97f&&screen.y>.03f&&screen.y<.97f)Stats.actorVisibleFrames++;
        }
        public IEnumerator Recover()
        {
            if(canceled||!Stats.entered)yield break;
            transition=true;
            Vector3 start=camera.transform.position,startFocus=look;
            Quaternion rotation=camera.transform.rotation;float fov=camera.fieldOfView,began=Time.unscaledTime;
            bool clear=ArcClear(start,home,startFocus,homeFocus);
            if(!clear){Stats.safe=false;transition=false;yield break;}
            try
            {
                for(float t=0;t<1.60f&&!canceled;t+=Time.unscaledDeltaTime)
                {
                    float q=Ease(t/1.60f);Vector3 p=Arc(start,home,q),focus=Vector3.Lerp(startFocus,homeFocus,q);
                    Quaternion aimed=Quaternion.LookRotation(focus-p,Vector3.up);
                    Quaternion startBlend=Quaternion.Slerp(rotation,aimed,Mathf.Clamp01(q*3));
                    Apply(p,Quaternion.Slerp(startBlend,homeRotation,q),Mathf.Lerp(fov,homeFov,q));yield return null;
                }
                if(!canceled){Apply(home,homeRotation,homeFov);Stats.recovered=true;}
            }
            finally{Stats.recoverySeconds=Time.unscaledTime-began;transition=false;}
        }
        void Apply(Vector3 p,Quaternion rotation,float fov)
        {
            float dt=Mathf.Max(.001f,Time.unscaledDeltaTime);
            Stats.maxSpeed=Mathf.Max(Stats.maxSpeed,Vector3.Distance(camera.transform.position,p)/dt);
            Stats.maxAngularSpeed=Mathf.Max(Stats.maxAngularSpeed,Quaternion.Angle(camera.transform.rotation,rotation)/dt);
            Stats.maxFovSpeed=Mathf.Max(Stats.maxFovSpeed,Mathf.Abs(camera.fieldOfView-fov)/dt);
            Stats.travel=Mathf.Max(Stats.travel,Vector3.Distance(home,p));
            camera.transform.SetPositionAndRotation(p,rotation);camera.fieldOfView=fov;
        }
        bool Clear(Vector3 p,Vector3 focus)
        {
            if(arena.Stadium==null)return true;
            int n=Physics.OverlapSphereNonAlloc(p,.24f,nearby,~0,QueryTriggerInteraction.Ignore);
            if(n==nearby.Length)return false;
            for(int i=0;i<n;i++)if(nearby[i].transform.IsChildOf(arena.Stadium.transform))return false;
            return !arena.Stadium.CameraPathBlocked(p,focus);
        }
        bool TravelClear(Vector3 from,Vector3 to)
        {
            Vector3 d=to-from;if(arena.Stadium==null||d.sqrMagnitude<.000001f)return true;
            int n=Physics.SphereCastNonAlloc(from,.24f,d.normalized,hits,d.magnitude,~0,QueryTriggerInteraction.Ignore);
            if(n==hits.Length)return false;
            for(int i=0;i<n;i++)if(hits[i].collider.transform.IsChildOf(arena.Stadium.transform))return false;
            return true;
        }
        public void Cancel(){canceled=true;Stats.interrupted=Stats.planned&&!Stats.recovered;}
    }
}
