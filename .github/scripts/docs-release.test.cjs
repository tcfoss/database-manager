const assert = require('node:assert/strict');
const { test } = require('node:test');
const { isLatestStableRelease } = require('./docs-release.cjs');

const release = (tag, attributes = {}) => ({
    tag_name: tag,
    draft: false,
    prerelease: false,
    ...attributes
});

const cases = [
    ['first stable release', 'v1.0.0', [release('v1.0.0')], true],
    ['latest stable release', 'v1.2.0', [release('v1.1.0'), release('v1.2.0')], true],
    ['same-version re-release', 'v1.2.0', [release('v1.2.0')], true],
    ['older major re-release', 'v1.9.9', [release('v1.9.9'), release('v2.0.0')], false],
    ['older minor re-release', 'v1.9.0', [release('v1.9.0'), release('v1.10.0')], false],
    ['older patch re-release', 'v1.0.9', [release('v1.0.9'), release('v1.0.10')], false],
    ['newer run finishes first', 'v1.1.0', [release('v1.2.0'), release('v1.1.0')], false],
    ['higher draft does not block stable', 'v1.0.0', [release('v2.0.0', { draft: true }), release('v1.0.0')], true],
    ['higher prerelease does not block stable', 'v1.0.0', [release('v2.0.0', { prerelease: true }), release('v1.0.0')], true],
    ['prerelease is not deployed', 'v2.0.0-beta.1', [release('v2.0.0-beta.1', { prerelease: true })], false],
    ['prerelease flag is respected', 'v2.0.0', [release('v2.0.0', { prerelease: true })], false],
    ['draft is not deployed', 'v2.0.0', [release('v2.0.0', { draft: true })], false],
    ['missing release is not deployed', 'v1.0.0', [], false],
    ['unknown tag format is not deployed', 'latest', [release('latest')], false]
];

for (const [name, tag, releases, expected] of cases) {
    test(name, () => {
        assert.equal(isLatestStableRelease(tag, releases), expected);
    });
}