"""Trim and gain-stage supplied Gift Festival WAVs; originals remain untouched.

Uses ffmpeg for filtering, short boundary fades and true-peak measurement. No project build.
The report records source hashes, exact edits and measured output levels.
"""
from pathlib import Path
import hashlib
import json
import math
import re
import subprocess
import tempfile
import uuid
import wave

ROOT = Path(__file__).resolve().parents[1]
DEST = ROOT / "Assets/_Project/Audio/SFX/GiftFestival"
SOURCE = Path("/Users/turgayp/Downloads")
SPECS = {
    "Intro": dict(source="Giftfestival_intro_v2.wav", start=0.0, end=3.0, fade_in=0.015, fade_out=0.120, highpass=60, lowpass=14000, peak=-7, lufs=-22, channels=2, volume=0.8),
    "Throw": dict(start=0.018, end=0.30, fade_in=0.002, fade_out=0.020, highpass=100, lowpass=14000, peak=-6, channels=1, volume=0.85),
    "BatHit": dict(start=0.0, end=0.135, fade_in=0.0005, fade_out=0.015, highpass=70, lowpass=14000, peak=-3, channels=1, volume=0.9),
    "BallFlight": dict(start=0.105, end=0.53, fade_in=0.003, fade_out=0.025, highpass=100, lowpass=14000, peak=-7, channels=1, volume=0.65),
    "GlassBreak": dict(start=0.016, end=0.59, fade_in=0.002, fade_out=0.030, highpass=180, lowpass=10000, peak=-4, channels=1, volume=0.85),
}


def run(args):
    return subprocess.run(["ffmpeg", "-hide_banner", "-nostats", "-y", *args], capture_output=True, text=True, check=True)


def measure(path, filters=None):
    chain = ([filters] if filters else []) + ["loudnorm=I=-18:TP=-3:LRA=7:print_format=json"]
    result = run(["-i", str(path), "-af", ",".join(chain), "-f", "null", "-"])
    offset = result.stderr.rfind("{")
    result = json.JSONDecoder().raw_decode(result.stderr[offset:])[0]
    return {"truePeakDbTP": float(result["input_tp"]), "integratedLUFS": float(result["input_i"]) if math.isfinite(float(result["input_i"])) else None}


def meta(path, folder=False):
    p = Path(str(path) + ".meta")
    if p.exists(): return re.search(r"^guid: (\w+)", p.read_text(), re.M).group(1)
    guid = uuid.uuid5(uuid.NAMESPACE_URL, "tinyfixers/" + str(path.relative_to(ROOT))).hex
    if folder:
        p.write_text(f"fileFormatVersion: 2\nguid: {guid}\nfolderAsset: yes\nDefaultImporter:\n  externalObjects: {{}}\n  userData: \n  assetBundleName: \n  assetBundleVariant: \n")
    else:
        p.write_text(f"""fileFormatVersion: 2
guid: {guid}
AudioImporter:
  externalObjects: {{}}
  serializedVersion: 8
  defaultSettings:
    serializedVersion: 2
    loadType: 0
    sampleRateSetting: 0
    sampleRateOverride: 48000
    compressionFormat: 0
    quality: 1
    conversionMode: 0
    preloadAudioData: 1
  platformSettingOverrides: {{}}
  forceToMono: 0
  normalize: 0
  loadInBackground: 0
  ambisonic: 0
  3D: 0
  userData: Offline gain-staged; do not normalize again on import
  assetBundleName: 
  assetBundleVariant: 
""")
    return guid


def main():
    DEST.mkdir(parents=True, exist_ok=True)
    meta(DEST, folder=True)
    report = []
    refs = {}
    for name, spec in SPECS.items():
        source = SOURCE / spec.get("source", f"GiftFestival_{name}.wav")
        # Replace the existing imported asset, preserving its GUID and library reference.
        dest = DEST / f"GiftFestival_{name}.wav"
        duration = spec["end"] - spec["start"]
        filters = ",".join([
            f"atrim=start={spec['start']}:end={spec['end']}", "asetpts=PTS-STARTPTS",
            "aformat=channel_layouts=" + ("stereo" if spec["channels"] == 2 else "mono"),
            f"highpass=f={spec['highpass']}", f"lowpass=f={spec['lowpass']}",
            f"afade=t=in:st=0:d={spec['fade_in']}",
            f"afade=t=out:st={duration-spec['fade_out']}:d={spec['fade_out']}",
        ])
        # Measure the actual rendered channel layout, avoiding filter-graph mono/stereo negotiation differences.
        with tempfile.TemporaryDirectory(prefix="giftfestival-audio-") as staging:
            prepared = Path(staging) / "prepared.wav"
            run(["-i", str(source), "-af", filters, "-ar", "48000", "-ac", str(spec["channels"]), "-c:a", "pcm_s24le", str(prepared)])
            before = measure(prepared)
            gain = spec["peak"] - before["truePeakDbTP"]
            if "lufs" in spec and before["integratedLUFS"] is not None:
                gain = min(gain, spec["lufs"] - before["integratedLUFS"])
            run(["-i", str(prepared), "-af", f"volume={gain:.4f}dB", "-ar", "48000", "-ac", str(spec["channels"]), "-c:a", "pcm_s16le", str(dest)])
        after = measure(dest)
        assert after["truePeakDbTP"] <= spec["peak"] + 0.15, (name, after)
        with wave.open(str(dest)) as wav:
            frames, rate = wav.getnframes(), wav.getframerate()
            assert wav.getsampwidth() == 2 and rate == 48000
        refs[name] = meta(dest)
        entry = {"clip": name, "source": str(source), "sourceSha256": hashlib.sha256(source.read_bytes()).hexdigest(),
            "sourceRangeSeconds": [spec["start"], spec["end"]], "gainDb": round(gain, 3),
            "fadeInSeconds": spec["fade_in"], "fadeOutSeconds": spec["fade_out"],
            "highpassHz": spec["highpass"], "lowpassHz": spec["lowpass"],
            "outputDurationSeconds": round(frames/rate, 6), "channels": spec["channels"],
            "tempoFactor": 1.0,
            "cueVolume": spec["volume"], **after}
        report.append(entry)
        print(f"{name}: {entry['outputDurationSeconds']:.3f}s, {after['truePeakDbTP']:.2f} dBTP, gain {gain:+.2f} dB")
    library = ROOT / "Assets/_Project/Resources/Audio/EventSfxLibrary.asset"
    text = library.read_text()
    for name, spec in SPECS.items():
        field = "giftFestival" + name
        block = f"  {field}:\n    clips:\n    - {{fileID: 8300000, guid: {refs[name]}, type: 3}}\n    volume: {spec['volume']}\n    pitchJitter: 0\n    maxDuration: 0\n"
        pattern = rf"^  {field}:\n.*?(?=^  \w|\Z)"
        if re.search(pattern, text, re.M | re.S): text = re.sub(pattern, block, text, flags=re.M | re.S)
        else: text += ("" if text.endswith("\n") else "\n") + block
    library.write_text(text)
    notes = ROOT / "Docs/Audio/GiftFestival"
    notes.mkdir(parents=True, exist_ok=True)
    (notes / "AudioProcessing.json").write_text(json.dumps(report, ensure_ascii=False, indent=2)+"\n")


if __name__ == "__main__": main()
