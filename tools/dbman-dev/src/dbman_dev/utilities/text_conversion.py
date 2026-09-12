"""Tools for manipulating strings."""


def snake_to_pascal(snake_str: str) -> str:
    """Convert a snake_case string to PascalCase."""
    components = snake_str.split("_")
    return "".join(word.capitalize() for word in components)


def pluralize(singular: str) -> str:
    """Convert a singular noun to its plural form."""
    if singular.endswith("y"):
        return singular[:-1] + "ies"
    return singular + "s"
