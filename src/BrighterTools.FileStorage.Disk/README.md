# BrighterTools.FileStorage.Disk

MIT-0. Opt-in disk implementation of the shared writable, streaming and bounded
storage contracts. Construct `DiskFileStorage` with an absolute root outside the
published website. Hosts authorize media requests, validate uploads and generate
storage keys. Database metadata stores keys, never absolute server paths.
Existing files are immutable; new uploads require new keys. Path traversal and
linked paths are rejected. Back up storage and metadata together. This provider
does not change Azure registration or move existing media.
