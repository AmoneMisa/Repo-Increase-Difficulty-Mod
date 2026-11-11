"""Convert a supplied PCM16 laugh to the mod's canonical mono WAV format."""
import argparse
import array
import sys
import wave
from pathlib import Path

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("source", type=Path)
parser.add_argument("output", type=Path)
args = parser.parse_args()
with wave.open(str(args.source), "rb") as source:
    channels, width, rate, frames, compression, _ = source.getparams()
    if width != 2 or channels not in (1, 2) or compression != "NONE":
        raise ValueError("Expected mono/stereo PCM16 WAV")
    samples = array.array("h", source.readframes(frames))
if sys.byteorder != "little":
    samples.byteswap()
mono = [sum(samples[i:i + channels]) / channels for i in range(0, len(samples), channels)]
if not mono:
    raise ValueError("Empty source")
dc = sum(mono) / len(mono)
mono = [value - dc for value in mono]
peak = max(abs(value) for value in mono)
if peak < 1:
    raise ValueError("Silent source")
active = [i for i, value in enumerate(mono) if abs(value) >= max(32, peak * .002)]
padding = int(rate * .03)
mono = mono[max(0, active[0] - padding):min(len(mono), active[-1] + padding + 1)]
# Keep the voice natural: no pitch shift, stretching, extra reverb or gain boost.
gain = min(1., 26214 / peak)
fade_in, fade_out = max(1, int(rate * .008)), max(1, int(rate * .02))
result = array.array("h", (round(value * gain * min(1., i / fade_in, (len(mono) - 1 - i) / fade_out))
                           for i, value in enumerate(mono)))
if sys.byteorder != "little":
    result.byteswap()
args.output.parent.mkdir(parents=True, exist_ok=True)
with wave.open(str(args.output), "wb") as output:
    output.setparams((1, 2, rate, 0, "NONE", "not compressed"))
    output.writeframes(result.tobytes())
print(f"{args.output}: PCM16 mono, {rate} Hz, {len(mono) / rate:.3f} s")
