# Using Poetry and pipx

As wonderful as Python is for scripting, and even some more involved programming,
getting import paths right can be a bit of a pain. The
[Poetry](https://python-poetry.org/docs/#installation)
tool, designed for managing Python dependencies and virtual environments, makes this
a lot simpler.

While all the scripts here can theoretically be used just by calling python
from your terminal, I strongly recommend installing Poetry using the instructions at
the link above.

## Installing the DbMan-Dev Project

Once Poetry is installed, you can navigate to the `tools/dbman-dev` directory and call

```sh
poetry install
```

This will create a virtual environment for the DbMan-Dev project if one does not already
exist. Then it will install all necessary dependencies at the appropriate versions into
that virtual environment. Then it will **register scripts** defined in
`tools/dbman-dev/pyproject.toml`.

**NOTE**: If the command hangs indefinitely, and you're on Linux or macOS, the most likely
culprit is that it's trying and failing to access the system keyring. You can disable this
behavior by running

```sh
poetry config keyring.enabled false
```

## Updating the Tools

If the version number has changed in the `pyproject.toml` file, running `poetry install`
again will update the tools. If you're modifying the code and are not yet ready to
increase the version number, `poetry sync` will do what you want.

## Invoking the Scripts

As mentioned above, the `pyproject.toml` file defines a number of scripts under the
`[project.scripts]` heading that become available when you install the Poetry project.

There are three approaches to running the scripts.

### Using `poetry run`

Scripts can be invoked, from anywhere below `tools/dbman-dev` directory, by calling

```sh
poetry run SCRIPT_NAME ARGUMENTS ...
```

### From the Virtual Environment

Alternatively, you can enter the virtual environment that poetry created. To do this,
make sure you are somewhere under the `tools/dbman-dev` directory, and call

```sh
poetry env activate
```

This will output a command you can execute to enter the `dbman-dev` virtual envionment. Execute
the command, and then the scripts can be invoked with just the name, without the `poetry`
prefix:

```sh
SCRIPT_NAME ARGUMENTS ...
```

Note that if you are in the virtual environment, you no longer need to be under the
`tools/dbman-dev` directory. You can now call `SCRIPT_NAME ARGUMENTS ...` from any location,
theoretically, but most of the scripts in this project need to be executed from somewhere
under the repo root.


## pipx

The third approach is to install the Python project with [pipx](https://pipx.pypa.io/stable/installation/). This makes the scripts available in your OS account path, so you
no longer bother invoking `poetry` directly, either as a prefix to the script name
or to enter a virtual environment.

The scripts can be invoked using just

```sh
SCRIPT_NAME ARGUMENTS ...
```

from anywhere (though, again, most of them require you to be under the repo root).

### Installing DbMan-Dev with Pipx

Once you have `pipx` installed (there are instructions at the link above), navigate to `tools/dbman-dev`, and run

```sh
pipx install .
```

That's it. The scripts are available from anywhere.

### Upgrading DbMan-Dev with Pipx

To upgrade the tools in your user path to the version in your repo folder, run

```sh
pipx upgrade dbman-dev
```
