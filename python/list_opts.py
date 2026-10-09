"""List and download SDGA option files."""

from __future__ import annotations
import base64
from concurrent.futures import ThreadPoolExecutor
import re
import sys
import threading
import time
import zlib
from dataclasses import dataclass
from pathlib import Path
from typing import Iterable
from urllib.parse import parse_qs, urlsplit, urlunsplit

import requests


INSTRUCTION_URL = "http://naominet.jp/sys/servlet/DownloadOrder"
INSTRUCTION_USER_AGENT = "ALL.Net"
GAME_ID = "SDGA"
TITLE_VERSION = "1.65"
CLIENT_ID = "A63E01E0048"
DOWNLOAD_USER_AGENT = CLIENT_ID
DOWNLOAD_DIR = Path("downloads")
SEGMENT_SIZE = 2 * 1024 * 1024
MAX_SEGMENTS = 4
MAX_RETRIES = 3
PROGRESS_WIDTH = 30
REQUEST_TIMEOUT = (10, 2)
@dataclass(frozen=True)
class OptionEntry:
    name: str
    url: str
    latest: bool


class DownloadCancelled(Exception):
    """Raised when the user requests cancellation."""


class DownloadProgress:
    def __init__(self, total_bytes: int) -> None:
        self.total_bytes = total_bytes
        self.downloaded = 0
        self.started = time.monotonic()
        self.last_update = 0.0
        self.lock = threading.Lock()

    def update(self, amount: int, *, force: bool = False) -> None:
        with self.lock:
            self.downloaded += amount
            now = time.monotonic()
            if not force and now - self.last_update < 0.2:
                return
            self.last_update = now
            elapsed = max(now - self.started, 0.001)
            speed = self.downloaded / elapsed
            fraction = min(self.downloaded / self.total_bytes, 1.0)
            filled = int(PROGRESS_WIDTH * fraction)
            bar = "#" * filled + "-" * (PROGRESS_WIDTH - filled)
            percent = fraction * 100
            sys.stdout.write(
                f"\r[{bar}] {percent:6.2f}% "
                f"{format_bytes(speed)}/s "
                f"({format_bytes(self.downloaded)}/{format_bytes(self.total_bytes)})"
            )
            sys.stdout.flush()

    def finish(self) -> None:
        self.update(0, force=True)
        sys.stdout.write("\r" + (" " * 140) + "\r")
        sys.stdout.write("\n")
        sys.stdout.flush()


def format_bytes(value: float) -> str:
    units = ("B", "KiB", "MiB", "GiB", "TiB")
    for unit in units:
        if value < 1024 or unit == units[-1]:
            return f"{value:.1f} {unit}"
        value /= 1024
    return f"{value:.1f} TiB"


def build_request() -> bytes:
    query = f"game_id={GAME_ID}&ver={TITLE_VERSION}&serial={CLIENT_ID}"
    compressed = zlib.compress(query.encode("utf-8"))
    return base64.b64encode(compressed)


def decode_response(response_body: bytes) -> str:
    try:
        compressed = base64.b64decode(response_body.strip(), validate=True)
        return zlib.decompress(compressed).decode("utf-8")
    except (ValueError, UnicodeDecodeError, zlib.error) as exc:
        raise ValueError("The SDGA instruction response was not valid base64/zlib data.") from exc


def fix_url(value: str) -> str:
    """Match Solve.FixUrl for URLs returned by the instruction service."""
    value = re.sub(r"^tps://|^ps://", "https://", value)
    value = re.sub(r"^ttp://", "http://", value)
    try:
        parsed = urlsplit(value)
        host = parsed.hostname
        if host is None:
            return value
        host = host.replace("_", ".")
        path = re.sub(r"(patch|option)_(\d+)_(\d+)", r"\1_\2.\3", parsed.path)
        path = re.sub(r"_([a-zA-Z0-9]+)$", r".\1", path)
        netloc = host
        if parsed.port is not None:
            netloc += f":{parsed.port}"
        return urlunsplit((parsed.scheme, netloc, path, parsed.query, parsed.fragment))
    except ValueError:
        return value


def manifest_urls(instruction_text: str) -> list[str]:
    values = parse_qs(instruction_text, keep_blank_values=True).get("uri", [])
    urls: list[str] = []
    for value in values:
        for item in value.split("|"):
            item = item.strip()
            if item and item.lower() != "null":
                urls.append(fix_url(item))
    return urls


def option_entries(manifest: str) -> Iterable[OptionEntry]:
    optional_marker = manifest.lower().find("[optional]")
    pattern = re.compile(r"(?mi)^\s*INSTALL\d+=\s*(https?://\S+)")
    entries: list[OptionEntry] = []
    for match in pattern.finditer(manifest):
        url = match.group(1).rstrip(";,)")
        name = urlsplit(url).path.rsplit("/", 1)[-1]
        if not name or name.lower() == "null":
            continue
        entries.append(
            OptionEntry(
                name=name,
                url=url,
                latest=optional_marker < 0 or match.start() < optional_marker,
            )
        )
    return sorted(entries, key=lambda entry: entry.name.casefold())


