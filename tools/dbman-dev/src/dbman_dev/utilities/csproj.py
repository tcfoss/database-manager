"""Functions for extracting and updating information in .csproj files."""

import typing as _tp
from pathlib import Path as _Path
from xml.etree import ElementTree as _ET
import semver as _sv

from dbman_dev.utilities.property_update_mode import PropertyUpdateMode


@_tp.overload
def get_version(csproj_path: _Path, error_on_not_found: _tp.Literal[True]) -> _sv.Version: ...


@_tp.overload
def get_version(
    csproj_path: _Path, error_on_not_found: _tp.Literal[False]
) -> _sv.Version | None: ...


@_tp.overload
def get_version(csproj_path: _Path, error_on_not_found: bool) -> _sv.Version | None: ...


@_tp.overload
def get_version(csproj_path: _Path) -> _sv.Version: ...


def get_version(csproj_path: _Path, error_on_not_found: bool = True) -> _sv.Version | None:
    """Return the version from the given .csproj file, or None if not found.

    Parameters
    ----------
    csproj_path : Path
        The path to the .csproj file.
    error_on_not_found : bool, optional
        Whether to raise an error if the version is not found. Default is True.

    Returns
    -------
    semver.Version | None
        The version as a semver.Version object, or None if not found.
    """

    if not csproj_path.exists():
        raise FileNotFoundError(f".csproj file not found at {csproj_path}")

    tree = _ET.parse(csproj_path)
    root = tree.getroot()

    csproj_version = None

    for pg in root.findall("PropertyGroup"):
        ve = pg.find("Version")
        if ve is not None and ve.text:
            csproj_version = ve.text.strip()
            break

    if not csproj_version and error_on_not_found:
        raise ValueError(f"Version element not found in {csproj_path}")

    if not csproj_version:
        return None

    return _sv.Version.parse(csproj_version)


@_tp.overload
def get_package_id(csproj_path: _Path, error_on_not_found: _tp.Literal[True]) -> str: ...


@_tp.overload
def get_package_id(csproj_path: _Path, error_on_not_found: _tp.Literal[False]) -> str | None: ...


@_tp.overload
def get_package_id(csproj_path: _Path, error_on_not_found: bool) -> str | None: ...


@_tp.overload
def get_package_id(csproj_path: _Path) -> str: ...


def get_package_id(csproj_path: _Path, error_on_not_found: bool = True) -> str | None:
    """Return the PackageId from the given .csproj file, or None if not found.

    Parameters
    ----------
    csproj_path : Path
        The path to the .csproj file.
    """

    if not csproj_path.exists():
        raise FileNotFoundError(f".csproj file not found at {csproj_path}")

    tree = _ET.parse(csproj_path)
    root = tree.getroot()

    csproj_package_id = None

    for pg in root.findall("PropertyGroup"):
        pe = pg.find("PackageId")
        if pe is not None and pe.text:
            csproj_package_id = pe.text.strip()
            break

    if not csproj_package_id and error_on_not_found:
        raise ValueError(f"PackageId element not found in {csproj_path}")

    return csproj_package_id


def update_property(
    csproj_path: _Path, property_name: str, property_value: str, update_mode: PropertyUpdateMode
) -> str | None:
    """Update or add a property in the given csproj based on the update mode.

    Parameters
    ----------
    csproj_path : Path
        The path to the .csproj file.
    property_name : str
        The name of the property to update or add.
    property_value : str
        The value to set for the property.
    update_mode : PropertyUpdateMode
        The mode for updating the property.

    Returns
    -------
    str | None
        The old value of the property if it was updated, or None if it was added or skipped.
    """

    if update_mode == PropertyUpdateMode.SKIP:
        return None

    tree = _ET.parse(csproj_path)
    root = tree.getroot()

    property_elem = None
    for pg in root.findall("PropertyGroup"):
        pe = pg.find(property_name)
        if pe is not None:
            property_elem = pe
            break

    if property_elem is not None:
        old_value = property_elem.text
        property_elem.text = property_value
        tree.write(csproj_path, encoding="utf-8", xml_declaration=True)
        return old_value
    elif update_mode == PropertyUpdateMode.ADD_IF_ABSENT:
        pg_first = root.find("PropertyGroup")
        if pg_first is None:
            pg_first = _ET.SubElement(root, "PropertyGroup")
        new_property_elem = _ET.SubElement(pg_first, property_name)
        new_property_elem.text = property_value
        tree.write(csproj_path, encoding="utf-8", xml_declaration=True)
        return None

    return None


def update_version(
    csproj_path: _Path,
    version: _sv.Version,
    info_version_handling: PropertyUpdateMode = PropertyUpdateMode.UPDATE_IF_PRESENT,
    file_version_handling: PropertyUpdateMode = PropertyUpdateMode.UPDATE_IF_PRESENT,
    assembly_version_handling: PropertyUpdateMode = PropertyUpdateMode.UPDATE_IF_PRESENT,
) -> _sv.Version | None:
    """Update the Version element in the given csproj and return the old version.

    Parameters
    ----------
    csproj_path : Path
        The path to the .csproj file.
    version : semver.Version
        The new version to set.

    Returns
    -------
    str | None
        The old version before the update, or None if not found.
    """

    old_version = update_property(
        csproj_path, "Version", str(version), PropertyUpdateMode.ADD_IF_ABSENT
    )

    update_property(csproj_path, "InformationalVersion", str(version), info_version_handling)
    file_version = f"{version.major}.{version.minor}.{version.patch}.0"
    update_property(csproj_path, "FileVersion", file_version, file_version_handling)
    assembly_version = f"{version.major}.0.0.0"
    update_property(csproj_path, "AssemblyVersion", assembly_version, assembly_version_handling)

    if old_version:
        return _sv.Version.parse(old_version)

    return None
