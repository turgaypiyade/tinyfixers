# Codex Project Instructions

- Do not run Unity builds, `dotnet build`, or other project build commands for this repository unless the user explicitly asks for a build in that turn.
- Performance is a primary requirement. For gameplay/VFX changes, proactively review object churn, allocations, repeated board scans, and cleanup without waiting for a reminder. Reuse the existing pools for frequently spawned effects; prefer shared update loops and reusable buffers in hot paths. Verify reuse and cancellation/scene teardown behavior, and distinguish measured performance from expected improvements.
