"""Database containers."""

from abc import ABC, abstractmethod
import shutil
import tempfile
from pathlib import Path
import time

import docker
from docker.models.containers import Container
from docker import errors as docker_errors
import pymysql
import psycopg
from psycopg.rows import dict_row

_DOCKER_TIMEOUT = 30
_DB_TIMEOUT = 60
_DOCKER_IP_ADDRESS = "172.17.0.1"  # Default Docker bridge IP address

type QueryParams = tuple[object, ...] | dict[str, object]


class DbContainerBase(ABC):
    """Base class for database containers"""

    def __init__(
        self, name: str, image: str, port: int, container_port: int, environment: dict[str, str]
    ):
        self.name = name
        self.image = image
        self.port = port
        self.port_config = {f"{container_port}/tcp": port}
        self.environment = environment

        self._started = False
        self._created = False
        self._connected = False
        self._container: Container | None = None
        self._client: docker.DockerClient | None = None

    @property
    @abstractmethod
    def quote_char(self) -> str:
        """Return the character used for quoting identifiers."""

    def quote_identifier(self, identifier: str) -> str:
        if not identifier or "\0" in identifier:
            raise ValueError(f"Invalid SQL identifier: {identifier!r}")
        quote = self.quote_char
        return quote + identifier.replace(quote, quote + quote) + quote

    def start(self):
        """Start the container."""

        if self._container:
            print(f"Container {self.name} is already running.")
            return

        if self._client is None:
            self._client = docker.from_env()

        try:
            self._container = self._client.containers.get(self.name)
        except docker_errors.NotFound:
            self._container = self._client.containers.create(
                self.image,
                name=self.name,
                ports=self.port_config,
                environment=self.environment,
            )
            self._created = True

        if self._container.status == "running":
            print(f"Container {self.name} is already running.")
            return

        print(f"Starting container {self.name} (image: {self.image})...")
        self._started = True
        self._container.start()

        for _ in range(_DOCKER_TIMEOUT):
            self._container.reload()
            if self._container.status == "running":
                return
            time.sleep(1)

        raise RuntimeError(
            f"Failed to start container {self.name} within {_DOCKER_TIMEOUT} seconds."
        )

    @abstractmethod
    def connect(self):
        """Connect to the database."""

    @abstractmethod
    def disconnect(self):
        """Disconnect from the database."""

    def stop(self):
        """Stop the container."""

        try:
            self.disconnect()
        finally:
            try:
                if self._container is not None:
                    if self._created:
                        try:
                            if self._started:
                                self._container.stop()
                                self._started = False
                        finally:
                            self._container.remove(force=True)
                            self._created = False
                            self._started = False
                    elif self._started:
                        self._container.stop()
                        self._started = False
                    self._container = None
            finally:
                if self._client is not None:
                    self._client.close()
                    self._client = None

    def upload_file(self, source_path: str, target_path: str):
        """Upload a file to the container."""
        if not self._container:
            raise RuntimeError("Container is not running.")
        with tempfile.TemporaryDirectory() as directory:
            archive = shutil.make_archive(str(Path(directory) / "schemas"), "tar", source_path, "")
            with open(archive, "rb") as stream:
                if not self._container.put_archive(target_path, stream):
                    raise RuntimeError(f"Failed to upload {source_path} to {target_path}")

    def exec_run(self, command: str | list[str], workdir=None):
        """Execute a command in the container."""
        if not self._container:
            raise RuntimeError("Container is not running.")
        return self._container.exec_run(command, workdir=workdir)

    @abstractmethod
    def query_dict(self, sql: str, params: QueryParams | None = None) -> list[dict[str, object]]:
        """Execute a query on the database."""

    @abstractmethod
    def query(self, sql: str, params: QueryParams | None = None) -> list[tuple[object, ...]]:
        """Execute a query on the database and return raw results."""


