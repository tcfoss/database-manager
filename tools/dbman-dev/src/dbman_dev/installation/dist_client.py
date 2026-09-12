"""Client for the DatabaseManager distribution API."""

import json as _json
import pathlib as _pathlib
import re as _re
import sys as _sys
import urllib.request as _request

import semver as _semver


class DistClient:
    """Client for the distribution server API."""

    def __init__(self, base_url: str, token: str):
        """Initialize the client.

        Parameters
        ----------
        base_url : str
            The base URL of the distribution server (the part before ``/api.php``).
        token : str
            The authentication token.
        """
        self._api_url = base_url.rstrip("/") + "/api.php"
        self._token = token

    def _get_json(self, resource: str, **params: str) -> dict:
        """Make an authenticated GET request and return the parsed JSON response."""

        url = f"{self._api_url}?resource={resource}"
        for key, value in params.items():
            url += f"&{key}={value}"

        req = _request.Request(url)
        req.add_header("Authorization", f"Bearer {self._token}")

        with _request.urlopen(req) as resp:
            return _json.loads(resp.read().decode())

    def _get_all_versions(self, resource: str) -> list[tuple[_semver.Version, int]]:
        """Fetch all versions from a paginated version endpoint.

        Returns a list of ``(version, id)`` tuples sorted newest-first.
        """

        data = self._get_json(resource, per_page="200")
        versions: list[tuple[_semver.Version, int]] = []
        for item in data["data"]:
            v = _semver.Version(
                major=item["major"],
                minor=item["minor"],
                patch=item["patch"],
            )
            versions.append((v, item["id"]))
        versions.sort(key=lambda x: x[0], reverse=True)
        return versions

    def get_latest_installer_version(self) -> tuple[_semver.Version, int] | None:
        """Return ``(version, id)`` for the latest installer version, or ``None``."""

        versions = self._get_all_versions("installer_versions")
        return versions[0] if versions else None

    def get_latest_app_version(self) -> tuple[_semver.Version, int] | None:
        """Return ``(version, id)`` for the latest app version, or ``None``."""

        versions = self._get_all_versions("app_versions")
        return versions[0] if versions else None

    def _find_file_id(
        self, resource: str, version_id_field: str, version_id: int, arch: str
    ) -> int | None:
        """Find a file ID from a file listing matching a version and architecture filter."""

        data = self._get_json(resource, arch=arch, per_page="200")
        for item in data["data"]:
            if item[version_id_field] == version_id:
                return item["id"]
        return None

    def _download_file(
        self, resource: str, file_id: int, dest_dir: _pathlib.Path
    ) -> _pathlib.Path:
        """Download a file by ID and save it into *dest_dir*."""

        url = f"{self._api_url}?resource={resource}&id={file_id}"
        req = _request.Request(url)
        req.add_header("Authorization", f"Bearer {self._token}")

        with _request.urlopen(req) as resp:
            cd = resp.headers.get("Content-Disposition", "")
            match = _re.search(r'filename="?([^";\s]+)"?', cd)
            filename = match.group(1) if match else f"{resource}-{file_id}"

            dest = dest_dir / filename
            with open(dest, "wb") as f:
                while chunk := resp.read(8192):
                    f.write(chunk)

        return dest

    def download_installer(
        self, version_id: int, arch: str, dest_dir: _pathlib.Path
    ) -> _pathlib.Path:
        """Download an installer file for the given version and architecture."""

        file_id = self._find_file_id(
            "installers", "installer_version_id", version_id, arch
        )
        if file_id is None:
            print(
                f"No installer file found for architecture '{arch}' "
                f"and version ID {version_id}. Exiting."
            )
            _sys.exit(3)
        return self._download_file("installers", file_id, dest_dir)

    def download_app(
        self, version_id: int, arch: str, dest_dir: _pathlib.Path
    ) -> _pathlib.Path:
        """Download an app file for the given version and architecture."""

        file_id = self._find_file_id("apps", "app_version_id", version_id, arch)
        if file_id is None:
            print(
                f"No app file found for architecture '{arch}' "
                f"and version ID {version_id}. Exiting."
            )
            _sys.exit(3)
        return self._download_file("apps", file_id, dest_dir)
