# RC15 source baseline

The starting product version is **1.1.0-rc15**. The original workspace and its
delivered internal acceptance packages remain untouched.

`rc15-source-files.json` records SHA-256 hashes of the files selected from the
existing Studio workspace before repository integration changes.
`rc15-delivered-artifacts.json` records the existing package hashes, not newly
built or newly approved artifacts. The binaries themselves are not committed.

The delivered Runtime BUILD-IDENTITY reports original commit
`56fd8731a64660226c75fe6cfb415f708e2c2122`, uncommitted changes and
`testsExecuted: false`. The local changed source is now captured in the separate
Runtime repository. These records establish provenance but do not prove a
bit-identical rebuild of the previously delivered executable.

The `rc15-source-baseline` tag is a source preservation point, not a new hardware
approval. Customer profiles, bound templates, keys, local card evidence and
generated outputs are deliberately absent. Legacy tests requiring those assets
must be run separately with explicitly supplied local fixtures.
