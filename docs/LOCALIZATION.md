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

Prefer external, key-based UI catalogs over inline locale branches. A possible future layout is:

```text
resources/localization/ui/
    en.json
    ru.json
```

This path and JSON are design direction, not an implemented or permanently selected storage format. CONTRACTS-R1 creates no catalogs, resource directories, or localization package.

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

Missing or malformed translations must not crash or prevent photograph viewing. Diagnostics must not replace the visible fallback.

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
