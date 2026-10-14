using System;
using System.Collections.Generic;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Original deterministic mono synthesis: wet pressure train (61), airy spaced pops (145).</summary>
    public sealed class BubbleAttackAudio : MonoBehaviour
    {
        public const int SampleRate=22050;
        struct CachedClip { public AudioClip clip; public PcmStats stats; }
        readonly Dictionary<long,CachedClip> clips=new Dictionary<long,CachedClip>();
        static float[] beamPcm,bubblePcm;
        [Serializable] public struct PcmStats
        {
            public int sampleRate,samples; public float duration,peak,rms,seamJump; public bool finite;
            public override string ToString()=>"Original bubbles samples="+samples+" peak="+peak.ToString("F4")+" rms="+rms.ToString("F4")+" finite="+finite;
        }
        public PcmStats LastPcmStats { get; private set; }
        public static PcmStats DefaultPcmStats=>AnalyzePcm(CreateOriginalPcm(61));
        public static float DefaultDuration(int moveId)=>moveId==61?1.95f:2.40f;
        public static AudioSource Play(Transform parent,Vector3 source,int moveId,float duration)
        {
            Check(moveId); if(parent==null)return null;
            var owner=parent.GetComponentInParent<BubbleAttackAudio>();
            if(owner==null)owner=parent.gameObject.AddComponent<BubbleAttackAudio>();
            int samples=Mathf.RoundToInt(Safe(duration,moveId)*SampleRate);long key=((long)moveId<<32)|(uint)samples;
            if(!owner.clips.TryGetValue(key,out CachedClip cached))
            {
                var pcm=CreateOriginalPcm(moveId,samples/(float)SampleRate);
                var clip=AudioClip.Create("Original bubbles "+moveId+" "+samples,pcm.Length,1,SampleRate,false);clip.SetData(pcm,0);
                cached=new CachedClip{clip=clip,stats=AnalyzePcm(pcm)};owner.clips.Add(key,cached);
            }
            owner.LastPcmStats=cached.stats;
            var go=new GameObject(moveId==61?"Bulles d'O · original wet pops":"Écume · original airy pops");
            go.transform.SetParent(parent,false);go.transform.position=source;
            var audio=go.AddComponent<AudioSource>();audio.playOnAwake=false;audio.clip=cached.clip;
            audio.volume=moveId==61?.52f:.45f;audio.spatialBlend=.5f;audio.dopplerLevel=0;
            audio.rolloffMode=AudioRolloffMode.Linear;audio.minDistance=2;audio.maxDistance=30;audio.priority=75;
            if(Application.isPlaying){audio.Play();Destroy(go,cached.clip.length+.1f);}return audio;
        }
        /// <summary>Shared default arrays are read-only; arbitrary durations return freshly synthesized samples.</summary>
        public static float[] CreateOriginalPcm(int moveId)
        {
            Check(moveId);
            if(moveId==61)return beamPcm??(beamPcm=Synthesize(61,DefaultDuration(61)));
            return bubblePcm??(bubblePcm=Synthesize(145,DefaultDuration(145)));
        }
        public static float[] CreateOriginalPcm(int moveId,float duration)
        {
            Check(moveId);duration=Safe(duration,moveId);
            return Mathf.Abs(duration-DefaultDuration(moveId))<.0001f?CreateOriginalPcm(moveId):Synthesize(moveId,duration);
        }
        static float[] Synthesize(int moveId,float duration)
        {
            bool beam=moveId==61;int samples=Mathf.RoundToInt(duration*SampleRate);
            var pcm=new float[samples];uint seed=beam?0x61babb1eu:0x145a1b1eu;
            float mid=0,low=0;float nominal=DefaultDuration(moveId),step=nominal/samples;
            for(int i=0;i<samples;i++)
            {
                float t=i/(float)SampleRate,p=i*step;seed^=seed<<13;seed^=seed>>17;seed^=seed<<5;
                float noise=(seed&0xffffffu)/8388607.5f-1f;low+=.035f*(noise-low);mid+=.22f*(noise-mid);
                float pressure=Smooth((p-.10f)/.04f)*(1-Smooth((p-(beam?.69f:.96f))/.13f));
                float value=pressure*(beam?(.38f*mid+.28f*low):(.16f*(noise-mid)+.13f*mid));
                int bubbles=beam?24:16;
                for(int b=0;b<bubbles;b++)
                {
                    float born=.12f+b*(beam?.024f:.052f),phase=b*2.399963f;
                    float age=p-born;float flight=beam?.50f:.74f;
                    if(age>=0 && age<.15f)
                        value+=(beam?.095f:.080f)*Mathf.Sin(2*Mathf.PI*(440f*age+1200f*age*age)+phase)*Mathf.Exp(-age*35f)*Smooth(age/.004f);
                    float popAge=age-flight;
                    if(popAge<0 || popAge>.27f)continue;
                    float frequency=(beam?1050f:690f)+290f*(.5f+.5f*Mathf.Sin(phase));
                    float angle=2*Mathf.PI*frequency*(1-Mathf.Exp(-popAge*12f))/12f;
                    float envelope=Smooth(popAge/.0015f)*Mathf.Exp(-popAge*(beam?53f:32f));
                    value+=envelope*((beam?.22f:.18f)*Mathf.Sin(angle)+.12f*mid+.025f*(noise-mid));
                    float drip=popAge-.095f;
                    if(drip>0)value+=.035f*Mathf.Sin(2*Mathf.PI*1850f*drip)*Mathf.Exp(-drip*52f)*Smooth(drip/.003f);
                }
                float fade=Smooth(t/.008f)*Smooth((duration-t)/.10f);
                pcm[i]=.78f*(float)Math.Tanh(value*1.35f)*fade;
            }
            return pcm;
        }
        public static PcmStats AnalyzePcm(float[] pcm)
        {
            var result=new PcmStats{sampleRate=SampleRate,finite=pcm!=null};if(pcm==null)return result;
            result.samples=pcm.Length;result.duration=pcm.Length/(float)SampleRate;double sum=0;
            foreach(float v in pcm){if(float.IsNaN(v)||float.IsInfinity(v)){result.finite=false;continue;}result.peak=Mathf.Max(result.peak,Mathf.Abs(v));sum+=v*v;}
            result.rms=pcm.Length==0?0:(float)Math.Sqrt(sum/pcm.Length);result.seamJump=pcm.Length<2?0:Mathf.Abs(pcm[0]-pcm[pcm.Length-1]);return result;
        }
        static float Smooth(float p){p=Mathf.Clamp01(p);return p*p*(3-2*p);}
        static float Safe(float d,int moveId)=>float.IsNaN(d)||float.IsInfinity(d)?DefaultDuration(moveId):Mathf.Clamp(d,.5f,5f);
        static void Check(int moveId){if(moveId!=61&&moveId!=145)throw new ArgumentOutOfRangeException(nameof(moveId));}
        void OnDestroy(){foreach(CachedClip c in clips.Values)if(c.clip!=null){if(Application.isPlaying)Destroy(c.clip);else DestroyImmediate(c.clip);}clips.Clear();}
    }
}
