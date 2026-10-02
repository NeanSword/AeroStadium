"""Prepare a sample-aligned title loop from a locally supplied audio recording.

Requires NumPy and an explicit FFmpeg executable. No downloader or network calls.
The loop tail is blended toward the source pre-roll at the same musical phase;
the operation keeps the complete period and does not shorten the tempo grid.
Optional power compensation preserves energy for uncorrelated performances.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import math
from pathlib import Path
import subprocess
import wave

import numpy as np

RATE = 48000


def prepare(args):
    command = [str(args.ffmpeg), '-hide_banner', '-loglevel', 'error', '-i', str(args.input),
               '-vn', '-ac', '2', '-ar', str(RATE), '-f', 'f32le', 'pipe:1']
    decoded = subprocess.run(command, stdout=subprocess.PIPE, stderr=subprocess.PIPE, check=True)
    audio = np.frombuffer(decoded.stdout, dtype='<f4').reshape(-1, 2)
    if not np.isfinite(audio).all():
        raise ValueError('Source contains invalid samples')
    first = round(args.start * RATE)
    frames = round(args.duration * RATE)
    overlap = round(args.crossfade * RATE)
    if overlap < 2 or overlap >= frames or first < overlap or first + frames > len(audio):
        raise ValueError('Invalid loop interval or missing pre-roll')
    loop = audio[first:first + frames].copy()
    prefix = audio[first - overlap:first]
    ending = loop[-overlap:].copy()
    correlation = float(np.dot(ending.ravel(), prefix.ravel()) /
                        max(math.sqrt(float(np.sum(ending.astype(np.float64)**2)) *
                                      float(np.sum(prefix.astype(np.float64)**2))), 1e-12))
    weight = (.5 - .5 * np.cos(np.linspace(0, np.pi, overlap))).astype(np.float32)
    blended = ending * (1 - weight[:, None]) + prefix * weight[:, None]
    preserve_power = getattr(args, 'preserve_crossfade_power', False)
    if preserve_power:
        # Uncorrelated performances lose energy at a linear crossfade midpoint.
        # Keep the measured power while retaining exact endpoint samples and time.
        usable_correlation = min(1., max(-.95, correlation))
        power = (1 - weight)**2 + weight**2 + 2 * usable_correlation * weight * (1 - weight)
        blended /= np.sqrt(np.maximum(power, .025))[:, None]
    loop[-overlap:] = blended
    loop -= loop.mean(axis=0, keepdims=True)
    raw_peak = float(np.max(np.abs(loop)))
    if raw_peak < .001:
        raise ValueError('Loop is silent')
    gain = (10 ** (args.peak_dbfs / 20)) / raw_peak
    loop *= gain
    if not np.isfinite(loop).all() or float(np.max(np.abs(loop))) >= 1:
        raise ValueError('Mastered loop clips or contains invalid samples')
    pcm = np.rint(loop * 32767).astype('<i2')
    args.output.parent.mkdir(parents=True, exist_ok=True)
    with wave.open(str(args.output), 'wb') as stream:
        stream.setnchannels(2)
        stream.setsampwidth(2)
        stream.setframerate(RATE)
        stream.writeframes(pcm.tobytes())
    actual_step = (pcm[0].astype(np.float64) - pcm[-1].astype(np.float64)) / 32767
    expected_step = (audio[first].astype(np.float64) - audio[first - 1].astype(np.float64)) * gain
    block = round(RATE * .1)
    complete = loop[:frames // block * block].reshape(-1, block, 2)
    levels = np.sqrt(np.mean(complete.astype(np.float64)**2, axis=(1, 2)))
    rms = float(np.sqrt(np.mean(loop.astype(np.float64)**2)))
    report = {
        'source_url': args.source_url,
        'source_title': args.source_title,
        'source_credit': args.source_credit,
        'source_sha256': hashlib.sha256(args.input.read_bytes()).hexdigest().upper(),
        'source_decoded_duration_seconds': len(audio) / RATE,
        'source_decoded_peak': float(np.max(np.abs(audio))),
        'loop_source_start_seconds': first / RATE,
        'loop_source_end_seconds': (first + frames) / RATE,
        'duration_seconds': frames / RATE,
        'sample_rate': RATE, 'channels': 2, 'pcm_bits': 16, 'frames': frames,
        'tempo_changed': False,
        'crossfade_seconds': overlap / RATE,
        'crossfade_method': 'raised-cosine blend of loop tail toward source pre-roll at matching musical phase',
        'crossfade_waveform_correlation': correlation,
        'crossfade_power_compensated': preserve_power,
        'gain_db': 20 * math.log10(gain),
        'peak_dbfs': 20 * math.log10(float(np.max(np.abs(loop)))),
        'rms_dbfs': 20 * math.log10(rms),
        'clipped_samples': int(np.count_nonzero(np.abs(loop) >= 1)),
        'min_100ms_rms_dbfs': float(20 * np.log10(max(float(np.min(levels)), 1e-10))),
        'silent_100ms_blocks_below_minus90db': int(np.count_nonzero(levels < 10 ** (-90 / 20))),
        'seam_step_error_relative_to_natural_source_step': float(np.max(np.abs(actual_step - expected_step))),
        'wav_sha256': hashlib.sha256(args.output.read_bytes()).hexdigest().upper(),
    }
    if report['seam_step_error_relative_to_natural_source_step'] > 2 / 32767:
        raise ValueError('Loop seam does not match the natural adjacent source samples')
    if report['silent_100ms_blocks_below_minus90db']:
        raise ValueError('Loop still contains a digital silence of 100 ms or more')
    args.output.with_suffix('.json').write_text(json.dumps(report, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(report, ensure_ascii=True))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--input', type=Path, required=True)
    parser.add_argument('--ffmpeg', type=Path, required=True)
    parser.add_argument('--output', type=Path, required=True)
    parser.add_argument('--start', type=float, required=True)
    parser.add_argument('--duration', type=float, required=True)
    parser.add_argument('--crossfade', type=float, default=.4285714286)
    parser.add_argument('--peak-dbfs', type=float, default=-1.2)
    parser.add_argument('--preserve-crossfade-power', action='store_true',
                        help='Preserve power when loop repeats have uncorrelated waveforms')
    parser.add_argument('--source-url', default='')
    parser.add_argument('--source-title', default='')
    parser.add_argument('--source-credit', default='')
    prepare(parser.parse_args())


if __name__ == '__main__':
    main()
