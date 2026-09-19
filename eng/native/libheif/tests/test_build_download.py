from __future__ import annotations

import hashlib
import importlib.util
import io
import tempfile
import unittest
import urllib.error
from pathlib import Path


BUILD_PATH = Path(__file__).resolve().parents[1] / "build.py"
SPEC = importlib.util.spec_from_file_location("fovium_libheif_build", BUILD_PATH)
assert SPEC is not None and SPEC.loader is not None
BUILD = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(BUILD)


class _Response(io.BytesIO):
    def __enter__(self) -> "_Response":
        return self

    def __exit__(self, *_: object) -> None:
        self.close()


class _ResettingResponse:
    def __init__(self, partial_payload: bytes) -> None:
        self._partial_payload = partial_payload
        self._read_count = 0

    def __enter__(self) -> "_ResettingResponse":
        return self

    def __exit__(self, *_: object) -> None:
        return None

    def read(self, _size: int = -1) -> bytes:
        self._read_count += 1
        if self._read_count == 1:
            return self._partial_payload
        raise ConnectionResetError("peer reset after a partial response")


class DownloadVerifiedArchiveTests(unittest.TestCase):
    def setUp(self) -> None:
        self._temporary = tempfile.TemporaryDirectory(prefix="Fovium.NativeDownload.Tests.")
        self.downloads = Path(self._temporary.name)
        self.payload = b"pinned archive payload"
        self.expected_hash = hashlib.sha256(self.payload).hexdigest()
        self.component = {
            "archiveFile": "dependency.tar.gz",
            "archiveUrl": "https://example.invalid/dependency.tar.gz",
            "archiveSha256": self.expected_hash,
        }

    def tearDown(self) -> None:
        self._temporary.cleanup()

    def test_transient_504_then_transient_503_then_success_is_bounded(self) -> None:
        outcomes = [self._http_error(504), self._http_error(503), _Response(self.payload)]
        delays: list[float] = []

        def opener(*_: object, **__: object) -> _Response:
            outcome = outcomes.pop(0)
            if isinstance(outcome, Exception):
                raise outcome
            return outcome

        archive = BUILD.download_verified_archive(
            self.component,
            self.downloads,
            opener=opener,
            sleep=delays.append,
        )

        self.assertEqual(self.payload, archive.read_bytes())
        self.assertEqual([1.0, 2.0], delays)
        self.assertEqual([], outcomes)
        self.assertEqual([], list(self.downloads.glob("*.part")))

    def test_permanent_404_is_not_retried(self) -> None:
        calls = 0

        def opener(*_: object, **__: object) -> _Response:
            nonlocal calls
            calls += 1
            raise self._http_error(404)

        with self.assertRaises(urllib.error.HTTPError):
            BUILD.download_verified_archive(
                self.component,
                self.downloads,
                opener=opener,
                sleep=lambda _: self.fail("404 must not back off"),
            )

        self.assertEqual(1, calls)
        self.assertFalse((self.downloads / self.component["archiveFile"]).exists())
        self.assertEqual([], list(self.downloads.glob("*.part")))

    def test_connection_reset_is_retried_then_succeeds(self) -> None:
        calls = 0
        delays: list[float] = []

        def opener(*_: object, **__: object) -> _Response:
            nonlocal calls
            calls += 1
            if calls == 1:
                raise ConnectionResetError("peer reset")
            return _Response(self.payload)

        archive = BUILD.download_verified_archive(
            self.component,
            self.downloads,
            opener=opener,
            sleep=delays.append,
        )

        self.assertEqual(2, calls)
        self.assertEqual([1.0], delays)
        self.assertEqual(self.payload, archive.read_bytes())

    def test_partial_response_reset_never_promotes_part_and_next_attempt_succeeds(self) -> None:
        outcomes = [_ResettingResponse(self.payload[:7]), _Response(self.payload)]
        delays: list[float] = []

        def opener(*_: object, **__: object) -> _ResettingResponse | _Response:
            return outcomes.pop(0)

        archive = BUILD.download_verified_archive(
            self.component,
            self.downloads,
            opener=opener,
            sleep=delays.append,
        )

        self.assertEqual(self.payload, archive.read_bytes())
        self.assertEqual([1.0], delays)
        self.assertEqual([], outcomes)
        self.assertEqual([], list(self.downloads.glob("*.part")))

    def test_downloaded_hash_mismatch_is_a_single_hard_failure_and_leaves_no_cache(self) -> None:
        calls = 0

        def opener(*_: object, **__: object) -> _Response:
            nonlocal calls
            calls += 1
            return _Response(b"truncated")

        with self.assertRaisesRegex(RuntimeError, "hash mismatch"):
            BUILD.download_verified_archive(
                self.component,
                self.downloads,
                opener=opener,
                sleep=lambda _: self.fail("hash mismatch must not retry"),
            )

        self.assertEqual(1, calls)
        self.assertFalse((self.downloads / self.component["archiveFile"]).exists())
        self.assertEqual([], list(self.downloads.glob("*.part")))

    def test_existing_verified_cache_is_used_without_network(self) -> None:
        archive = self.downloads / self.component["archiveFile"]
        archive.write_bytes(self.payload)

        result = BUILD.download_verified_archive(
            self.component,
            self.downloads,
            opener=lambda *_args, **_kwargs: self.fail("verified cache must avoid network"),
            sleep=lambda _: self.fail("verified cache must not back off"),
        )

        self.assertEqual(archive, result)
        self.assertEqual(self.payload, result.read_bytes())

    def test_invalid_cached_archive_is_removed_before_one_fresh_verified_download(self) -> None:
        archive = self.downloads / self.component["archiveFile"]
        archive.write_bytes(b"stale partial cache")
        calls = 0

        def opener(*_: object, **__: object) -> _Response:
            nonlocal calls
            calls += 1
            self.assertFalse(archive.exists())
            return _Response(self.payload)

        result = BUILD.download_verified_archive(
            self.component,
            self.downloads,
            opener=opener,
            sleep=lambda _: self.fail("successful refresh must not back off"),
        )

        self.assertEqual(1, calls)
        self.assertEqual(self.payload, result.read_bytes())
        self.assertEqual([], list(self.downloads.glob("*.part")))

    def test_primary_source_success_does_not_open_fallback(self) -> None:
        urls: list[str] = []

        def opener(request: object, **_: object) -> _Response:
            urls.append(request.full_url)
            return _Response(self.payload)

        archive = BUILD.download_verified_archive(
            self._multi_component(),
            self.downloads,
            opener=opener,
            sleep=lambda _: self.fail("primary success must not back off"),
        )

        self.assertEqual("primary.tar.gz", archive.name)
        self.assertEqual(["https://primary.invalid/archive"], urls)

    def test_transient_primary_exhaustion_reaches_verified_fallback(self) -> None:
        calls: list[str] = []
        delays: list[float] = []

        def opener(request: object, **_: object) -> _Response:
            calls.append(request.full_url)
            if "primary" in request.full_url:
                raise TimeoutError("primary timed out")
            return _Response(self.payload)

        archive = BUILD.download_verified_archive(
            self._multi_component(),
            self.downloads,
            opener=opener,
            sleep=delays.append,
        )

        self.assertEqual("fallback.tar.gz", archive.name)
        self.assertEqual(4, calls.count("https://primary.invalid/archive"))
        self.assertEqual("https://fallback.invalid/archive", calls[-1])
        self.assertEqual([1.0, 2.0, 4.0], delays)
        self.assertEqual(self.payload, archive.read_bytes())

    def test_permanent_primary_not_found_moves_immediately_to_fallback(self) -> None:
        calls: list[str] = []

        def opener(request: object, **_: object) -> _Response:
            calls.append(request.full_url)
            if "primary" in request.full_url:
                raise self._http_error(404)
            return _Response(self.payload)

        archive = BUILD.download_verified_archive(
            self._multi_component(),
            self.downloads,
            opener=opener,
            sleep=lambda _: self.fail("404 must not back off"),
        )

        self.assertEqual("fallback.tar.gz", archive.name)
        self.assertEqual(2, len(calls))

    def test_primary_hash_mismatch_is_terminal_and_never_reaches_fallback(self) -> None:
        calls: list[str] = []

        def opener(request: object, **_: object) -> _Response:
            calls.append(request.full_url)
            return _Response(b"wrong archive bytes")

        with self.assertRaises(BUILD.ArchiveIntegrityError):
            BUILD.download_verified_archive(
                self._multi_component(),
                self.downloads,
                opener=opener,
                sleep=lambda _: self.fail("integrity failure must not back off"),
            )

        self.assertEqual(["https://primary.invalid/archive"], calls)
        self.assertFalse((self.downloads / "fallback.tar.gz").exists())

    def test_both_sources_failing_reports_bounded_combined_failure(self) -> None:
        calls: list[str] = []

        def opener(request: object, **_: object) -> _Response:
            calls.append(request.full_url)
            raise self._http_error(404)

        with self.assertRaisesRegex(RuntimeError, "All pinned archive sources failed"):
            BUILD.download_verified_archive(
                self._multi_component(),
                self.downloads,
                opener=opener,
                sleep=lambda _: self.fail("404 must not back off"),
            )

        self.assertEqual(2, len(calls))
        self.assertEqual([], list(self.downloads.glob("*.part")))

    def test_stale_partial_is_removed_before_cache_or_network_is_considered(self) -> None:
        stale = self.downloads / "dependency.tar.gz.crashed.part"
        stale.write_bytes(b"partial bytes from an interrupted process")

        result = BUILD.download_verified_archive(
            self.component,
            self.downloads,
            opener=lambda *_args, **_kwargs: _Response(self.payload),
            sleep=lambda _: self.fail("success must not back off"),
        )

        self.assertEqual(self.payload, result.read_bytes())
        self.assertFalse(stale.exists())

    def _multi_component(self) -> dict[str, object]:
        return {
            "archives": [
                {
                    "archiveFile": "primary.tar.gz",
                    "archiveUrl": "https://primary.invalid/archive",
                    "archiveSha256": self.expected_hash,
                },
                {
                    "archiveFile": "fallback.tar.gz",
                    "archiveUrl": "https://fallback.invalid/archive",
                    "archiveSha256": self.expected_hash,
                },
            ]
        }

    @staticmethod
    def _http_error(status: int) -> urllib.error.HTTPError:
        return urllib.error.HTTPError(
            "https://example.invalid/dependency.tar.gz",
            status,
            "test",
            hdrs=None,
            fp=None,
        )


if __name__ == "__main__":
    unittest.main()
