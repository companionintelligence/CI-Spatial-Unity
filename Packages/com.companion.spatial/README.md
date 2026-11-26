# Companion Spatial

The engine-free core of the scanner's semantic map v2 (work package S4): one tag vocabulary, tracks that fuse
detections across captures, and the glTF extras schema v2 with a v1 reader. Nothing in `Runtime/` references
UnityEngine (the assembly sets `noEngineReferences`): it computes in `double` with its own small `Vec3`, has no
static mutable state, reads no clock and uses no random numbers, so the same input always gives the same output. Its
one dependency is Newtonsoft.Json (`com.unity.nuget.newtonsoft-json`), for the extras.

Assembly `Companion.Spatial`, namespace `Companion.Spatial`. Tests: assembly `Companion.Spatial.Tests`, NUnit, EditMode.

## What is here

| Folder | Namespace | Holds |
|---|---|---|
| `Runtime/Geometry` | `Companion.Spatial` | `Vec2`, `Vec3`, `Quat`, `NormalizedBox`: the small value types the modules share |
| `Runtime/Vocabulary` | `.Vocabulary` | `TagVocabulary`, `TagEntry`, `MetaLabels` (the tag table is `DefaultTags.cs`) |
| `Runtime/Tracking` | `.Tracking` | `TrackTable`, `TrackTableOptions`, `Track`, `Detection3D`, `TrackEvent` and friends |
| `Runtime/Extras` | `.Extras` | `ScanExtras` and its parts, `ExtrasReader`, `ExtrasWriter`, `ExtrasMerge` |
| `Documentation~` | | `CONTRACT.md` (v1, v2 and the web contract, field by field) and `scan-extras.v2.schema.json` |
| `Tests/Editor` | | the tests; `Fixtures~` holds the scanner's golden v1 file, copied unchanged |

## Running the tests

In the editor: Window > General > Test Runner > EditMode > `Companion.Spatial.Tests`. Headless, with the Unity CLI:

```
unity test <project> --mode EditMode --filter "Companion.Spatial.Tests"
```

On the build machine run it through the one-engine-job wrapper (`run-unity-tests.ps1 -Project <project> -Mode EditMode
-Filter "Companion.Spatial.Tests"`), so two editors never run at once. The filter `Companion` runs this package
and `Companion.Core.Tests` together. The tests read two files of the package (the schema in `Documentation~` and the
golden v1 file in `Tests/Editor/Fixtures~`); both folders end in `~`, so Unity does not import them.

## Tag vocabulary

