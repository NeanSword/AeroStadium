using System;
using System.IO;
using UnityEngine;

namespace AeroStadium.Presentation
{
    /// <summary>Copies the listener DSP mix without changing playback. Begin/Finish run on the main thread.</summary>
    [DisallowMultipleComponent]
    public sealed class EnergyBallReviewAudio : MonoBehaviour
    {
        const int MaximumChannels=8, MaximumFloatSamples=25000000; // At most 100 MB, allocated before capture.
        [Serializable] public sealed class CaptureReport
        {
            public bool passed, audio;
            public string wav, error;
            public int sampleRate, channels, sampleFrames, leadingZeroFrames;
            public long callbacks, rawSampleFrames, copiedSampleFrames, droppedFrames, outOfRangeFrames, gapFrames, overlapFrames, invalidBlocks, nonfiniteSamples, bufferBytes;
            public double referenceDspTime, referenceRealtime, endDspTime, firstDspOffset, seconds;
            public float peak;
        }
        readonly object gate=new object();
        float[] samples;
        string wavPath, jsonPath;
        bool active, begun, finished;
        int rate, channels, frameLimit, capturedMaxFrame, firstCopiedFrame;
        long callbacks, rawFrames, copiedFrames, droppedFrames, outOfRangeFrames, gapFrames, overlapFrames, invalidBlocks, bufferBytes;
        double referenceDsp, referenceRealtime, firstOffset;
        public CaptureReport LastReport { get; private set; }
        public string LastFailure { get; private set; }

        public bool BeginCapture(string directory,double referenceDsp,double referenceRealtime,float maxSeconds)
        {
            if (begun) { LastFailure="Audio capture can only begin once per component."; return false; }
            try
            {
                string folder=Path.GetFullPath(directory);
                wavPath=Path.Combine(folder,"game-audio.wav"); jsonPath=Path.Combine(folder,"game-audio.json");
                if (File.Exists(wavPath)||File.Exists(jsonPath)) throw new IOException("Audio capture output already exists; refusing to overwrite it.");
                Directory.CreateDirectory(folder); begun=true;
                this.referenceDsp=referenceDsp; this.referenceRealtime=referenceRealtime;
                if (!Finite(referenceDsp)||!Finite(referenceRealtime)||!Finite(maxSeconds)||maxSeconds<=0)
                    throw new ArgumentException("Audio capture needs finite clocks and a positive duration.");
                var listener=GetComponent<AudioListener>();
                if (listener==null||!listener.isActiveAndEnabled) throw new InvalidOperationException("Audio capture needs the existing active AudioListener.");
                rate=AudioSettings.outputSampleRate;
                if (rate<=0) throw new InvalidOperationException("Unity has no audio output sample rate.");
                frameLimit=(int)Math.Min(int.MaxValue,Math.Ceiling(Math.Min(120d,maxSeconds)*rate));
                var buffer=new float[(int)Math.Min(MaximumFloatSamples,(long)frameLimit*MaximumChannels)];
                bufferBytes=(long)buffer.Length*sizeof(float);
                lock (gate) { samples=buffer; active=true; }
                return true;
            }
            catch (Exception exception) { LastFailure=exception.Message; return false; }
        }

        void OnAudioFilterRead(float[] data,int inputChannels)
        {
            // Unity's documented DSP clock is the only Unity API read on this thread.
            double dsp=AudioSettings.dspTime;
            lock (gate)
            {
                if (!active) return;
                callbacks++;
                if (data==null||inputChannels<1||inputChannels>MaximumChannels||data.Length%inputChannels!=0||!Finite(dsp)) { invalidBlocks++; return; }
                int frames=data.Length/inputChannels; rawFrames+=frames;
                if (channels==0) { channels=inputChannels; firstOffset=dsp-referenceDsp; }
                if (channels!=inputChannels) { invalidBlocks++; droppedFrames+=frames; return; }
                long start=(long)Math.Round((dsp-referenceDsp)*rate),end=start+frames;
                int limit=Math.Min(frameLimit,samples.Length/channels);
                int skip=(int)Math.Min(frames,Math.Max(0,-start));
                long destination=Math.Max(0,start);
                int count=(int)Math.Max(0,Math.Min((long)frames-skip,(long)limit-destination));
                int inRange=(int)Math.Max(0,Math.Min(end,frameLimit)-Math.Max(start,0));
                outOfRangeFrames+=frames-inRange; droppedFrames+=inRange-count;
                if (count==0) return;
                if (copiedFrames==0) firstCopiedFrame=(int)destination;
                else gapFrames+=Math.Max(0,destination-capturedMaxFrame);
                overlapFrames+=Math.Min(count,Math.Max(0,capturedMaxFrame-destination));
                Array.Copy(data,skip*channels,samples,(int)destination*channels,count*channels);
                copiedFrames+=count; capturedMaxFrame=Math.Max(capturedMaxFrame,(int)destination+count);
            }
        }

