"""Tools for interacting with Git repositories."""

import typing as _typing
import semver as _semver
import dbman_dev.utilities.shell_exec as _shell_exec


def default_version_tag_selector(tag: str) -> _semver.Version | None:
    """Default function to select version string from a Git tag.

    Parameters
    ----------
    tag : str
        The Git tag string.

    Returns
    -------
    str
        The version string extracted from the tag.
    """
    if tag.startswith("v") or tag.startswith("V"):
        return _semver.Version.parse(tag[1:])
    return None


def get_latest_tag_version(
    branch: str = "master",
    version_selector: _typing.Callable[[str], _semver.Version | None] | None = None,
) -> _semver.Version:
    """Get the latest Git tag merged into the specified branch.

    Parameters
    ----------
    branch : str, optional
        The branch to check for merged tags (Default is 'master').
    version_selector : Callable[[str], semver.Version | None] | None, optional
        A function to select the version from a tag string. It should return `None`
        if the tag string should not be interpreted as a version. (Default is None, in
        which case if a leading "v" is present, it will be stripped, and otherwise the tag
        will be interpreted as not a version).

    Returns
    -------
    str
        The latest merged Git tag.
    """

    if version_selector is None:
        version_selector = default_version_tag_selector

    cmd = ["git", "tag", "--merged", branch]
    tags_output = _shell_exec.run_output(cmd)
    tag_list = tags_output.splitlines()

    if not tag_list:
        raise ValueError(f"No tags found merged into branch '{branch}'.")

    versions = []
    for tag in tag_list:
        version = version_selector(tag)
        if version is not None:
            versions.append(version)

    return max(versions)
