"""Bounded Topaz-8 MUImem table-label geometry check from a read-only capture.

This is specific to the A500 diagnostic's default 8-pixel font. It is not a
general MUI text measurement oracle or a test of live counter correctness.
"""
import argparse, json, re
from pathlib import Path

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('capture', type=Path, help='interaction capture JSON')
p.add_argument('--output', type=Path, required=True)
a = p.parse_args()
objects = json.loads(a.capture.read_text())['state']['Objects']
windows = [o['Object'] for o in objects if o['Class'] == 'Window.mui' and o['NativeWindow'] != 0]
labels = ['Chip', 'Fast', 'Total', 'Available', 'In-Use', 'Largest', 'Maximum']
observations = []
for label in labels:
    found = [o for o in objects if o['Class'] == 'Text.mui' and o['Visible'] and
             o['ContainingWindow'] in windows and o['Width'] > 0 and
             re.sub(r'\x1b[bcln]', '', o['Text']) == label]
    observations.append(dict(label=label, expectedMinimumWidth=8*len(label),
        matches=[dict(object=hex(o['Object']), left=o['Left'], top=o['Top'], width=o['Width'], height=o['Height']) for o in found],
        passed=len(found) == 1 and found[0]['Width'] >= 8*len(label)))
headers = sorted([o['matches'][0] for o in observations[:3] if len(o['matches']) == 1], key=lambda o: o['left'])
headersSeparated = len(headers) == 3 and all(x['left'] + x['width'] <= y['left'] for x, y in zip(headers, headers[1:]))
result = dict(protocol='muimem-topaz8-label-geometry-v1', capture=str(a.capture),
              scope='KNOWN_STATIC_LABEL_WIDTHS_AND_HEADER_SEPARATION_ONLY', observations=observations,
              headersSeparated=headersSeparated,
              verdict='PASS_LABEL_GEOMETRY' if headersSeparated and all(o['passed'] for o in observations) else 'FAIL_OR_UNAVAILABLE_LABEL_GEOMETRY')
a.output.write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps(result))
