"""Compose and render AeroStadium's original sampled-instrument title theme.

No existing game melody or recording is used. Requires numpy for WAV mastering.
FluidSynth and a General MIDI SoundFont are explicit external renderer inputs.
"""
from __future__ import annotations

import argparse
import ctypes
import json
import math
import os
from pathlib import Path
import random
import struct
import subprocess
import wave

BPM = 120
TPQ = 480
BARS = 64
BEATS = BARS * 4
RATE = 48000
SEED = 20261002

THEME = [
    [('D4',1.5),(None,.5),('F4',.5),('G4',.5),('A4',1)],
    [('D5',1.5),('C5',.5),('A#4',1),('A4',1)],
    [('G4',1.5),('A4',.5),('A#4',1),('D5',1)],
    [('C#5',1),('A4',1),('E4',1),('A4',1)],
    [('D5',2),('C5',1),('A4',1)],
    [('A#4',1.5),('A4',.5),('F4',1),('D4',1)],
    [('G4',1),('A4',.5),('A#4',.5),('D5',1),('C5',1)],
    [('C#5',2),('A4',1.5),(None,.5)],
    [('D5',1.5),('A4',.5),('D5',1),('F5',1)],
    [('D5',2),('C5',1),('A#4',1)],
    [('G4',1.5),('A#4',.5),('D5',1),('G5',1)],
    [('E5',1),('D5',.5),('C#5',.5),('A4',2)],
    [('F5',1.5),('E5',.5),('D5',2)],
    [('D5',1.5),('C5',.5),('A#4',1),('F4',1)],
    [('G4',1),('A#4',1),('D5',1),('A#4',1)],
    [('E5',1),('C#5',1),('A4',1.5),(None,.5)],
]
BRIDGE = [
    [('A#4',2),('D5',1),('F5',1)],
    [('C5',1),('D5',1),('F5',2)],
    [('E5',1.5),('D5',.5),('C5',2)],
    [('D5',3),('A4',1)],
    [('G4',1.5),('A4',.5),('A#4',2)],
    [('C5',1.5),('D5',.5),('F5',2)],
    [('G5',1.5),('F5',.5),('E5',1),('D5',1)],
    [('C#5',3),('A4',1)],
    [('D5',1.5),('F5',.5),('A5',2)],
    [('G5',1.5),('F5',.5),('D5',2)],
    [('D5',1),('C5',1),('A#4',1),('G4',1)],
    [('E5',1),('C#5',1),('A4',2)],
    [('F5',2),('E5',1),('D5',1)],
    [('A#4',1),('C5',1),('D5',2)],
    [('G5',1.5),('F5',.5),('E5',1),('D5',1)],
    [('C#5',2),('E5',1),('A4',1)],
]
INTRO = [
    [('D4',2.5),(None,.5),('A3',1)],
    [('D4',2),('F4',1),('D4',1)],
    [('G4',2),('D4',2)],
    [('E4',1),('C#4',1),('A3',2)],
    [('D4',1.5),(None,.5),('F4',1),('A4',1)],
    [('A#4',2),('A4',1),('F4',1)],
    [('G4',1),('A#4',1),('D5',2)],
    [('C#5',2),('A4',1.5),(None,.5)],
]
CHORDS = {
    'Dm': ['D3','F3','A3'], 'Bb': ['A#2','D3','F3'],
    'Gm': ['G2','A#2','D3'], 'A': ['A2','C#3','E3'],
    'F': ['F2','A2','C3'], 'C': ['C3','E3','G3'],
}
PROGRESSION = ['Dm','Bb','Gm','A'] * 4
BRIDGE_CHORDS = ['Bb','F','C','Dm','Gm','F','C','A','Dm','Bb','Gm','A','Dm','Bb','C','A']

def pitch(value):
    if isinstance(value, int): return value
    note, octave = value[:-1], int(value[-1])
    return 12 * (octave + 1) + {'C':0,'C#':1,'D':2,'D#':3,'E':4,'F':5,'F#':6,'G':7,'G#':8,'A':9,'A#':10,'B':11}[note]

