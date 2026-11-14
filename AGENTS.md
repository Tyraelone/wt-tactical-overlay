# Development and release

- Work in this Git checkout connected to `Tyraelone/wt-tactical-overlay`. Keep code, runtime map data, documentation and tests together. Do not maintain a separate untracked development copy or edit an old publishing copy.
- Preserve local `user-data` and calibration settings. Personal settings, screenshots, diagnostics, research caches and raw third-party imagery are excluded from public source and release archives.
- Build with `build.ps1`, run the full suite with `test.ps1`, and create public Windows packages with `package.ps1`. The ignored local `pack.ps1` is a legacy personal-package helper, not the public release entry point.
- Build release assets from the exact committed Git tree. Verify source/data consistency, package contents and checksums before publishing the source, tag and release.
- Do not operate the War Thunder game UI without explicit user authorization. Offline tests and read-only localhost map requests do not prove live gameplay alignment.
- Describe tactical coverage as sourced layouts and mode variants. Preserve source attribution and remaining gaps; do not invent tactical positions to fill missing data.
