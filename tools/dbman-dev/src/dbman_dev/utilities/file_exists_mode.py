"""What to do if a file exists."""

import enum


class FileExistsMode(enum.Enum):
    """What to do if a file exists."""

    OVERWRITE = "overwrite"
    PROMPT = "prompt"
    SKIP = "skip"
    ERROR = "error"

    @staticmethod
    def from_string(value: str) -> "FileExistsMode":
        """Create a FileExistsMode from a string."""

        value = value.lower()

        if value == "overwrite":
            return FileExistsMode.OVERWRITE
        if value == "prompt":
            return FileExistsMode.PROMPT
        if value == "skip":
            return FileExistsMode.SKIP
        if value == "error":
            return FileExistsMode.ERROR

        raise ValueError(f"Unknown file exists mode: {value}")
