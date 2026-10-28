#!/usr/bin/env python3
"""Use the original user-provided 256x256 Sirroh Skills shield in Thunderstore."""
import pathlib
import shutil
import struct
import sys

source = pathlib.Path("assets/thunderstore/icon.png")
dest = pathlib.Path(sys.argv[1] if len(sys.argv) > 1 else "icon.png")
data = source.read_bytes()
assert data[:8] == bytes((137, 80, 78, 71, 13, 10, 26, 10)), "Invalid PNG signature"
width, height = struct.unpack(">II", data[16:24])
assert (width, height) == (256, 256), "Thunderstore requires 256 x 256 PNG"
dest.parent.mkdir(parents=True, exist_ok=True)
shutil.copyfile(source, dest)
assert dest.read_bytes() == data
print("Copied original Thunderstore icon:", dest)
