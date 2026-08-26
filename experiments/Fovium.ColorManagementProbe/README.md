# Fovium monitor color-management probe

Role: Disposable R8-B-P1 executable evidence for ICC parsing, Skia transforms, native display-profile discovery, direct-Skia target inspection, and presentation-strategy cost.

This project is not referenced by production `Fovium` and does not change normal rendering. It intentionally uses only the Avalonia and SkiaSharp versions already present in the solution. The optional Little CMS helper under `reference/` is a developer-only comparison program and is not part of the .NET build or product package graph.

Run the headless profile and transform probe:

```powershell
dotnet run --project experiments/Fovium.ColorManagementProbe -c Release -- --profiles resources/test-images/color-management/profiles
```

Add `--benchmark` for the 24 MP full-frame and viewport-sized CPU-raster measurements. Inspect the actual Avalonia direct-Skia target and, on Windows, the current display profile associated with the real probe `HWND`:

```powershell
dotnet run --project experiments/Fovium.ColorManagementProbe -c Release -- --avalonia-target
```

The profile directory is an ignored local corpus. R8-B-P1 used these ICC Registry inputs:

| File | Official source | Bytes | SHA-256 | Purpose |
| --- | --- | ---: | --- | --- |
| `sRGB2014.icc` | <https://registry.color.org/rgb-registry/profiles/sRGB2014.icc> | 3,024 | `384B832DE3412066743B52A75EE906B6FB9FB8D9E09E936FC2C43223815C6E0A` | ICC v2 RGB display matrix/TRC source/destination |
| `Display-P3.icc` | extracted from <https://registry.color.org/rgb-registry/profiles/DisplayP3.zip> | 536 | `20789FDBEA9835251A4F0796C8BF45CBD964896044886540DA21FFC7457AF0AB` | ICC v4 RGB display matrix/TRC wide-gamut source/destination |
| `sRGB_v4_ICC_preference_displayclass.icc` | <https://registry.color.org/rgb-registry/profiles/sRGB_v4_ICC_preference_displayclass.icc> | 60,988 | `F54B145A18E4B12112750E672F1C79CAC9347DC8403DA3955E7F74A352816A21` | ICC v4 RGB display A2B/B2A LUT capability boundary |

The Display P3 ZIP downloaded for extraction was 1,219 bytes with SHA-256 `9E4BD457D5493382C34A852E9747F69A6A4FFF45462F422DFB59E6051DCF1613`. ICC Registry redistribution terms are stated on its [profile library](https://registry.color.org/profile-library/). These profiles and all derived malformed copies remain ignored and untracked.

For the reference comparison, download official tag `lcms2.19` from <https://github.com/mm2/Little-CMS/archive/refs/tags/lcms2.19.zip> (SHA-256 `CA96DAE87F478740C25699718A360A36B6910EC3436B86E17748C303900269DA`), build it outside the tracked tree or under ignored `artifacts/`, and point `reference/lcms_compare.c` at that build. The helper uses relative-colorimetric RGB-to-RGB transforms. Little CMS remains a research candidate, not a Fovium dependency.