`TagVocabulary.Default.Normalize(raw)` turns raw detector text (GroundingDINO, Florence, a Meta Space Setup label)
into a canonical lower-case singular tag; `ToSemanticLabel(rawOrCanonical)` gives one of Meta's 16 labels
(`MetaLabels.All`, in the scanner's order), `OTHER` when unknown. It folds case, white space, `_` and `-` and
punctuation, drops a leading "a", "an" or "the", makes simple plurals singular, and folds synonyms ("sofa", "Sofa Bed",
"a loveseat" are all `couch`). Text with no letter in it ("123", ".", "a") is garbage: an empty tag and `OTHER`. All 16 Meta
labels round-trip, including `INVISIBLE_WALL_FACE` and `GLOBAL_MESH`, which no detector class maps to. The whole
table is one list of rows in `Runtime/Vocabulary/DefaultTags.cs`, documented there; add a synonym to a row or add a
row, and the table tests check the result. `new TagVocabulary(rows)` builds one of your own.

## Track table

`TrackTable.Update(timestamp, detections)` takes the detections of one capture, each already carrying a world
position (`Detection3D`: raw label, score, position, normal, frame id, optional normalized box), and returns
`TrackEvent`s: `Found`, `Updated`, `Lost`, `Revived` (and `Retired`). A detection joins the nearest track of the same
canonical tag within 0.4 m (greedy across the whole capture, deterministic, never two detections to one track); a
track's position is an exponential moving average (alpha 0.5), its confidence the noisy-OR of its scores capped at
0.99, its tag and label text the highest summed score, and it keeps its last 16 observations. A track not seen for
10 s of scan time is reported `Lost` once, by the next `Update` or by `Tick(timestamp)`, and stays in the table, so a
capture ten seconds later revives it with the same id instead of pinning it again. Ids start at 1 and are never reused;
`RetireAfterSeconds` (never, by default) removes a lost track for good.

## Extras v2

`ScanExtras` is the model of the extras block: space (id, frame, created and updated time), consent, device, frames
(id, time, camera pose, intrinsics), objects (id, track id, label text and tag and Meta label, confidence, source,
pose, bounds, observations), planes from the Meta scene, and bounds. `ExtrasReader.Read(json)` (string or `JObject`)
loads v1 files, including the golden file and the shape the exporter really writes, and v2 files into that model;
unknown keys are kept in each part's `Unknown` bag, and a corrupt or non-object document gives a result with an error,
never an exception. `ExtrasWriter.Write(model)` gives the v2 `JObject` with a stable key order and every list as an
array, never null. `ScanObject.FromTrack(track, sessionId)` makes the object a track stands for. `ExtrasMerge` merges
a new export into the earlier one by object id, so a re-export lists each object once, recomputes the time and the
bounds, and replaces a corrupt earlier document with a warning. The schema is
`Documentation~/scan-extras.v2.schema.json`; `Documentation~/CONTRACT.md` lists where v2 differs from v1 and from the
web Scan contract.

## Decisions

Where the plan was ambiguous or the source contradicted it, the simplest behaviour that satisfies the tests was chosen.

Vocabulary
- A TV and a monitor are one tag, `screen`, the word that Meta, the web Scan app and the CV server's default list share;
  the raw text ("TV screen") stays in the object's label text. Likewise sofa, couch, sofa bed and loveseat are `couch`.
- `chair`, `cup`, `laptop` and the other 12 default-prompt classes without a Meta label are `OTHER`, and each is a known
  tag (`IsKnownTag`), so tracking can tell a cup from a chair. `picture frame` is `wall art` (`WALL_ART`); it gave OTHER.
- An unknown compound is its own tag and `OTHER`; the vocabulary does not guess from the last word ("wall art frame").
  Synonyms are listed explicitly instead ("coffee table", "floor lamp", "bookshelf").
- Plurals: `ies` to `y`, `sses`/`ches`/`shes`/`xes`/`zes` drop `es`, otherwise drop `s`, except words that end like a
  singular (`glass`, `cactus`) or are plural by name (`glasses`, `scissors`), and a short list of irregular plurals
  (`shelves`, `knives`, `mice`). Add to those lists when a word is wrong.

Tracking
- A detection is compared with the last observed raw position of a track, as the scanner's tracker always did, not with
  the smoothed one (so an object moved slowly keeps its track).
- Pairs are taken nearest first over the whole capture, ties by detection index then track id, which does not depend on
  the order the detections are listed in. The scanner's per-detection greedy could swap two nearby cups.
- The lost time defaults to 10 s (the pipeline's 2.5 s expiry made a later manual capture pin the object again). A
  track is lost when 10 s have passed, not more than 10. A revival is always preceded by its `Lost` event, even in the
  same call.
- `Retired` is a fifth event kind, so the scanner can remove the pin of a track that left the table.
- Bad input never throws: a detection with a NaN score, a non-finite position or a label with no letter is ignored and
  counted in `RejectedDetections`; a score outside 0..1 (infinity too) is clamped; a NaN or earlier timestamp is taken
  as the latest one. Options are checked once and throw `ArgumentOutOfRangeException`.
- Because detections only join a track of their own tag, a track's tag is always unanimous; the tag vote exists so the
  rule holds if matching is ever widened. The label text is the one that varies (`Sofa`, `couch`): the spelling with the
  highest summed score wins, ties to the earlier.

Extras
- v2 names itself (`schema`, `schema_version: 2` as a number). Positions are `{x,y,z}` objects and rotations are
  `quaternion`, as in the web contract; the reader also takes arrays and `rotation`.
- The coordinate frame is a file-level field, `space.frame` (`unity-world` by default). The v1 docs call the same
  numbers both left- and right-handed.
- Plane extents stay on the plane's own x and y axes, where the web contract has x and z (see CONTRACT.md).
- A v1 pin's `semantic_label` is kept when it names a Meta label other than OTHER, otherwise the vocabulary decides:
  the old OTHER for "picture frame" becomes WALL_ART. A v1 object id is `<session>-track-<n>`, `anchor-<uuid>` or
  `pin-<index>`; the app's track numbers restart in every run, so ids must carry the session
  (`ScanObject.MakeId`).
- What v1 had and v2 has no place for is kept under `"v1"` (see CONTRACT.md); a pin that appears twice in a v1 file
  (the old merge listed each twice) becomes one object, with a warning.
- The reader is lenient and keeps what is written: an empty label text stays empty (only a missing one is filled in
  from the tag), a number where text belongs (`"app_version": 1.4`) is read as its text, a negative track id is 0 and a
  focal length that is not above 0 drops the intrinsics, each with a warning, and a consent without its notice version
  or time warns (the uploader will refuse such a file).
- The writer repairs what JSON and the schema cannot hold: NaN and infinity become 0, a confidence or score is held
  to 0..1, a negative track id is 0, a Meta label that is not one of the 16 becomes OTHER, and an unknown key is never
  written under the name of a part of the document. What it cannot repair is written as it is and the schema flags it
  (a time that is not a time, a consent without its time).
- The merge replaces by id in place, keeps the old objects, takes the new planes whenever there are any, never carries
  consent over (it belongs to the export that recorded it), recomputes the bounds from the content plus the new export's
  own bounds, and refuses to merge two documents of different spaces or coordinate frames (the earlier one is replaced,
  with a warning). `created_at_utc` is the earliest time known, `updated_at_utc` the latest, compared as instants.
- "v1 to v2 to v1-compatible read" is taken to mean: a v1 file read into the model, written as v2 and read back by the
  same reader (which accepts both versions) gives the same document and keeps everything the v1 file said. There is no
  v1 writer: v2 files cannot be read by v1 consumers, and the web Scan app's own glTF extras (`extras.companion_scan`)
  are not read.

Package
- `com.unity.nuget.newtonsoft-json` 3.2.1 is declared; the scratch project resolved it to the 3.2.2 already in its
  cache, so `Packages/manifest.json` was not touched. Unity added the package to `Packages/packages-lock.json`.
- `InternalsVisibleTo("Companion.Spatial.Tests")` lets the tests check the score vote directly.
