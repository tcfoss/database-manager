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
   5. Build documentation strictly and test its release deployment policy.

6. Complete the Pull Request. The subsequent pipeline will
   1. Attach the release tag `vX.Y.Z` to the merge commit.
   2. Build the application, packages and documentation from that tag.
   3. Publish the application with the new version for all currently-supported platforms (currently
      `linux-x64`, `win-x64`, and `osx-arm64`).
   4. Upload the published artifacts.
   5. Pack and publish the Core, provider, and LibWrapper NuGet packages and their
      matching `.snupkg` symbol packages.
   6. Create a release for version `X.Y.Z` with the corresponding tag and link the published artifacts.
   7. Deploy documentation to GitHub Pages only after successful release finalization
      and only if this is the highest published stable version.


## NuGet Symbols

Each library produces a `.nupkg` and a matching `.snupkg` with portable PDBs.
The pack job checks that the symbol archive exists for every package. Normal
packages are pushed to GitHub Packages and NuGet.org with automatic symbol
upload disabled; a separate step pushes the `.snupkg` files to NuGet.org using
the same NuGet login. Package and symbol pushes allow duplicate uploads during
re-releases.


## Documentation Deployment

The site describes the latest stable release rather than the current `master`
branch. Pre-release versions do not update it. Deployments are serialized, and
the version check runs immediately before deployment. Therefore, an older re-release or
a slower older workflow cannot roll the site back.

Manual releases use the same tag-based source selection and documentation
deployment policy. Re-releasing the latest stable version can refresh its site;
re-releasing an older version cannot replace newer documentation.
