"""How to handle property updates."""

import enum


class PropertyUpdateMode(enum.Enum):
    """Enum for property update modes."""

    UPDATE_IF_PRESENT = enum.auto()
    """Update the <Property> if it is already present in the file."""
    ADD_IF_ABSENT = enum.auto()
    """Add the <Property> if it is not already present in the file."""
    SKIP = enum.auto()
    """Do not update or add the <Property>."""
