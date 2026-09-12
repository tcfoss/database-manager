"""Options for rsync commands."""

from attrs import define as _define


@_define(frozen=True)
class RsyncOpts:
    """Options for rsync commands."""

    archive: bool = True
    delete: bool = True
    verbose: bool = False
    chmod: str | None = None
    exclude_patterns: list[str] | None = None

    def to_cmd_args(self) -> list[str]:
        """Convert the options to rsync command line arguments."""

        args = []

        if self.archive:
            args.append("-a")
        if self.delete:
            args.append("--delete")
        if self.verbose:
            args.append("-v")
        if self.chmod:
            args.extend(["--chmod", self.chmod])

        if self.exclude_patterns:
            for pattern in self.exclude_patterns:
                args.extend(["--exclude", pattern])

        return args
