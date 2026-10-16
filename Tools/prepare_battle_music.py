"""Prepare intro-at-zero plus sample-exact, later battle music loop.

Only local inputs; no downloader, paid services, or network operations.
The intro stays untrimmed and unfaded. Both files receive one constant gain.
The loop retains its exact 24-bar period; only its last 20 ms is blended
toward the source pre-roll at the same musical phase.
"""
from pathlib import Path
import hashlib
import json
import math
import wave
import sys
import numpy as np

P=Path(sys.argv[1]).resolve() if len(sys.argv)==2 else Path(__file__).parent
RATE=48000
CHANNELS=2
START=1885461                # source 39.2804375 s, later inferred phrase re-entry
FRAMES=1502609               # 96 beats at 184 BPM, nearest whole sample
CROSSFADE=960                # 20 ms, applied only to final loop tail
END=START+FRAMES
PEAK_DBFS=-1.2

def sha(path): return hashlib.sha256(path.read_bytes()).hexdigest().upper()

def write(path, pcm):
    with wave.open(str(path),'wb') as w:
        w.setnchannels(CHANNELS)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(pcm.tobytes())

def rms(a): return float(np.sqrt(np.mean(a.astype(np.float64)**2)))

source=np.fromfile(P/'source.f32',dtype='<f4').reshape(-1,CHANNELS)
meta=json.loads((P/'XEKBrjtCdL8.info.json').read_text(encoding='utf-8'))
assert END<=len(source) and START>CROSSFADE
assert np.isfinite(source).all()
intro=source[:START].copy()
loop=source[START:END].copy()
prefix=source[START-CROSSFADE:START].copy()
tail=loop[-CROSSFADE:].copy()
correlation=float(np.sum(tail.astype(np.float64)*prefix)/math.sqrt(float(np.sum(tail.astype(np.float64)**2))*float(np.sum(prefix.astype(np.float64)**2))))
weights=(.5-.5*np.cos(np.linspace(0,np.pi,CROSSFADE))).astype(np.float32)
loop[-CROSSFADE:]=tail*(1-weights[:,None])+prefix*weights[:,None]
# One common gain prevents clipping and preserves the intro's dynamics.
gain=float(10**(PEAK_DBFS/20)/max(np.abs(intro).max(),np.abs(loop).max()))
intro_pcm=np.rint(intro*gain*32767).astype('<i2')
loop_pcm=np.rint(loop*gain*32767).astype('<i2')
assert np.array_equal(intro_pcm,np.rint(source[:START]*gain*32767).astype('<i2'))
assert np.array_equal(loop_pcm[:-CROSSFADE],np.rint(source[START:END-CROSSFADE]*gain*32767).astype('<i2'))
write(P/'AeroStadiumBattleIntro.wav',intro_pcm)
write(P/'AeroStadiumBattleLoop.wav',loop_pcm)
# Short seam audition: 2 seconds of tail then 4 seconds after the wrap.
write(P/'loop-seam-review.wav',np.concatenate((loop_pcm[-2*RATE:],loop_pcm[:4*RATE])))
write(P/'intro-to-loop-review.wav',np.concatenate((intro_pcm[-2*RATE:],loop_pcm[:4*RATE])))
write(P/'entrance-review.wav',intro_pcm[:5*RATE])
intro_step=(loop_pcm[0].astype(np.float64)-intro_pcm[-1])/32767
loop_step=(loop_pcm[0].astype(np.float64)-loop_pcm[-1])/32767
natural_step=(source[START].astype(np.float64)-source[START-1])*gain
raw_loop_step=(source[START].astype(np.float64)-source[END-1])*gain
pcm_combined=np.concatenate((intro_pcm,loop_pcm))
report={
 'source_url':meta['webpage_url'],'source_video_id':meta['id'],
 'source_title':meta['title'],'source_uploader':meta['uploader'],
 'source_channel_url':meta['channel_url'],'source_upload_date':meta.get('upload_date'),
 'source_description':meta.get('description'),
 'source_file':'XEKBrjtCdL8.webm','source_sha256':sha(P/'XEKBrjtCdL8.webm'),
 'source_decoded_frames':len(source),'source_decoded_duration_seconds':len(source)/RATE,
 'source_decoded_peak':float(np.max(np.abs(source))),
 'sample_rate':RATE,'channels':CHANNELS,'pcm_bits':16,
 'intro':{'file':'AeroStadiumBattleIntro.wav','source_start_frame':0,'source_end_frame_exclusive':START,'frames':START,'duration_seconds':START/RATE,'trimmed':False,'faded':False,'sample_exact_to_source_after_constant_gain_and_pcm_quantization':True,'sha256':sha(P/'AeroStadiumBattleIntro.wav')},
 'loop':{'file':'AeroStadiumBattleLoop.wav','source_start_frame':START,'source_end_frame_exclusive':END,'source_start_seconds':START/RATE,'source_end_seconds':END/RATE,'frames':FRAMES,'duration_seconds':FRAMES/RATE,'sha256':sha(P/'AeroStadiumBattleLoop.wav')},
 'musical_alignment':{'estimated_bpm':184,'quarter_note_beats_per_loop':96,'bars_in_4_4':24,'theoretical_period_seconds':96*60/184,'whole_sample_period_error_seconds':FRAMES/RATE-96*60/184,'inferred_phrase_reentry_seconds':START/RATE,'evidence':'Global spectral recurrence peaks near 31.30 s; independently matched four-second waveform windows throughout the main section all identify 1,502,609-frame delay. The cut is at a later phrase re-entry on the inferred bar grid (source musical start near 0.150 s, 30 bars later), preserving the original intro and first statement in full. The distinct introductory material is excluded from the repeating region. Subjective phrase-boundary audition is still recommended; exact beat/bar grid and phrase inference are analytical, not a claim of listening.','subjective_listening_performed':False},
 'crossfade':{'frames':CROSSFADE,'seconds':CROSSFADE/RATE,'method':'raised-cosine blend of final loop tail toward source pre-roll immediately before loop start; complete period retained','applied_to_intro':False,'waveform_correlation':correlation,'preroll_tail_rms_difference_db':20*math.log10(rms(prefix)/rms(tail))},
 'common_gain_db':20*math.log10(gain),'tempo_changed':False,
 'verification':{'intro_to_loop_max_step_error_from_natural_source_step':float(np.max(np.abs(intro_step-natural_step))),'loop_to_loop_max_step_error_from_natural_source_step':float(np.max(np.abs(loop_step-natural_step))),'loop_step_peak':float(np.max(np.abs(loop_step))),'loop_step_peak_without_crossfade':float(np.max(np.abs(raw_loop_step))),'peak_dbfs':20*math.log10(float(np.abs(pcm_combined.astype(np.float64)).max())/32767),'clipped_samples':int(np.count_nonzero(np.abs(pcm_combined.astype(np.int32))>=32767)),'loop_rms_dbfs':20*math.log10(rms(loop_pcm)/32767),'loop_no_silent_100ms_blocks':bool(np.all(np.sqrt(np.mean(loop_pcm[:FRAMES//4800*4800].astype(np.float64).reshape(-1,4800,2)**2,axis=(1,2)))>1))},
 'runtime_guidance':{'start':'At battle entrance schedule AeroStadiumBattleIntro.wav at time sample 0 with full intended music volume, without a fade-in.','transition':'Schedule AeroStadiumBattleLoop.wav using AudioSource.PlayScheduled at intro start DSP time + 1885461 / 48000. Separate 2D AudioSources; intro.loop=false, loop.loop=true. Do not poll isPlaying to start the loop because a frame-delayed start may create a gap.','import':'PCM WAV, DecompressOnLoad / PCM, preloadAudioData=true, loadInBackground=false, preserve sample rate 48000; do not compress or resample the seamless loop.','stop':'On battle exit, fade both sources together using unscaled time over about 0.4–0.7 seconds; then Stop both and reset their timeSamples=0 before next battle. Do not fade or stop between normal turns.'}
}
assert report['verification']['clipped_samples']==0
assert report['verification']['loop_to_loop_max_step_error_from_natural_source_step']<2/32767
(P/'battle-music.json').write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(json.dumps(report,ensure_ascii=True,indent=2))
