#!/usr/bin/env python3
"""Produce a small original, flat PNG icon required by Thunderstore packages."""
import pathlib
import struct
import sys
import zlib

N = 256
rgba = bytearray(N*N*4)
for y in range(N):
    for x in range(N):
        ax = x - 128
        ay = y - 128
        inside = ax*ax + ay*ay <= 115*115
        if not inside:
            continue
        # Muted burgundy shield with a simple steel-colored diagonal blade.
        outline = ax*ax + ay*ay >= 108*108
        handle = abs(x - (128 + (y-128)//5)) < 12 and 60 < y < 207
        blade = 52 < x < 189 and 55 < y < 171 and abs(y - x + 26) < 21
        if outline:
            color = (96, 82, 85, 255)
        elif blade:
            color = (194, 193, 187, 255)
        elif handle:
            color = (126, 119, 109, 255)
        else:
            color = (78, 32, 48, 255)
        rgba[(y*N+x)*4:(y*N+x+1)*4] = bytes(color)

def chunk(kind, data):
    return struct.pack('!I',len(data)) + kind + data + struct.pack('!I',zlib.crc32(kind+data)&0xffffffff)
raw=b''.join(b'\\0'+rgba[i*N*4:(i+1)*N*4] for i in range(N))
png=b'\\x89PNG\\r\\n\\x1a\\n'+chunk(b'IHDR',struct.pack('!2I5B',N,N,8,6,0,0,0))+chunk(b'IDAT',zlib.compress(raw,9))+chunk(b'IEND',b'')
path=pathlib.Path(sys.argv[1] if len(sys.argv)>1 else 'icon.png')
path.parent.mkdir(parents=True,exist_ok=True)
path.write_bytes(png)
print('Generated',path,len(png),'bytes')