def fetch_instruction(session: requests.Session) -> str:
    response = session.post(
        INSTRUCTION_URL,
        data=build_request(),
        headers={
            "Pragma": "DFI",
            "User-Agent": INSTRUCTION_USER_AGENT,
            "Accept-Encoding": "identity",
            "Content-Type": "application/octet-stream",
        },
        timeout=30,
    )
    response.raise_for_status()
    return decode_response(response.content)


def fetch_manifest(session: requests.Session, url: str) -> str:
    response = session.get(
        url,
        headers={"User-Agent": DOWNLOAD_USER_AGENT},
        timeout=30,
    )
    response.raise_for_status()
    return response.text


def download_segment(
    session: requests.Session,
    url: str,
    output: Path,
    start: int,
    end: int,
    progress: DownloadProgress,
    cancel: threading.Event,
) -> None:
    written = 0
    for attempt in range(1, MAX_RETRIES + 1):
        if cancel.is_set():
            raise DownloadCancelled
        try:
            headers = {
                "User-Agent": DOWNLOAD_USER_AGENT,
                "Range": f"bytes={start + written}-{end}",
            }
            with session.get(
                url, headers=headers, stream=True, timeout=REQUEST_TIMEOUT
            ) as response:
                if response.status_code != 206:
                    raise RuntimeError(
                        f"range request returned HTTP {response.status_code}, expected 206"
                    )
                with output.open("r+b") as file:
                    file.seek(start + written)
                    for chunk in response.iter_content(chunk_size=81920):
                        if cancel.is_set():
                            raise DownloadCancelled
                        if not chunk:
                            continue
                        remaining = end - start + 1 - written
                        chunk = chunk[:remaining]
                        file.write(chunk)
                        written += len(chunk)
                        progress.update(len(chunk))
                        if written >= end - start + 1:
                            break
            if written == end - start + 1:
                return
        except DownloadCancelled:
            raise
        except (OSError, requests.RequestException, RuntimeError):
            if attempt < MAX_RETRIES:
                if cancel.wait(2):
                    raise DownloadCancelled
    raise RuntimeError(f"segment {start}-{end} failed after {MAX_RETRIES} attempts")


