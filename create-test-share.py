#!/usr/bin/env python3
"""
Creates (or extends) a large test share for slskd browse/performance testing.

First run  : creates 10,000 folders and 100,000 files.
Subsequent : adds another FILES_PER_RUN files to the existing tree.

Usage:
  python create-test-share.py [ROOT_DIR]

Edit the constants below to change targets.
"""

import os
import random
import sys
import time
import uuid

# ── Configuration ──────────────────────────────────────────────────────────────

ROOT_DIR      = r"C:\TestShare"   # override via argv[1]
TOTAL_DIRS    = 100_000           # folders to create (including root)
MIN_DEPTH     = 10                # guaranteed nesting depth on one branch
MAX_DEPTH     = 30                # cap on any single path
FILES_PER_RUN = 900_000           # files added each time the script runs
STRUCT_SEED   = 42                # keeps folder layout identical across runs

# Dirs below this index were created in the first run without UUIDs in their
# names.  Keep that naming so re-runs don't try to recreate them.
_LEGACY_DIR_COUNT = 10_000

# ── Minimal ~1 KB valid MP3 ────────────────────────────────────────────────────
# ID3v2.3 header (no tag frames) + MPEG-1 Layer 3 frame header (320 kbps /
# 44100 Hz / stereo) + zero-padded silence.  Long enough for metadata readers
# to identify the bitrate; duration will show as ~0 s which is fine for testing.

_MP3 = (
    b'ID3\x03\x00\x00\x00\x00\x00\x00'   # ID3v2.3, 0-byte payload
    + b'\xff\xfb\xe4\x00'                  # MPEG-1 L3, 320 kbps, 44100 Hz, stereo
    + b'\x00' * 1010                        # pad to ~1 KB
)

# ── Helpers ────────────────────────────────────────────────────────────────────

# \\?\ prefix bypasses Windows MAX_PATH (260 chars), allowing paths up to 32 KB.
_EXT = '\\\\?\\'

def fullpath(rel):
    p = os.path.join(ROOT_DIR, rel) if rel else ROOT_DIR
    return _EXT + p if not p.startswith(_EXT) else p


def build_dir_list():
    """Return (rel_paths, max_depth).  Always identical for the same STRUCT_SEED."""
    rng   = random.Random(STRUCT_SEED)
    rels  = ['']   # '' represents ROOT_DIR itself
    deps  = [0]

    # Guarantee at least one MIN_DEPTH-deep branch
    cur = 0
    for d in range(MIN_DEPTH):
        seg  = f'depth_{d + 1:02d}'
        path = os.path.join(rels[cur], seg) if rels[cur] else seg
        rels.append(path)
        deps.append(d + 1)
        cur = len(rels) - 1

    # Fill remaining slots with random children
    attempts = 0
    while len(rels) < TOTAL_DIRS:
        attempts += 1
        if attempts > TOTAL_DIRS * 30:
            break
        p = rng.randint(0, len(rels) - 1)
        if deps[p] >= MAX_DEPTH:
            continue
        idx = len(rels)
        if idx < _LEGACY_DIR_COUNT:
            # Original run: no UUID, keep names identical so we skip existing dirs
            seg = f'dir_{idx:05d}'
        else:
            # New dirs: UUID fragment makes paths look like real music library paths
            uid = uuid.uuid5(uuid.NAMESPACE_X500, f'{STRUCT_SEED}:{idx}').hex[:8]
            seg = f'dir_{idx:06d}_{uid}'
        path = os.path.join(rels[p], seg) if rels[p] else seg
        rels.append(path)
        deps.append(deps[p] + 1)

    return rels, max(deps)


def ensure_dirs(rels):
    new = 0
    for rel in rels:
        fp = fullpath(rel)
        if not os.path.isdir(fp):
            os.makedirs(fp, exist_ok=True)
            new += 1
    return new


def scan_existing(rels):
    """Return dict rel -> next_file_index for each dir."""
    next_n = {}
    for rel in rels:
        hi = 0
        try:
            for name in os.listdir(fullpath(rel)):
                if name.startswith('track_') and name.endswith('.mp3'):
                    try:
                        n = int(name[6:-4])
                        if n >= hi:
                            hi = n + 1
                    except ValueError:
                        pass
        except OSError:
            pass
        next_n[rel] = hi
    return next_n


def add_files(rels, next_n, count):
    rng   = random.Random()
    t0    = time.time()
    for i in range(count):
        rel = rng.choice(rels)
        n   = next_n[rel]
        fp  = os.path.join(fullpath(rel), f'track_{n:07d}.mp3')
        with open(fp, 'wb') as f:
            f.write(_MP3)
        next_n[rel] = n + 1
        if (i + 1) % 10_000 == 0:
            elapsed = time.time() - t0
            rate    = (i + 1) / elapsed
            remain  = (count - i - 1) / rate
            print(f'  {i + 1:,} / {count:,}  '
                  f'({rate:,.0f} files/s, ~{remain:.0f}s remaining)',
                  flush=True)


# ── Entry point ────────────────────────────────────────────────────────────────

def main():
    global ROOT_DIR
    if len(sys.argv) > 1:
        ROOT_DIR = sys.argv[1]

    print(f'Root      : {ROOT_DIR}')
    print(f'Target    : {TOTAL_DIRS:,} dirs, +{FILES_PER_RUN:,} files this run')
    print()

    print('Building directory list...', end=' ', flush=True)
    rels, max_depth = build_dir_list()
    print(f'{len(rels):,} dirs, max depth {max_depth}')

    # Depth distribution summary
    from collections import Counter
    rng2  = random.Random(STRUCT_SEED)
    deps2 = [0] + list(range(1, MIN_DEPTH + 1))  # approximate
    print(f'  Deepest guaranteed branch: {MIN_DEPTH} levels')
    print(f'  Overall max depth found  : {max_depth} levels')
    print()

    print('Ensuring directories exist...')
    new_dirs = ensure_dirs(rels)
    print(f'  {new_dirs:,} created, {len(rels) - new_dirs:,} already existed')
    print()

    print('Scanning existing files...', end=' ', flush=True)
    next_n   = scan_existing(rels)
    existing = sum(next_n.values())
    print(f'{existing:,} found')
    print()

    print(f'Adding {FILES_PER_RUN:,} files...')
    t0 = time.time()
    add_files(rels, next_n, FILES_PER_RUN)
    elapsed = time.time() - t0

    total = existing + FILES_PER_RUN
    print()
    print(f'Done in {elapsed:.1f}s  ({FILES_PER_RUN / elapsed:,.0f} files/s)')
    print(f'Total files in share: ~{total:,}')
    print(f'Add the following path as a shared directory in slskd:')
    print(f'  {ROOT_DIR}')


if __name__ == '__main__':
    main()