        public bool FinishCapture(double endDsp)
        {
            if (finished) return LastReport!=null&&LastReport.passed;
            if (!begun) { LastFailure=LastFailure??"Audio capture never began."; return false; }
            float[] captured;
            lock (gate) { active=false; captured=samples; samples=null; finished=true; }
            bool validEnd=Finite(endDsp)&&endDsp>=referenceDsp;
            int frames=validEnd?(int)Math.Min(capturedMaxFrame,Math.Max(0,Math.Round((endDsp-referenceDsp)*rate))):0;
            var report=new CaptureReport { sampleRate=rate,channels=channels,sampleFrames=frames,leadingZeroFrames=Math.Min(firstCopiedFrame,frames),
                callbacks=callbacks,rawSampleFrames=rawFrames,copiedSampleFrames=copiedFrames,droppedFrames=droppedFrames,outOfRangeFrames=outOfRangeFrames,
                gapFrames=gapFrames,overlapFrames=overlapFrames,invalidBlocks=invalidBlocks,bufferBytes=bufferBytes,referenceDspTime=Finite(referenceDsp)?referenceDsp:0,
                referenceRealtime=Finite(referenceRealtime)?referenceRealtime:0,endDspTime=Finite(endDsp)?endDsp:0,firstDspOffset=firstOffset,seconds=rate>0?(double)frames/rate:0,error=LastFailure };
            LastReport=report;
            try
            {
                int sampleCount=frames*channels;
                for (int i=0;i<sampleCount;i++)
                {
                    float value=captured[i];
                    if (!Finite(value)) report.nonfiniteSamples++;
                    else report.peak=Math.Max(report.peak,Math.Abs(value));
                }
                report.audio=callbacks>0&&copiedFrames>0&&sampleCount>0&&report.peak>0;
                if (!validEnd) report.error="Audio capture end DSP clock is invalid.";
                else if (!report.audio) report.error=report.error??"No nonzero listener DSP audio was captured.";
                else if (droppedFrames>0||invalidBlocks>0||report.nonfiniteSamples>0||gapFrames>0||overlapFrames>0)
                    report.error="Listener DSP capture contains missing, overlapping, invalid or nonfinite samples; see counters.";
                if (sampleCount>0)
                {
                    using (var stream=new FileStream(wavPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read,65536))
                    using (var writer=new BinaryWriter(stream))
                    {
                        int bytes=sampleCount*2;
                        writer.Write(0x46464952u); writer.Write(36+bytes); writer.Write(0x45564157u); writer.Write(0x20746d66u); writer.Write(16);
                        writer.Write((short)1); writer.Write((short)channels); writer.Write(rate); writer.Write(rate*channels*2);
                        writer.Write((short)(channels*2)); writer.Write((short)16); writer.Write(0x61746164u); writer.Write(bytes);
                        for (int i=0;i<sampleCount;i++)
                        {
                            double value=Finite(captured[i])?Math.Max(-1,Math.Min(1,captured[i])):0;
                            writer.Write((short)Math.Round(value*32767));
                        }
                    }
                    report.wav=Path.GetFileName(wavPath);
                }
                report.passed=report.audio&&string.IsNullOrEmpty(report.error);
            }
            catch (Exception exception) { report.passed=false; report.error=exception.Message; }
            try
            {
                using (var stream=new FileStream(jsonPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read))
                using (var writer=new StreamWriter(stream)) writer.Write(JsonUtility.ToJson(report,true));
            }
            catch (Exception exception) { report.passed=false; report.error="Audio metadata write failed: "+exception.Message; }
            LastFailure=report.error;
            return report.passed;
        }
        static bool Finite(double value) => !double.IsNaN(value)&&!double.IsInfinity(value);
        void OnDestroy() { lock (gate) { active=false; samples=null; } }
    }
}