def download_segmented(
    session: requests.Session,
    url: str,
    output: Path,
    cancel: threading.Event,
) -> bool:
    response = session.head(
        url,
        headers={"User-Agent": DOWNLOAD_USER_AGENT},
        timeout=REQUEST_TIMEOUT,
    )
    response.raise_for_status()
    total_bytes = int(response.headers.get("Content-Length", "0"))
    if total_bytes <= 0:
        return False

    segment_count = min(MAX_SEGMENTS, (total_bytes + SEGMENT_SIZE - 1) // SEGMENT_SIZE)
    segment_count = max(1, segment_count)
    segment_length = (total_bytes + segment_count - 1) // segment_count
    segments = [
        (
            index * segment_length,
            min((index + 1) * segment_length - 1, total_bytes - 1),
        )
        for index in range(segment_count)
    ]
    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("wb") as file:
        file.truncate(total_bytes)

    progress = DownloadProgress(total_bytes)
    try:
        executor = ThreadPoolExecutor(max_workers=segment_count)
        futures = [
            executor.submit(
                download_segment,
                session,
                url,
                output,
                start,
                end,
                progress,
                cancel,
            )
            for start, end in segments
        ]
        try:
            for future in futures:
                future.result()
        except KeyboardInterrupt as exc:
            cancel.set()
            executor.shutdown(wait=False, cancel_futures=True)
            raise DownloadCancelled from exc
        except DownloadCancelled:
            cancel.set()
            executor.shutdown(wait=False, cancel_futures=True)
            raise
        except BaseException:
            cancel.set()
            executor.shutdown(wait=False, cancel_futures=True)
            raise
        else:
            executor.shutdown(wait=True)
        progress.finish()
        return True
    except DownloadCancelled:
        cancel.set()
        raise
    except (OSError, requests.RequestException, RuntimeError):
        cancel.set()
        progress.finish()
        output.unlink(missing_ok=True)
        return False


def download_with_retry(
    session: requests.Session,
    url: str,
    output: Path,
    cancel: threading.Event,
) -> bool:
    for attempt in range(1, MAX_RETRIES + 1):
        if cancel.is_set():
            raise DownloadCancelled
        try:
            already_downloaded = output.stat().st_size if output.exists() else 0
            headers = {"User-Agent": DOWNLOAD_USER_AGENT}
            if already_downloaded:
                headers["Range"] = f"bytes={already_downloaded}-"
            with session.get(
                url, headers=headers, stream=True, timeout=REQUEST_TIMEOUT
            ) as response:
                if response.status_code == 416:
                    return True
                response.raise_for_status()
                total_bytes = already_downloaded + int(
                    response.headers.get("Content-Length", "0")
                )
                progress = DownloadProgress(total_bytes)
                output.parent.mkdir(parents=True, exist_ok=True)
                with output.open("ab") as file:
                    for chunk in response.iter_content(chunk_size=81920):
                        if cancel.is_set():
                            raise DownloadCancelled
                        if chunk:
                            file.write(chunk)
                            progress.update(len(chunk))
                progress.finish()
            return True
        except DownloadCancelled:
            raise
        except (OSError, requests.RequestException):
            if attempt < MAX_RETRIES:
                if cancel.wait(2):
                    raise DownloadCancelled
    return False


def download_file(session: requests.Session, url: str, output: Path) -> None:
    cancel = threading.Event()
    try:
        if download_segmented(session, url, output, cancel):
            return
        output.unlink(missing_ok=True)
        if not download_with_retry(session, url, output, cancel):
            raise RuntimeError(f"download failed: {url}")
    except (DownloadCancelled, KeyboardInterrupt) as exc:
        cancel.set()
        output.unlink(missing_ok=True)
        if isinstance(exc, KeyboardInterrupt):
            raise DownloadCancelled from exc
        raise


def download_entries(
    session: requests.Session,
    entries: list[OptionEntry],
    indexes: Iterable[int],
    download_dir: Path,
) -> None:
    for index in indexes:
        entry = entries[index]
        destination = download_dir / entry.name
        if destination.exists():
            print(f"Already downloaded {entry.name} -> {destination}")
            continue
        print(f"Downloading {entry.name} -> {destination}")
        download_file(session, entry.url, destination)
        print(f"Downloaded {entry.name}")


def read_selection(text: str, entry_count: int) -> list[int]:
    indexes: set[int] = set()
    for value in text.split(","):
        value = value.strip()
        if not value:
            continue
        try:
            number = int(value)
        except ValueError as exc:
            raise ValueError(f"Invalid selection: {value}") from exc
        if not 1 <= number <= entry_count:
            raise ValueError(f"Selection must be between 1 and {entry_count}: {number}")
        indexes.add(number - 1)
    if not indexes:
        raise ValueError("Choose at least one file.")
    return sorted(indexes)


def interactive_download(
    session: requests.Session,
    entries: list[OptionEntry],
    download_dir: Path,
) -> None:
    if not entries:
        print("\nNo files are available to download.")
        return

    print("\nDownload menu")
    print("  1. Download selected")
    print("  2. Download all")
    print("  3. Exit/back")
    while True:
        try:
            choice = input("\nChoose an action [1-3]: ").strip().lower()
        except (EOFError, KeyboardInterrupt):
            print()
            return
        if choice in {"3", "back", "exit", "b", "q"}:
            return
        if choice in {"2", "all", "a"}:
            download_entries(session, entries, range(len(entries)), download_dir)
            return
        if choice in {"1", "selected", "s"}:
            while True:
                try:
                    selection = input(
                        "Choose which files to download (for example, 1, 3): "
                    )
                    indexes = read_selection(selection, len(entries))
                    break
                except (EOFError, KeyboardInterrupt):
                    print()
                    return
                except ValueError as exc:
                    print(exc)
            download_entries(session, entries, indexes, download_dir)
            return
        print("Please choose 1, 2, or 3.")


def main() -> int:
    try:
        with requests.Session() as session:
            instruction = fetch_instruction(session)
            manifests = manifest_urls(instruction)
            if not manifests:
                raise RuntimeError("The instruction response did not contain a manifest URL.")

            print(f"Instruction manifests: {len(manifests)}")
            all_entries: list[OptionEntry] = []
            for manifest_number, manifest_url in enumerate(manifests, start=1):
                print(f"\nManifest {manifest_number}: {manifest_url}")
                entries = list(option_entries(fetch_manifest(session, manifest_url)))
                all_entries.extend(entries)
                if not entries:
                    print("  No INSTALL entries found.")
                    continue
            print("\nAvailable options:")
            for index, entry in enumerate(all_entries, start=1):
                kind = "latest" if entry.latest else "optional"
                print(f"  {index}. [{kind}] {entry.name}")
            interactive_download(session, all_entries, DOWNLOAD_DIR)
    except DownloadCancelled:
        print("\nDownload cancelled.", file=sys.stderr)
        return 130
    except KeyboardInterrupt:
        print("\nOperation cancelled.", file=sys.stderr)
        return 130
    except (requests.RequestException, RuntimeError, ValueError) as exc:
        print(f"Unable to list options: {exc}", file=sys.stderr)
        return 1

    return 0


if __name__ == "__main__":
    raise SystemExit(main())
