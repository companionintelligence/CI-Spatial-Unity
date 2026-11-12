# Test images

The PlayMode QA tests (`PerImageModelTests`, `QaSceneImageProcessingTests`,
`MappingSnapshotQaTests`) send every `.jpg`, `.jpeg` and `.png` file in this
folder to the CV server.

Every image here is a synthetic room render made by
[`tools/generate_synthetic_fixtures.py`](../tools/generate_synthetic_fixtures.py).
They are first-party work, covered by this repository's [LICENSE](../LICENSE),
and contain no third-party photographs, textures or fonts. To regenerate them:

```bash
python tools/generate_synthetic_fixtures.py
```

Do not commit photographs downloaded from the web. Earlier revisions of this
folder held 14 web photos of closets, pantries and laundry rooms with no
recorded source or licence, and they were removed for that reason. To test
with real photos, copy photos you took yourself into this folder locally and
leave them uncommitted, or commit only images whose licence allows
redistribution (for example CC0). Record the source, author and licence of
each committed image in [THIRD_PARTY.md](../THIRD_PARTY.md).
