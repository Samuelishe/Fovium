# Localization

Role: Contract for Fovium UI locale selection, catalogs, fallback, and translation boundaries.
Read when: Adding or changing user-visible text, language selection, localization resources, or locale resolution.
Authoritative for: Initially supported locales, fallback behavior, catalog direction, preserved user choice, and what should or should not be translated.
Not authoritative for: Settings storage implementation, theme behavior, metadata extraction, or current translated-resource availability.

## Supported locales

The initial UI locale set is:

```text
en
ru
```

English is the fallback locale. The architecture must permit additional catalogs without redesigning views or adding scattered language conditionals.

## Catalog direction

R1 implements external, key-based JSON UI catalogs at:

```text
resources/localization/ui/
    en.json
    ru.json
```

The catalogs are embedded into the production assembly; no localization package is used. Context-menu commands and overlay submenu, Photo Info and Histogram command/title/close chrome, the file-picker title, controlled image-load errors, Settings sections and typed command groups, contextual-scope hints, Stage/presenter customization, Hand and all markup tools/actions, effective-shortcut icon tooltips, EN/RU hold wording, capture/conflict dialogs, and shortcut states resolve through catalog keys rather than scattered locale branches. English uses “Opacity” and Russian consistently uses “Непрозрачность”, where 100% means fully opaque. Persisted command, scope/group, Matte-style, markup-tool, placement, and operation identities are never localized. Photo Info dates use the active locale while camera/lens/filename strings remain source data and unspecified EXIF clock time is never timezone-converted.

R8-A localizes Color Picker command/panel chrome, empty hint, history label, Transparent semantic, Approximate tooltip, and RGB(A) format templates in EN/RU. The 1,800 canonical proper color names remain the reviewed upstream English names unless an optional reliable curated localized name is later added; Fovium does not machine-translate the catalog. Stable IDs, HEX digits, and decimal channel values remain locale-independent.

R9-A localizes Photo Presentation command/context-menu copy, its Viewing subsection, Edge margin label, and the explanation that the photograph stays inside the presentation margin while Matte does not change its scale. The stable `viewer.togglePhotoPresentation` identity, serialized property names, percentage value, and active session state are locale-independent; English fallback remains mandatory.

R9-B localizes Slideshow, its Start/Stop live state, Slide duration/seconds, end-of-sequence prompt, Stop on last image, and Start again from first image in both English and natural Russian. The stable `viewer.toggleSlideshow` identity, numeric seconds, serialized enum/property names, timer state, and diagnostic names remain locale-independent. Catalog validation requires exact EN/RU key parity and English fallback.

## Locale resolution

Before an explicit user choice:

```text
supported OS locale
→ matching Fovium locale

unsupported OS locale
→ en
```

At minimum, a Russian environment resolves to `ru`; an unsupported environment resolves to `en`. Once the user selects a language in Settings → General, preserve that choice until reset or another explicit selection.

## Fallback and failure

Lookup follows:

```text
requested locale
↓ missing key
English
↓ missing English key
visible key + diagnostic warning
```

Missing translations do not crash or prevent photograph viewing. R1 falls back from the resolved locale to English and then returns the visible key while emitting a diagnostic trace. Catalog parse/load failure is currently an application-startup boundary failure rather than a recoverable missing-key case; future user-editable catalogs would require a different policy.

## Translation boundary

Translate user-interface concepts, for example Settings, Background, Fit, Actual size, Photo information, and Performance.

Do not translate identity or technical/source values where doing so reduces clarity, including:

```text
Fovium
JPEG
Adobe RGB
Display P3
Canon EOS R5
DSC_1234.JPG
ISO 400
85 mm
```

Advanced metadata may retain canonical tag names. User-entered paths, filenames, profile names, camera models, and embedded source text remain source data. The product name **Fovium** never changes by locale.