def vlq(value):
    out = [value & 127]
    while value > 127:
        value >>= 7
        out.insert(0, (value & 127) | 128)
    return bytes(out)

def midi(out):
    rng = random.Random(SEED)
    tracks = [[] for _ in range(13)]
    def ev(channel, beat, data, order=1):
        tracks[channel].append((max(0,round(beat * TPQ)),order,bytes(data)))
    def note(ch, start, duration, key, velocity, human=True):
        if key is None: return
        jitter = rng.uniform(-.012,.012) if human else 0
        start = max(0,start+jitter)
        velocity = max(1,min(127,round(velocity + rng.uniform(-3,3))))
        ev(ch,start,[0x90|ch,pitch(key),velocity],2)
        ev(ch,min(BEATS,start+duration),[0x80|ch,pitch(key),0],0)
    # Low horns, orchestral strings and drums; no folk/dance accompaniment.
    instruments = {
        0:(60,105,61), 1:(60,63,38), 2:(48,68,33), 3:(48,73,85),
        4:(43,91,50), 5:(44,54,95), 6:(52,35,70), 7:(47,98,51),
        8:(57,71,72), 9:(0,103,64), 10:(61,48,69), 11:(60,62,82),
        12:(116,83,58),
    }
    for ch,(program,volume,pan) in instruments.items():
        ev(ch,0,[0xC0|ch,program])
        for control,value in [(7,volume),(10,pan),(91,49),(93,0)]:
            ev(ch,0,[0xB0|ch,control,value])
    for bar in range(BARS):
        beat = bar * 4
        intro = bar < 8
        bridge = 32 <= bar < 48
        final = bar >= 48
        index = (bar-8)%16 if not intro and bar < 32 else bar%16
        chord_name = (BRIDGE_CHORDS if bridge else PROGRESSION)[index]
        chord = [pitch(p) for p in CHORDS[chord_name]]
        root = chord[0] - 12
        melody = INTRO[bar] if intro else (BRIDGE[bar%16] if bridge else THEME[index])
        at = 0
        for key,duration in melody:
            vel = (76 if intro else 88 if bridge else 94) + (9 if final else 0)
            note(0,beat+at,duration*.94,key,vel)
            if key and (final or bridge):
                note(11,beat+at,duration*.91,pitch(key)-12,66 if final else 51)
            if key and final and bar >= 56:
                note(3,beat+at,duration*.94,pitch(key)+12,63)
            at += duration
        if abs(at-4) > .0001: raise ValueError('A melodic bar must have four beats')
        # Sustained low voices provide weight; brass punctuates the cadence.
        for n in chord:
            note(2,beat,3.97,n+12,57 if intro else 81 if bridge else 67)
            if bridge or final:
                note(6,beat,3.95,n+12,47 if final else 41)
            if bar%4 in (0,3) or final:
                note(1,beat,1.4 if intro else 2.8,n+12,53 if intro else 69)
            if (bar%4 == 0 and not intro) or final:
                note(8,beat,.85,n,79 if final else 66)
        note(4,beat,3.96,root,91 if final else 80)
        note(4,beat,3.95,root+7,46)
        note(5,beat,3.98,chord[0]+12,61 if intro else 45)
        # Repeated 3+3+2 accents build tension without an oom-pah bass.
        if not intro:
            pattern = [chord[0]+12,chord[2]+12,chord[0]+12,chord[1]+12,
                       chord[0]+12,chord[2]+12,chord[1]+12,chord[2]+12]
            for step,key in enumerate(pattern):
                accent = step in (0,3,6)
                note(3,beat+step*.5,.23,key,80 if accent else 62)
        elif bar >= 4:
            for offset in (0,1.5,3):
                note(3,beat+offset,.35,chord[0]+12,56+bar*3)
        # Broad timpani/taiko accents and low toms, no hi-hat or dance backbeat.
        intensity = .55 if bar < 4 else .72 if intro else .82 if bridge else 1.0
        for offset,velocity in [(0,104),(2,78),(3.5,68)]:
            if intro and offset == 3.5: continue
            note(7,beat+offset,.62,root+12,velocity*intensity)
            note(12,beat+offset,.34,'D2',velocity*intensity)
        for offset,velocity in [(0,113),(1.5,78),(3,94)]:
            if intro and offset != 0: continue
            note(9,beat+offset,.16,36,velocity*intensity,human=False)
            note(9,beat+offset,.24,41,velocity*.78*intensity,human=False)
        if not intro:
            note(9,beat+2,.15,38,74 if bridge else 84,human=False)
            if final: note(9,beat+3.5,.16,43,81,human=False)
        if bar in (8,24,32,48,56):
            note(9,beat,1.1,49,79 if final else 66,human=False)
            for n in chord: note(10,beat,1.6,n+12,67 if final else 51)
        if bar%8 == 7:
            for step in range(8):
                offset = 3 + step*.125
                note(9,beat+offset,.10,38,47+step*6,human=False)
                note(7,beat+offset,.12,root+12,48+step*5)
            note(9,beat+3.5,.25,43,94,human=False)
            note(9,beat+3.75,.25,41,110,human=False)
    tempo = round(60_000_000/BPM)
    conductor = b'\x00\xff\x51\x03'+tempo.to_bytes(3,'big')+b'\x00\xff\x58\x04\x04\x02\x18\x08'
    name = b'AeroStadium - Le serment des champions - original v2'
    conductor += b'\x00\xff\x03'+vlq(len(name))+name
    conductor += vlq(BEATS*TPQ)+b'\xff\x2f\x00'
    chunks = [conductor]
    for events in tracks:
        if not events: continue
        body,last = bytearray(),0
        for tick,order,data in sorted(events,key=lambda e:(e[0],e[1])):
            body.extend(vlq(tick-last)); body.extend(data); last=tick
        body.extend(vlq(BEATS*TPQ-last)); body.extend(b'\xff\x2f\x00')
        chunks.append(bytes(body))
    out.write_bytes(b'MThd'+struct.pack('>IHHH',6,1,len(chunks),TPQ)+b''.join(b'MTrk'+struct.pack('>I',len(c))+c for c in chunks))

