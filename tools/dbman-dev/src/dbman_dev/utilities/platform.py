"""Os-related tools."""

import ctypes as _ctypes
import platform as _platform
import os as _os


OPERATING_SYSTEM = _platform.system().lower()
IS_UNIX_LIKE = OPERATING_SYSTEM in ("linux", "darwin")


def is_user_admin() -> bool:
    """Check if the current user has administrative/root privileges."""
    # pylint: disable=broad-exception-caught
    if OPERATING_SYSTEM == "windows":
        try:
            return _ctypes.windll.shell32.IsUserAnAdmin() != 0  # type: ignore
        except Exception:
            return False
    else:
        return _os.geteuid() == 0
