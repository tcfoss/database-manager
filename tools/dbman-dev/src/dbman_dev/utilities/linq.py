"""LINQ-like functions."""

from typing import Iterable, Callable, TypeVar

T = TypeVar("T")
U = TypeVar("U")


def select(iterable: Iterable[T], selector: Callable[[T], U]) -> Iterable[U]:
    """Projects each element of a sequence into a new form."""
    for item in iterable:
        yield selector(item)


def where(iterable: Iterable[T], predicate: Callable[[T], bool]) -> Iterable[T]:
    """Filters a sequence of values based on a predicate."""
    for item in iterable:
        if predicate(item):
            yield item


def first_or_default(
    iterable: Iterable[T], predicate: Callable[[T], bool], default: T | None = None
) -> T | None:
    """Returns the first element of a sequence, or a default value."""
    for item in iterable:
        if predicate(item):
            return item
    return default


def first(iterable: Iterable[T], predicate: Callable[[T], bool]) -> T:
    """Returns the first element of a sequence, or raises an error."""
    for item in iterable:
        if predicate(item):
            return item
    raise ValueError("No matching element found")
