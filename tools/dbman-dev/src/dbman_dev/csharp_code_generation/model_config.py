"""Class to define how to convert a ShowFieldsResult to a C# model."""

import attrs as _attrs


@_attrs.define
class ModelConfig:
    """How to convert a ShowFieldsResult to a C# model."""

    table_name: str
    key_columns: list[str]
    model_name_override: str | None = None
    plural_name_override: str | None = None
    not_null_overrides: list[str] | None = None
    ignored_columns: list[str] | None = None
