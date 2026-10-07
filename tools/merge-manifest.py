#!/usr/bin/env python3
"""Portable equivalent of mt -manifest A B -out:C for code-only WinUI builds.

The Windows build uses Microsoft's mt.exe. This helper merges only XML; it
retains the SDK-generated activation entries and never substitutes DLLs.
"""
from pathlib import Path
import sys
from xml.dom import minidom

args = sys.argv[1:]
start = args.index('-manifest') + 1
destination = next(arg[len('-out:'):] for arg in args if arg.startswith('-out:'))
sources = [arg for arg in args[start:] if not arg.startswith('-')]
if len(sources) != 2:
    raise SystemExit('Exactly two manifests are required.')
document = minidom.parse(sources[0])
other = minidom.parse(sources[1])
for index in range(other.documentElement.attributes.length):
    attribute = other.documentElement.attributes.item(index)
    if not document.documentElement.hasAttribute(attribute.name):
        document.documentElement.setAttribute(attribute.name, attribute.value)
for node in other.documentElement.childNodes:
    if node.nodeType == node.ELEMENT_NODE:
        document.documentElement.appendChild(document.importNode(node, True))
output = Path(destination)
output.parent.mkdir(parents=True, exist_ok=True)
output.write_bytes(document.toxml(encoding='utf-8'))
