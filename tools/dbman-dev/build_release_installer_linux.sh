#!/bin/bash

VERSION=$(poetry run python src/dbman_dev/installation/get_installer_version.py)

echo "__installer_version__ = '$VERSION'" > src/dbman_dev/installation/_build_meta.py

poetry run pyinstaller ./src/dbman_dev/installation/install_release.py \
    --onefile \
    --name "dbman-installer-linux-x64-v$VERSION"
