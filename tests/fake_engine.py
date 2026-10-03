#!/usr/bin/env python3
"""Deterministic, network-free downloader stand-in for integration tests only."""
import json
import os
from pathlib import Path
import shutil
import sys
import time

args = sys.argv[1:]
stage = Path(args[args.index('-P') + 1])
fixture = Path(os.environ['BILI_TEST_FIXTURE'])
mode = os.environ.get('BILI_TEST_MODE', 'ok')
if '--cookies' in args:
    data = Path(args[args.index('--cookies') + 1]).read_text()
    assert 'foreignvalue' not in data and 'fakevalue' not in data
if mode in ('fail', 'wait'):
    (stage / 'source.part').write_bytes(b'partial download')
    if mode == 'wait':
        time.sleep(30)
    print('ERROR: HTTP 412 SESSDATA=privatevalue', file=sys.stderr)
    sys.exit(1)
target = stage / ('source' + fixture.suffix)
shutil.copyfile(fixture, target)
if mode == 'preview':
    print('WARNING: only the preview is available', file=sys.stderr)
print('[download] 100.0%')
print('__BILI_RESULT__' + json.dumps({
    'path': str(fixture if mode == 'outside' else target),
    'title': '绿幕测试 角色 😀', 'id': 'BV13x41117TL'
}, ensure_ascii=False))