def read_float_audio(path, dll_path):
    import numpy as np
    if os.name == 'nt': os.add_dll_directory(str(dll_path.parent))
    lib = ctypes.CDLL(str(dll_path))
    class Info(ctypes.Structure):
        _fields_ = [('frames',ctypes.c_int64),('samplerate',ctypes.c_int),('channels',ctypes.c_int),('format',ctypes.c_int),('sections',ctypes.c_int),('seekable',ctypes.c_int)]
    lib.sf_open.argtypes = [ctypes.c_char_p,ctypes.c_int,ctypes.POINTER(Info)]
    lib.sf_open.restype = ctypes.c_void_p
    lib.sf_readf_float.argtypes = [ctypes.c_void_p,ctypes.POINTER(ctypes.c_float),ctypes.c_int64]
    lib.sf_readf_float.restype = ctypes.c_int64
    lib.sf_close.argtypes = [ctypes.c_void_p]
    info = Info(); handle = lib.sf_open(os.fsencode(path),0x10,ctypes.byref(info))
    if not handle: raise RuntimeError('Cannot open rendered sound')
    try:
        audio = np.empty((info.frames,info.channels),dtype=np.float32)
        read = lib.sf_readf_float(handle,audio.ctypes.data_as(ctypes.POINTER(ctypes.c_float)),info.frames)
        return audio[:read],info.samplerate
    finally: lib.sf_close(handle)

