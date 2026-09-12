"""Tools for getting some common paths."""

from pathlib import Path


def _is_repo_root(path: Path) -> bool:
    """Check if the given path is the repository root by looking for the Core project."""

    return (path / "src" / "TcfOss.DatabaseManager.Core").is_dir()


def _find_repo_root() -> Path:
    """Find the repository root by looking for the Core project."""
    current = Path(__file__).resolve().parents[4]
    if _is_repo_root(current):
        return current
    current = Path.cwd().resolve()
    if _is_repo_root(current):
        return current
    for parent in current.parents:
        if _is_repo_root(parent):
            return parent
    raise RuntimeError("Could not find repository root.")


REPO_ROOT = _find_repo_root()
