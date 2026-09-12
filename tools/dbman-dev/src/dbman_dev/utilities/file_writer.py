"""Write text to a file."""

import os
from dbman_dev.utilities.file_exists_mode import FileExistsMode


def write_text(file_path: str, text: str, mode: FileExistsMode) -> None:
    """Write text to a file."""
    if mode == FileExistsMode.OVERWRITE:
        with open(file_path, "w", encoding="utf-8") as f:
            f.write(text)

    elif mode == FileExistsMode.PROMPT:
        if os.path.exists(file_path):
            response = input(f"File {file_path} exists. Overwrite? (y/n) ")
            if response.lower() not in ("y", "yes", "1", "t", "true"):
                print("File exists. Skipping write.")
                return

        with open(file_path, "w", encoding="utf-8") as f:
            f.write(text)

    elif mode == FileExistsMode.SKIP:
        if os.path.exists(file_path):
            print("File exists. Skipping write.")
            return

        with open(file_path, "w", encoding="utf-8") as f:
            f.write(text)

    elif mode == FileExistsMode.ERROR:
        if os.path.exists(file_path):
            raise FileExistsError(f"File {file_path} exists.")

        with open(file_path, "w", encoding="utf-8") as f:
            f.write(text)
