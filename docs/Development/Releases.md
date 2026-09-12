# Releases

Here are the procedures for when it's time to bump versions and issue a new release.

## Prerequisites

1. All changes intended for the release must be merged into `master`.
2. The target release version must be established in IssueTracker.
3. The target release in IssueTracker must have at least one associated, release-relevant work item.
4. All work items tied to the target release in IssueTracker must be resolved.

## Procedure

1. Pull `master`, and cut a new branch `release/vX.Y.Z`, where
   `X.Y.Z` is the version that will be associated with the
   release.

   **NOTE**: There **must not** be either (1) a preexisting tag
   in the project with the name `vX.Y.Z` or (2) a preexisting
   release in the project with the tag `vX.Y.Z`

2. Add an empty commit
   `git commit --allow-empty -m "Release X.Y.Z"`

   If there are no other changes to commit, this empty commit ensures that the Pull Request has at
   least one commit.

3. Push the branch.

4. Create a Pull Request from the release branch into `master`.

5. Wait for the pipeline to finish. It will
   1. Verify via `dotnet format` that the code is properly formatted.
   2. Run the test suite.
   3. Validate that there is at least one message for the changelog (see Prerequisites).
   4. Validate that the release version is not already resolved.

6. Complete the Pull Request. The subsequent pipeline will
   1. Build the application.
   2. Attach the release tag `vX.Y.Z` to the merge commit.
   3. Publish the application with the new version for all currently-supported platforms (currently
      `linux-x64`, `win-x64`, and `osx-arm64`).
   4. Upload the published artifacts.
   5. Create a release for version `X.Y.Z` with the corresponding tag, and link the published
      artifacts.
