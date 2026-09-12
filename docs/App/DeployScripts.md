# Deploy Scripts

Each schema mapping can specify one or more scripts to run at various points in the
deployment (for now, that means their text is insert into the database update script).

## Basic Syntax

The basic syntax for specifying a deploy script under a schema mapping in the configuration
file is

```yaml
Type: { PreDeployment | PostDropConstraints | PreAddConstraints | PostDeployment } # Required*
FilePath: <path to script, relative to schema root> # Required
UniqueId: <guid for the script> # Optional
```

If `UniqueId` is specified, the script will only be included if a script with that `UniqueId`
has not been executed before. Otherwise, the script will be included every time.

The `FilePath` can be
1. A path to a `.sql` file.
2. A path to a `.yaml` file listing more scripts in the format given above.
3. A path to a directory.

If `FilePath` points to a directory, `UniqueId` and `Type` apply to every script in that
directory. If it points to a `yaml` file, `UniqueId` and `Type` are the _default_ values
for the scripts it references.

At the end of the file-path resolution process, **every script must have a `Type`**.
Therefore, `Type` is optional only if the file path points to a `.yaml` file **and**
all scripts referenced in the `.yaml` file have their own `Type` specified.

## File Path Resolution

The path resolver add scripts in the order in which they are referenced. The contents
of YAML references are read and registered before proceeding to the next item in the
containing list. The contents of directory references are read and registered  **in
standard string sort order** before proceeding to the next reference.

Script execution order is determined (1) by the script type and then, within a script
type, by the resolution order described above.

### A Somewhat Convoluted Example

Suppose in your configuration file you have

```yaml
Schemas:
  - SchemaName: my_schema
    RootPath: SchemaDefinition
    DeployScripts:
      - FilePath: SomeScripts/script1.sql
        Type: PreDeployment
      - FilePath: current-scripts.yaml
        Type: PostDeployment
      - FilePath: SomeScripts/script2.sql
        Type: PostDropConstraints
```

and the contents of `current-scripts.yaml` is

```yaml
FilePath: MoreScripts/script4.sql
Type: PreAddConstraints
FilePath: GeneralScripts
Type: PostDeployment
FilePath: MoreScripts/script5.sql
```

Further suppose that your directory layout looks like this:

```
SchemaDefinition/
|-- Tables/
|   |-- table1.sql
|   |-- table2.sql
|   `-- (...)
|-- SomeScripts/
|   |-- script1.sql
|   |-- script2.sql
|   `-- script3.sql
|-- MoreScripts/
|   |-- script4.sql
|   `-- script5.sql
|-- GeneralScripts/
|   |-- script6.sql
|   |-- script7.sql
|   `-- script08.sql
`-- current-scripts.yaml
```

The script path resolver then does down the deploy-script list in order, first
giving

1. SchemaDefinition/SomeScripts/script1.sql (PreDeployment)

It then comes to the YAML file reference, opens it, and begins parsing that in order,
adding to the list

2. SchemaDefinition/MoreScripts/script4.sql (PreAddConstraints)

Then it comes to a directory. It reads the contents of the directory in **OS-sort order**,
adding

3. SchemaDefinition/GeneralScripts/script08.sql (PostDeployment)
4. SchemaDefinition/GeneralScripts/script6.sql (PostDeployment) # because "6" > "0"
5. SchemaDefinition/GeneralScripts/script7.sql (PostDeployment)

They all inherit "PostDeployment" from the reference in `current-scripts.yaml`. Then
it continues with said YAML file, adding

6. SchemaDefinition/MoreScripts/script5.sql (PostDeployment)

The type there was inherited from the main configuration file. That's the end of the
references in `current-scripts.yaml`, so the resolver continues with the final item
in the main configuration, adding

7. SchemaDefinition/SomeScripts/script2.sql (PostDropConstraints)
