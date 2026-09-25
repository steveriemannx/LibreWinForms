#!/usr/bin/env python3
"""Preprocess canonical Accessibility IL for Mono ILAsm on FreeBSD."""

from pathlib import Path
import subprocess
import sys


source = Path(sys.argv[1]).resolve()
destination = Path(sys.argv[2]).resolve()
preprocessed = subprocess.run(
    ["cpp", "-P", "-x", "c", source.name],
    cwd=source.parent,
    check=True,
    capture_output=True,
    text=True,
).stdout

# Microsoft's ILAsm accepts quoted assembly-name macros; Mono ILAsm expects the
# equivalent unquoted IL identifiers.
preprocessed = preprocessed.replace('["System.Runtime"]', "[System.Runtime]")
preprocessed = preprocessed.replace(
    '["System.Runtime.InteropServices"]', "[System.Runtime.InteropServices]"
)
preprocessed = preprocessed.replace(
    '.assembly extern "System.Runtime"', ".assembly extern System.Runtime"
)
preprocessed = preprocessed.replace(
    '.assembly extern "System.Runtime.InteropServices"',
    ".assembly extern System.Runtime.InteropServices",
)

destination.parent.mkdir(parents=True, exist_ok=True)
destination.write_text(preprocessed)