class MySqlContainer(DbContainerBase):

    def __init__(
        self,
        name: str,
        port: int = 3308,
        user: str = "root",
        password: str = "",
        db: str = "information_schema",
        image: str = "mariadb:latest",
        root_password: str | None = None,
    ):
        if root_password is None:
            effective_root_password = password if user == "root" else ""
        else:
            effective_root_password = root_password
        environment = (
            {"MYSQL_ROOT_PASSWORD": effective_root_password}
            if effective_root_password
            else {"MYSQL_ALLOW_EMPTY_PASSWORD": "true"}
        )
        if db.casefold() not in ("information_schema", "mysql", "performance_schema", "sys"):
            environment["MYSQL_DATABASE"] = db
        if user != "root":
            if not password:
                raise ValueError("A password is required when creating a non-root MySQL user")
            environment.update({"MYSQL_USER": user, "MYSQL_PASSWORD": password})
        super().__init__(
            name=name,
            image=image,
            port=port,
            container_port=3306,
            environment=environment,
        )
        self.user = user
        self.password = effective_root_password if user == "root" else password
        self.db = db

        self._con: pymysql.Connection | None = None

    @property
    def quote_char(self) -> str:
        return "`"

    def connect(self) -> None:
        if self._con:
            print(f"Already connected to database {self.db}.")
            return

        last_error: pymysql.MySQLError | None = None
        for _ in range(_DB_TIMEOUT):
            try:
                self._con = pymysql.connect(
                    host=_DOCKER_IP_ADDRESS,
                    port=self.port,
                    user=self.user,
                    password=self.password,
                    database=self.db,
                )
                self._connected = True
                return
            except pymysql.MySQLError as e:
                last_error = e
                time.sleep(1)

        raise RuntimeError(
            f"Failed to connect to database {self.db} within timeout. Last error: {last_error}"
        )

    def disconnect(self) -> None:
        if not self._con:
            return

        if self._connected:
            try:
                self._con.close()
            finally:
                self._con = None
                self._connected = False
        else:
            print(f"Connection to container {self.name} is externally managed.")

    def query_dict(self, sql: str, params: QueryParams | None = None) -> list[dict[str, object]]:
        if not self._con:
            raise RuntimeError("Not connected to the database.")
        with self._con.cursor(pymysql.cursors.DictCursor) as cursor:
            cursor.execute(sql, params)
            return list(cursor.fetchall())

    def query(self, sql: str, params: QueryParams | None = None) -> list[tuple[object, ...]]:
        if not self._con:
            raise RuntimeError("Not connected to the database.")
        with self._con.cursor() as cursor:
            cursor.execute(sql, params)
            return list(cursor.fetchall())


class PostgresContainer(DbContainerBase):

    @property
    def quote_char(self) -> str:
        return '"'

    def __init__(
        self,
        name: str,
        port: int = 5439,
        user: str = "postgres",
        password: str = "postgres",
        db: str = "postgres",
        image: str = "postgres:17",
    ):
        super().__init__(
            name=name,
            image=image,
            port=port,
            container_port=5432,
            environment={
                "POSTGRES_USER": user,
                "POSTGRES_PASSWORD": password,
                "POSTGRES_DB": db,
            },
        )
        self.user = user
        self.password = password
        self.db = db

        self._con: psycopg.Connection[tuple[object, ...]] | None = None

    def connect(self):
        if self._con:
            print(f"Already connected to database {self.db}.")
            return

        last_error: psycopg.OperationalError | None = None
        for _ in range(_DB_TIMEOUT):
            try:
                self._con = psycopg.connect(
                    host=_DOCKER_IP_ADDRESS,
                    port=self.port,
                    user=self.user,
                    password=self.password,
                    dbname=self.db,
                )
                self._con.autocommit = True
                self._connected = True
                return
            except psycopg.OperationalError as e:
                last_error = e
                time.sleep(1)

        raise RuntimeError(
            f"Failed to connect to database {self.db} within timeout. Last error: {last_error}"
        )

    def disconnect(self):
        if not self._con:
            return

        if self._connected:
            try:
                self._con.close()
            finally:
                self._con = None
                self._connected = False
        else:
            print(f"Connection to container {self.name} is externally managed.")

    def query_dict(self, sql: str, params: QueryParams | None = None) -> list[dict[str, object]]:
        if not self._con:
            raise RuntimeError("Not connected to the database.")

        with self._con.cursor(row_factory=dict_row) as cur:
            cur.execute(sql, params)  # type: ignore
            return cur.fetchall()

    def query(self, sql: str, params: QueryParams | None = None) -> list[tuple[object, ...]]:
        if not self._con:
            raise RuntimeError("Not connected to the database.")

        with self._con.cursor() as cur:
            cur.execute(sql, params)  # type: ignore
            return cur.fetchall()
