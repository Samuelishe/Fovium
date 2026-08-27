# Fovium Little CMS test profiles

This directory owns the source for the tiny synthetic ICC profiles used by production-interop tests.
They contain no third-party color data. `generate_profiles.c` creates both profiles with the pinned
Little CMS 2.19 public API:

- `fovium-linear-rgb-display.icc` is an RGB display matrix/TRC profile with sRGB primaries and
  linear transfer curves.
- `fovium-lut-rgb-display.icc` is an RGB display profile whose relative-colorimetric `BToA0`
  output pipeline contains a project-authored 3-channel CLUT.

Configure the generator with an installed pinned lcms2 CMake package, then pass the desired output
directory to the executable. The generator prints the lcms runtime version, final profile sizes and
SHA-256-independent reference patch bytes; repository tests separately hash and admit the emitted
ICC bytes. Regeneration is build-time/test maintenance only. Fovium never generates profiles at
runtime and never offers a user profile override.