def master(raw, out, dll_path):
    import numpy as np
    audio,rate = read_float_audio(raw,dll_path)
    if rate != RATE or audio.shape[1] != 2: raise ValueError('Expected stereo 48000 Hz')
    frames = round(BEATS*60/BPM*RATE)
    if len(audio) < frames: raise ValueError('Render is shorter than composition')
    loop = audio[:frames].copy()
    # Carry release/reverb from the ending into the start of the next iteration.
    tail = audio[frames:frames+RATE*4]
    loop[:len(tail)] += tail
    loop -= loop.mean(axis=0,keepdims=True)
    peak = float(np.max(np.abs(loop)))
    if not math.isfinite(peak) or peak < .001: raise ValueError('Silent or invalid render')
    loop /= peak
    # Gentle 2.5:1 peak compression, with a periodic gain envelope for the loop.
    block = 256
    levels = np.max(np.abs(loop.reshape(-1,block,2)),axis=(1,2))
    excess_db = np.maximum(20*np.log10(np.maximum(levels,1e-6))+9,0)
    targets = 10**(-excess_db*(1-1/2.5)/20)
    gains = np.empty_like(targets)
    gain = 1.0
    for iteration in range(2):
        for index,target in enumerate(targets):
            smoothing = math.exp(-(block/RATE)/(.004 if target < gain else .10))
            gain = target + smoothing*(gain-target)
            gains[index] = gain
    centers = np.arange(len(gains))*block+block/2
    control = np.interp(np.arange(frames),np.r_[-block/2,centers,frames+block/2],np.r_[gains[-1],gains,gains[0]])
    loop *= control[:,None]
    # Ease just 2 ms at either endpoint to zero; remove any phase discontinuity
    # left by the sample renderer without changing beat timing or loop length.
    edge_frames = round(RATE*.002)
    edge = .5-.5*np.cos(np.linspace(0,np.pi,edge_frames))
    loop[:edge_frames] *= edge[:,None]
    loop[-edge_frames:] *= edge[::-1,None]
    loop *= (10**(-1.2/20))/float(np.max(np.abs(loop)))
    pcm = np.rint(loop*32767).astype('<i2')
    with wave.open(str(out),'wb') as stream:
        stream.setnchannels(2); stream.setsampwidth(2); stream.setframerate(RATE); stream.writeframes(pcm.tobytes())
    rms = float(np.sqrt(np.mean(loop.astype(np.float64)**2)))
    report = {'title':'Le serment des champions','revision':2,'original_composition':True,'bpm':BPM,'bars':BARS,'key':'D minor / heroic orchestral','sample_rate':RATE,'channels':2,'frames':frames,'duration_seconds':frames/RATE,'peak_dbfs':20*math.log10(float(np.max(np.abs(loop)))),'rms_dbfs':20*math.log10(rms),'clipped_samples':int(np.count_nonzero(np.abs(loop)>=1)),'loop_boundary_step':float(np.max(np.abs(loop[0]-loop[-1]))),'wrapped_release_frames':len(tail),'endpoint_easing_ms':2}
    out.with_suffix('.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
    print(json.dumps(report))

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--out-dir',type=Path,required=True)
    parser.add_argument('--fluidsynth',type=Path)
    parser.add_argument('--soundfont',type=Path)
    args = parser.parse_args(); args.out_dir.mkdir(parents=True,exist_ok=True)
    mid = args.out_dir/'AeroStadiumTitleTheme.mid'; midi(mid)
    print('Wrote',mid)
    if bool(args.fluidsynth) != bool(args.soundfont): parser.error('Supply both renderer and soundfont')
    if args.fluidsynth:
        raw = args.out_dir/'AeroStadiumTitleTheme-render.wav'
        command = [str(args.fluidsynth),'-ni','-g','0.3','-F',str(raw),'-T','wav','-O','float','-r',str(RATE),'-o','synth.reverb.room-size=0.5','-o','synth.reverb.damp=0.4','-o','synth.reverb.level=0.22','-o','synth.chorus.level=0.15',str(args.soundfont),str(mid)]
        subprocess.run(command,check=True)
        master(raw,args.out_dir/'AeroStadiumTitleTheme.wav',args.fluidsynth.parent/'sndfile.dll')

if __name__ == '__main__': main()
