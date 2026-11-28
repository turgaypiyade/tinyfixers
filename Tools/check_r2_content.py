#!/usr/bin/env python3
"""Offline publisher validation tests; no Unity runs, levels, cloud writes or credentials."""
import copy
import hashlib
import subprocess
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch

import r2_content as content


class ContentTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / 'publish').mkdir()
        self.payload = b'opaque bundle bytes for publisher validation only'
        sha = hashlib.sha256(self.payload).hexdigest()
        self.bundle = {'id': 'levels-51', 'sha256': sha, 'file': sha + '.bundle', 'bytes': len(self.payload)}
        (self.root / 'publish' / self.bundle['file']).write_bytes(self.payload)
        self.manifest = {'schema': 2, 'platform': 'Android', 'baseUrl': 'https://content.example.com/Android',
            'cities': [], 'bundledLevels': 50, 'packLevels': 50, 'bundles': [self.bundle],
            'packs': [{'firstLevel': 51, 'lastLevel': 100, 'bundles': ['levels-51'], 'assets': []}]}
        self.save()

    def save(self):
        path = self.root / 'manifest.json'
        path.write_text(json.dumps(self.manifest))
        (self.root / 'plan.json').write_text(json.dumps({'manifestSha256': content.digest(path)}))

    def test_plan_does_not_read_credentials_or_upload(self):
        with patch.object(content, 'credentials', side_effect=AssertionError('credentials accessed')):
            result = content.publish(self.root, 'test-bucket', False)
        self.assertFalse(result['apply'])
        self.assertEqual(result['bytes'], len(self.payload))

    def test_corrupted_bundle_rejected(self):
        (self.root / 'publish' / self.bundle['file']).write_bytes(b'x' * len(self.payload))
        with self.assertRaises(ValueError): content.read_plan(self.root)

    def test_manifest_tampering_rejected(self):
        with (self.root / 'manifest.json').open('a') as f: f.write(' ')
        with self.assertRaises(ValueError): content.read_plan(self.root)

    def test_traversal_rejected(self):
        self.manifest['bundles'][0]['file'] = '../escape.bundle'; self.save()
        with self.assertRaises(ValueError): content.read_plan(self.root)

    def test_pack_gap_rejected(self):
        self.manifest['packs'][0]['firstLevel'] = 52; self.save()
        with self.assertRaises(ValueError): content.read_plan(self.root)

    def test_platform_path_mismatch_rejected(self):
        self.manifest['baseUrl'] = 'https://content.example.com/iOS'; self.save()
        with self.assertRaises(ValueError): content.read_plan(self.root)

    def add_city(self):
        bundle = dict(self.bundle, id='city-mis7')
        self.manifest['bundles'].append(bundle)
        city = {'cityId': 'mis7', 'firstLevel': 0, 'lastLevel': 0, 'bundles': [bundle['id']],
            'assets': [{'guid': 'city-source-' + suffix, 'path': 'assets/city' + suffix + '.png',
                'resourcePath': 'WorldTour/City_mis7' + suffix, 'bundle': bundle['id']} for suffix in ('', '_Site')]}
        self.manifest['cities'].append(city)
        return city

    def test_city_plan_is_independent_of_level_ranges(self):
        self.add_city(); self.save()
        manifest, _ = content.read_plan(self.root)
        self.assertEqual(manifest['cities'][0]['cityId'], 'mis7')

    def test_duplicate_city_rejected(self):
        self.add_city()
        self.manifest['cities'].append(copy.deepcopy(self.manifest['cities'][0])); self.save()
        with self.assertRaises(ValueError): content.read_plan(self.root)

    def test_shared_city_and_level_bundle_rejected(self):
        city = self.add_city()
        city['bundles'] = ['levels-51']
        for asset in city['assets']: asset['bundle'] = 'levels-51'
        self.save()
        with self.assertRaises(ValueError): content.read_plan(self.root)

    def test_missing_city_variant_rejected(self):
        self.add_city()['assets'].pop(); self.save()
        with self.assertRaises(ValueError): content.read_plan(self.root)

    def test_duplicate_city_source_rejected(self):
        city = self.add_city()
        city['assets'][1]['guid'] = city['assets'][0]['guid']; self.save()
        with self.assertRaises(ValueError): content.read_plan(self.root)

    def test_city_address_traversal_rejected(self):
        self.add_city()['assets'][0]['resourcePath'] = '../City_mis7'; self.save()
        with self.assertRaises(ValueError): content.read_plan(self.root)

    def test_city_missing_bundle_rejected(self):
        self.add_city()['bundles'] = ['missing']; self.save()
        with self.assertRaises(ValueError): content.read_plan(self.root)

    def curl_response(self, payload):
        def run(command, **kwargs):
            Path(command[command.index('--output') + 1]).write_bytes(payload)
            return subprocess.CompletedProcess(command, 0, '200', '')
        return run

    def test_verify_writes_receipt_only_after_matching_remote_bytes(self):
        with patch.object(content.subprocess, 'run', side_effect=self.curl_response(self.payload)):
            self.assertTrue(content.verify(self.root)['verified'])
        self.assertEqual((self.root / 'verified.sha256').read_text().strip(), content.digest(self.root / 'manifest.json'))

    def test_failed_verify_invalidates_previous_receipt(self):
        (self.root / 'verified.sha256').write_text('old success')
        with patch.object(content.subprocess, 'run', side_effect=self.curl_response(b'bad data')):
            with self.assertRaises(ValueError): content.verify(self.root)
        self.assertFalse((self.root / 'verified.sha256').exists())


if __name__ == '__main__': unittest.main()
