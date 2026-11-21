"""Build scoped, ordered queries for selected system-table data."""

import logging

import dbman_dev.code_generation.table_config as tc
import dbman_dev.code_generation.containers as dbc

logger = logging.getLogger(__name__)


class DataQueryer:
    def __init__(self, schemas: list[str], default_schema_predicate_lhs: str):
        self.schemas = schemas
        self.default_schema_predicate_lhs = default_schema_predicate_lhs

    def get_data(
        self, con: dbc.DbContainerBase, table: tc.ModelConfig
    ) -> list[tuple[object, ...]]:
        query, params = self._build_data_query(con, table)
        logger.info("Executing query: %s", query)
        return con.query(query, params)

    def _build_data_query(
        self, con: dbc.DbContainerBase, table: tc.ModelConfig
    ) -> tuple[str, dict[str, object]]:

        selections = ", ".join(
            field.query_template or f"t.{con.quote_identifier(field.column_name)}"
            for field in table.fields
        )

        source = (
            f"{con.quote_identifier(table.schema_name)}."
            f"{con.quote_identifier(table.table_name)} AS t"
        )

        params: dict[str, object] = {}
        schema_filter = ""
        schemas = ""

        if table.filter_by_schema:
            if not self.schemas or any(not schema for schema in self.schemas):
                raise ValueError(
                    f"{table.table_name}: schema filtering requires nonempty schema names"
                )

            params = {f"schema_{i}": schema for i, schema in enumerate(self.schemas)}
            schemas = ", ".join(f"%({name})s" for name in params)

            predicate_lhs = self.default_schema_predicate_lhs
            if table.schemas_join and table.schemas_join.schema_predicate_lhs:
                predicate_lhs = table.schemas_join.schema_predicate_lhs

            if not predicate_lhs:
                raise ValueError(f"{table.table_name}: missing schema predicate")

            schema_filter = f"{predicate_lhs} IN ({schemas})"

        if table.order_by is not None:
            if any(not expression.strip() for expression in table.order_by):
                raise ValueError(f"{table.table_name}: empty ordering expression")
            ordering = table.order_by

        else:
            fields = {field.property_name: field for field in table.fields}
            ordering = [
                f"t.{con.quote_identifier(fields[prop].column_name)}"
                for prop in (table.key_properties or [])
            ]

        order_clause = "ORDER BY " + ", ".join(ordering) if ordering else ""

        if table.data_query_template is not None:
            template = table.data_query_template
            if "{columns}" not in template:
                raise ValueError(f"{table.table_name}: query template must contain {{columns}}")

            if table.filter_by_schema and not any(
                token in template for token in ("{schema_filter}", "{schemas}")
            ):
                raise ValueError(f"{table.table_name}: query template must apply schema filtering")

            if table.additional_predicate is not None:
                raise ValueError(
                    f"{table.table_name}: put additional predicates in the query template"
                )

            if table.schemas_join is not None:
                raise ValueError(f"{table.table_name}: put joins in the query template")

            if order_clause and "{order_by}" not in template:
                raise ValueError(f"{table.table_name}: query template must contain {{order_by}}")

            query = template
            for token, value in (
                ("{columns}", selections),
                ("{table}", source),
                ("{schema_filter}", schema_filter),
                ("{schemas}", schemas),
                ("{order_by}", order_clause),
            ):
                query = query.replace(token, value)

        else:
            query = f"SELECT {selections} FROM {source}"
            if table.schemas_join:
                query += f" {table.schemas_join.join_clause}"

            predicates = [schema_filter] if schema_filter else []
            if table.additional_predicate is not None:
                if not table.additional_predicate.strip():
                    raise ValueError(f"{table.table_name}: empty additional predicate")

                predicates.append(table.additional_predicate)

            if predicates:
                query += " WHERE " + " AND ".join(f"({predicate})" for predicate in predicates)

            if order_clause:
                query += " " + order_clause

        return query, params
