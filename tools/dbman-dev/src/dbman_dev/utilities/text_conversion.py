"""Tools for manipulating strings."""


def snake_to_pascal(snake_str: str) -> str:
    """Convert a snake_case string to PascalCase."""
    components = snake_str.split("_")
    return "".join(word.capitalize() for word in components)
