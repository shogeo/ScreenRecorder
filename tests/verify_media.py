"""Validate encoded output independently of recorder state and UI labels."""
import array
import json
import math
from pathlib import Path
import shutil
import subprocess

ROOT = Path(__file__).resolve().parent.parent
RESULTS = ROOT / 'tests/results'

def executable(name):
    installed = shutil.which(name)
    if installed:
        return installed
    return str(next((ROOT / '.tools/ffmpeg').rglob(name + '.exe')))

ffmpeg, ffprobe = executable('ffmpeg'), executable('ffprobe')
report = json.loads((RESULTS / 'integration.json').read_text())
assert report['success'], report['error']
media = []
for path in report['files']:
    info = json.loads(subprocess.check_output([ffprobe, '-v', 'error', '-show_streams', '-show_format', '-of', 'json', path]))
    video = next(s for s in info['streams'] if s['codec_type'] == 'video')
    audio = next(s for s in info['streams'] if s['codec_type'] == 'audio')
    assert video['codec_name'] == 'h264' and audio['codec_name'] == 'aac'
    assert video['r_frame_rate'] == '60/1'
    assert audio['sample_rate'] == '48000' and audio['channels'] == 2
    assert float(info['format']['duration']) > 1
    assert abs(float(video['duration']) - float(audio['duration'])) < .15
    subprocess.run([ffmpeg, '-v', 'error', '-xerror', '-i', path, '-f', 'null', '-'], check=True, capture_output=True)
    media.append({'file': Path(path).name, 'duration': info['format']['duration'], 'size': [video['width'], video['height']], 'fullDecode': True})

path = str(RESULTS / 'hotkeys-toggle.mp4')
pcm = subprocess.check_output([ffmpeg, '-v', 'error', '-i', path, '-map', '0:a:0', '-ac', '1', '-ar', '48000', '-f', 's16le', '-'])
samples = array.array('h', pcm)
def rms(start, end):
    segment = samples[int(start*48000):int(end*48000)]
    return math.sqrt(sum(v*v for v in segment) / len(segment))
levels = {name: rms(a,b) for name,a,b in [('silentStart',.5,1.5), ('systemOn',2.6,3.8), ('systemOff',4.8,5.8), ('micOn',6.8,7.6), ('bothOn',9.5,10.5), ('bothOff',11.5,12.2)]}
for key in ['silentStart', 'systemOff', 'bothOff']:
    assert levels[key] < 2, (key, levels[key])
assert levels['systemOn'] > 100 and levels['bothOn'] > 100, levels
# Microphone energy depends on the environment; device startup/toggle is checked by the integration test.
# The test card's top-right is uniform. A visible HUD or a black exclusion rectangle would change it.
raw = subprocess.check_output([ffmpeg, '-v', 'error', '-ss', '3', '-i', path, '-vf', 'crop=360:50:1540:20', '-frames:v', '1', '-pix_fmt', 'rgb24', '-f', 'rawvideo', '-'])
average = [sum(raw[channel::3]) / (len(raw)//3) for channel in range(3)]
assert max(abs(a-b) for a,b in zip(average, [17,19,25])) < 5, ('indicator not excluded', average)
output = {'success': True, 'files': media, 'audioRms': levels, 'indicatorExcluded': True}
(RESULTS / 'media.json').write_text(json.dumps(output, indent=2))
print(json.dumps(output, indent=2))
