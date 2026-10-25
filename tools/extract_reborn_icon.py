"""Copy the original Reborn executable icon, without changing its artwork.
Usage: python tools/extract_reborn_icon.py path/to/Game.exe
Maintainer dependency: pefile (also supplied by PyInstaller).
"""
from pathlib import Path
import struct
import sys
import pefile

exe = pefile.PE(sys.argv[1])
icons = {}
group = None
for kind in exe.DIRECTORY_ENTRY_RESOURCE.entries:
    if kind.id not in [3, 14]: continue
    for entry in kind.directory.entries:
        data = entry.directory.entries[0].data.struct
        blob = exe.get_data(data.OffsetToData, data.Size)
        if kind.id == 3: icons[entry.id] = blob
        elif group is None: group = blob
if group is None: raise SystemExit('Game.exe enthält kein Icon.')
count = struct.unpack('<H', group[4:6])[0]
header, payload = bytearray(group[:6]), bytearray()
offset = 6 + 16 * count
for i in range(count):
    entry = group[6+14*i:20+14*i]
    blob = icons[struct.unpack('<H', entry[12:14])[0]]
    header.extend(entry[:8] + struct.pack('<II', len(blob), offset))
    payload.extend(blob)
    offset += len(blob)
destination = Path(__file__).resolve().parents[1]/'src/RebornLauncher/assets/reborn.ico'
destination.write_bytes(header + payload)
print(destination)
