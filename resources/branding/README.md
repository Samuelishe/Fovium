# Fovium branding assets

Role: Ownership and regeneration note for Fovium's application identity.

`fovium-app-icon.svg` is the canonical editable mark. Its two offset corners describe an open photographic field: the
warm-white corner establishes a quiet viewing frame, while the violet corner adds a precise point of recognition. The
corners use one shared contour, mirrored by 180 degrees, so stroke weight, radii, terminals, and the two optical gaps
remain exactly balanced. Subtle tonal gradients add depth at larger sizes without weakening the silhouette.

The artwork and every derived file in this directory are authored specifically for Fovium. They use no downloaded
icon, logo, photograph, font file, or stock asset.

## Files

| File                      | Purpose                                                                          |
|---------------------------|----------------------------------------------------------------------------------|
| `fovium-app-icon.svg`     | Canonical vector source and future packaging source                              |
| `fovium-app-icon-64.png`  | Small raster for packaging or documentation                                      |
| `fovium-app-icon-128.png` | README/application identity raster                                               |
| `fovium-app-icon-256.png` | Large raster for packaging or documentation                                      |
| `fovium.ico`              | Windows application icon with 16, 20, 24, 32, 48, 64, 128, and 256 px PNG frames |

## Regeneration and wiring

From the repository root on Windows:

```powershell
pwsh eng/branding/generate-branding.ps1
pwsh eng/branding/verify-branding.ps1
```

The generator uses only PowerShell and the Windows BCL drawing implementation. At 16–24 px it switches to a heavier,
flat-color optical master with wider interior counters; larger sizes use the rounded canonical contour and restrained
gradients. Concept and small-size comparisons are written under ignored `artifacts/branding/`.

`Fovium/Fovium.csproj` sets the `.ico` as `ApplicationIcon`, which embeds it in the Windows apphost. Avalonia's
application-icon bridge is explicitly enabled so the same resource becomes the default icon for every current
top-level window: Viewer, Settings, Color Editor, and Shortcut Conflict. The existing 128 px project-authored PNG is
also embedded as an Avalonia resource for the Settings navigation identity and About surface; no new asset or derivative
is introduced by that reuse.
