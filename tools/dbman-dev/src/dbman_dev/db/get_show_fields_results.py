"""Get results of the SHOW FIELDS command."""

from dbman_dev.containers.db_container import MySqlContainer
from dbman_dev.db.show_fields_result import ShowFieldsResult
from dbman_dev.csharp_code_generation.entity_model import EntityModel


def get_show_fields_results(
    container: MySqlContainer, schema: str, table: str
) -> list[EntityModel]:
    """Get the results of the SHOW FIELDS command for a specific table."""

    fields = container.query(f"SHOW FIELDS FROM `{schema}`.`{table}`;")
    return [ShowFieldsResult.from_row(x).to_cs_model() for x in fields]
