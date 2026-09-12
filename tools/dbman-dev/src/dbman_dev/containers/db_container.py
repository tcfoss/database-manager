"""Docker containers for database systems."""

from abc import ABC, abstractmethod
import shutil
import tempfile
import sys
import time

import docker
from docker.models.containers import Container
import pymysql

_DOCKER_IP_ADDRESS = "172.17.0.1"  # Default Docker bridge IP address
_DOCKER_TIMEOUT = 30  # Default timeout for waiting for the container to start
_DB_TIMEOUT = 60  # Default timeout for waiting for the RDBMS to be ready


class DbContainer(ABC):
    """Base class for database containers."""

    def __init__(
        self,
        name: str,
        image: str,
        port: int | dict[str, int] | None = None,
        environment=None,
    ):
        self.name = name
        self.image = image
        if isinstance(port, int):
            self.port = {"3306/tcp": port}
        elif isinstance(port, dict):
            self.port = port
        else:
            self.port = {"3306/tcp": 3306}
        self.environment = environment or {}
        self.container: Container | None = None

    def start(self):
        """Start the container."""
        if self.container:
            print(f"Container {self.name} is already running.")
            return

        print(
            f"Starting container {self.name} with image {self.image} "
            f"and environment {self.environment}..."
        )
        self.container = docker.from_env().containers.run(
            self.image,
            name=self.name,
            ports=self.port,
            environment=self.environment,
            detach=True,
        )

        for _ in range(_DOCKER_TIMEOUT):
            self.container.reload()
            if self.container.status == "running":
                print(f"Container {self.name} started successfully.")
                return
            print(f"Waiting for container {self.name} to start...")
            time.sleep(1)

    def status(self) -> str:
        """Get the container status."""
        if self.container:
            self.container.reload()
            return self.container.status
        return "not started"

    def exec_run(self, command: str | list[str], workdir=None):
        """Execute a command in the container."""
        if self.container:
            return self.container.exec_run(command, workdir=workdir)
        raise RuntimeError("Container is not running.")

    def upload_file(self, source_path: str, target_path: str):
        """Upload a file to the container."""
        if not self.container:
            raise RuntimeError("Container is not running.")

        with tempfile.NamedTemporaryFile() as tmp_file:
            shutil.make_archive(tmp_file.name, "tar", source_path, "")
            with open(tmp_file.name + ".tar", "rb") as f:
                self.container.put_archive(target_path, f)

    @abstractmethod
    def query(self, sql: str) -> tuple[tuple, ...]:
        """Run a SQL query against the database."""

    def stop(self):
        """Stop the container."""
        print(f"Stopping container {self.name}...")
        if self.container:
            self.container.stop()
            self.container.remove()
            self.container = None

    def __enter__(self):
        self.start()
        return self

    def __exit__(self, exc_type, exc_val, exc_tb):
        self.stop()


class MySqlContainer(DbContainer):
    """MySQL container class."""

    def __init__(
        self,
        name: str = "mysql_container",
        image: str = "mysql:8.4.6",
        port: int = 3309,
        environment=None,
    ):

        super().__init__(
            name=name,
            image=image,
            port=port,
            environment={
                "MYSQL_ALLOW_EMPTY_PASSWORD": "true",
                "MYSQL_DATABASE": "testdb",
                "MYSQL_USER": "admin",
                "MYSQL_PASSWORD": "password",
            }
            | (environment or {}),
        )

        print(self.environment)
        self.connection: pymysql.Connection | None = None
        self.cursor: pymysql.cursors.Cursor | None = None

    def start(self):
        """Start the MySQL container."""
        super().start()
        time.sleep(10)  # Allow some time for the container to initialize

        print(f"Waiting for connection to MySQL on port {self.port.get('3306/tcp', 3306)}...")

        for _ in range(_DB_TIMEOUT):
            try:
                self.connection = pymysql.connect(
                    host=_DOCKER_IP_ADDRESS,
                    port=self.port.get("3306/tcp", 3306),
                    user="root",
                    password="",
                    database="information_schema",
                )
                self.cursor = self.connection.cursor()
                print("MySQL container started successfully.")
                return

            except pymysql.err.OperationalError as e:
                if e.args[0] == 2013:
                    print("MySQL server is not ready yet, retrying...")
                    time.sleep(1)
                    continue
                print(f"Error connecting to MySQL: {e}")
                sys.exit(1)

            except pymysql.MySQLError as e:
                print(f"Error connecting to MySQL: {e}")
                sys.exit(1)

            time.sleep(1)

        raise RuntimeError("MySQL container did not start in time.")

    def stop(self):
        """Stop the MySQL container and close the connection."""
        print("Closing MySQL connection...")
        if self.cursor:
            self.cursor.close()
            self.cursor = None
        if self.connection:
            self.connection.close()
            self.connection = None
        super().stop()

    def query(self, sql: str):
        """Run a SQL query against the MySQL database."""
        if not self.connection or not self.cursor:
            raise RuntimeError("MySQL connection is not established.")

        self.cursor.execute(sql)
        return self.cursor.fetchall()
