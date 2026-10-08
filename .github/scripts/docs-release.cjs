function isLatestStableRelease(releaseTag, releases) {
    const parseVersion = tag => /^v(\d+)\.(\d+)\.(\d+)$/.exec(tag)?.slice(1).map(BigInt);
    const version = parseVersion(releaseTag);
    if (!version) {
        return false;
    }

    const stableReleases = releases.filter(release => !release.draft && !release.prerelease);
    return stableReleases.some(release => release.tag_name === releaseTag) &&
        !stableReleases.some(release => {
            const candidate = parseVersion(release.tag_name);
            if (!candidate) {
                return false;
            }
            for (let index = 0; index < version.length; index++) {
                if (candidate[index] !== version[index]) {
                    return candidate[index] > version[index];
                }
            }
            return false;
        });
}

module.exports = { isLatestStableRelease };