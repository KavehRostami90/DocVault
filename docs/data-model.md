# Data Model

- Document: Id (guid), Title, FileName, ContentType, Size, Hash, Text, Status, CreatedAt, UpdatedAt.
- Tag: Id (guid), Name; many-to-many via join.
- ImportJob: Id (guid), FileName, Status, StartedAt, CompletedAt, Error.
- Event records emitted for imported/indexed/search executed.
