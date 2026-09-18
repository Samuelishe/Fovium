# Localization

Role: Contract for Fovium UI locale selection, catalogs, fallback, and translation boundaries.
Read when: Adding or changing user-visible text, language selection, localization resources, or locale resolution.
Authoritative for: Initially supported locales, fallback behavior, catalog direction, preserved user choice, and what
should or should not be translated.
Not authoritative for: Settings storage implementation, theme behavior, metadata extraction, or current
translated-resource availability.

## Supported locales

The initial UI locale set is:

```text
en
ru
```

English is the fallback locale. The architecture must permit additional catalogs without redesigning views or adding
scattered language conditionals.

## Catalog direction

R1 implements external, key-based JSON UI catalogs at:

```text
resources/localization/ui/
    en.json
    ru.json
```

The catalogs are embedded into the production assembly; no localization package is used. Context-menu commands and
overlay submenu, Photo Info command/chrome/row labels/explanatory tooltips/textual photographic values, Histogram
command/title/close chrome, the file-picker title, controlled image-load errors, Settings sections and typed command
groups, contextual-scope hints, Stage/presenter customization, Hand and all markup tools/actions, effective-shortcut
icon tooltips, EN/RU hold wording, capture/conflict dialogs, and shortcut states resolve through catalog keys rather
than scattered locale branches. English uses “Opacity” and Russian consistently uses “Непрозрачность”, where 100% means
fully opaque. Persisted command, scope/group, Matte-style, markup-tool, placement, operation, and metadata enum
identities are never localized. Photo Info dates use the active locale while camera/lens/filename strings remain source
data and unspecified EXIF clock time is never timezone-converted.

R8-A localizes Color Picker command/panel chrome, empty hint, history label, Transparent semantic, Approximate tooltip,
and RGB (A) format templates in EN/RU. R8-A-F1 keeps the 1,800 reviewed English names as canonical matching/provenance
data and adds optional embedded display-name catalogs under `resources/color-names/localization/`, keyed by stable color
ID rather than UI-string keys. The active locale resolves current and historical sample names at the presentation
boundary; missing entries, malformed or absent catalogs, and unsupported locales fall back to canonical English without
changing matching or stored sample identity. The Russian catalog is Fovium-authored presentation data. Stable IDs,
RGB/HEX anchors, OKLab matching, tie order, HEX digits, and decimal channel values remain locale-independent.

R8-A-F2 localizes the Color Inspector's bounded semantic color taxonomy through ordinary EN/RU UI keys: hue families,
lightness/chroma classes, phrase templates, detail labels, and Clear. R8-A-F3 extends that bounded domain with
perceptual roles, neutral undertones, compound transition families, and near-black/near-white presentation names; it
does not add per-color catalog entries. R8-A-F4 adds bounded warm-neutral/earth-tone and violet/lilac-gray terms and
selects the localized `Color tone` or `Undertone` detail label from the locale-independent perceptual role. Core
classification stores locale-independent enum identities and OKLCH values; short and detailed phrases are composed
only at the presentation boundary. This bounded catalog is separate from the
1,800-entry creative-name data domain, which remains available as the secondary Creative/Образное name. HEX, RGB (A),
and labeled OKLCH numeric output remain locale-independent.

R8-A-F6 adds one bounded EN/RU `Mint` semantic term; it does not add per-color professional-name data. Creative-name
translation corrections remain keyed by the unchanged stable ID and never alter the canonical English anchor or
matching.

R8-A-F7 localizes the declarative professional-shade layer with one ordinary EN/RU UI key per supported term. Stable
term IDs, parent families, roles, OKLCH bounds, priorities, and classification remain locale-independent; the active
locale resolves only the final display term. Broad-family localization remains the detail/fallback path, and the
separate 1,800-entry creative catalog remains secondary rather than becoming professional-taxonomy data.

R8-A-F8 expands that ordinary UI-key domain from 18 to 33 professional terms. Multiple bounded perceptual regions may
share one stable term and localization key, so geometry can evolve without duplicating visible names. Every accepted
term ships with English and reviewed Russian together; audit-only aliases and external candidate vocabulary never enter
runtime localization or the creative catalog.

R8-A-F9 expands the domain to 41 professional terms with paired English/Russian names for Gold/Золотистый, Khaki/Хаки,
Copper/Медный, Mahogany/Махагоновый, Caramel/Карамельный, Lemon/Лимонный, Jade/Нефритовый, and Royal
Blue/Королевский синий. Component and alias data used to evaluate those terms remains developer-only; locale-independent
term identity and region geometry still resolve through one ordinary UI key at presentation time.

R8-A-F10 expands the same paired domain to 72 terms. New English/Russian keys cover six semantic groups, including
Lilac/Лиловый, Mauve/Розовато-лиловый, Cornflower Blue/Васильковый, Prussian Blue/Берлинская лазурь, Moss
Green/Моховой зелёный, Saffron/Шафрановый, Burnt Sienna/Жжёная сиена, Vermilion/Киноварь, Eggshell/Яичная
скорлупа, and Gunmetal Gray/Оружейно-серый. Every runtime identity still resolves through exactly one ordinary UI key;
audit aliases, dictionary variants, and external source names remain developer-only.

R8-A-F11 expands the paired domain to 80 terms with Ruby/Рубиновый, Cranberry/Клюквенный,
Viridian/Виридиановый, Celadon/Селадоновый, Antique White/Античный белый, Vanilla/Ванильный,
Naples Yellow/Неаполитанский жёлтый, and Espresso/Эспрессо. Developer-only aliases and research-domain categories do
not add UI keys or change the one-key-per-runtime-term contract.

R8-A-F12 expands the paired domain to 94 terms. New keys cover Heliotrope/Гелиотроповый, Slate Blue/Сланцево-синий,
Spring Green/Весенний зелёный, Pine Green/Сосновый зелёный, Brick Red/Кирпично-красный,
Raw Sienna/Натуральная сиена, Raw Umber/Натуральная умбра, Canary Yellow/Канареечный жёлтый, Gamboge/Гуммигут,
Ecru/Экрю, Buff/Палевый, Goldenrod/Золотисто-жёлтый, Russet/Рыжевато-коричневый, and Heather/Вересковый. Audit-only
aliases and Russian research candidates do not become localization keys.

R9-A localizes Photo Presentation command/context-menu copy, its Viewing subsection, Edge margin label, and the
explanation that the photograph stays inside the presentation margin while Matte does not change its scale. The stable
`viewer.togglePhotoPresentation` identity, serialized property names, percentage value, and active session state are
locale-independent; English fallback remains mandatory.

R9-B localizes Slideshow, its Start/Stop live state, Slide duration/seconds, end-of-sequence prompt, Stop on last image,
and Start again from first image in both English and natural Russian. The stable `viewer.toggleSlideshow` identity,
numeric seconds, serialized enum/property names, timer state, and diagnostic names remain locale-independent. Catalog
validation requires exact EN/RU key parity and English fallback.

R10-A localizes Average, Dominant, Color Wash, Matte color source, Photo separation, None, and Hairline Auto in EN/RU.
Persisted background/color-source/separation enum identities, reference-sRGB values, palette data, and analysis
diagnostics remain English code identities and are never localized. Catalog validation retains exact EN/RU key parity
and English fallback.
R10-B adds paired Color Gradient/Цветовой градиент and Soft Glow/Мягкое свечение names. It also corrects the earlier
literal mixed-language Color Wash label to the natural Russian Цветовая заливка. Stable enum identities and raster data
remain locale-independent; the correction changes presentation only.

R10-C changes no product strings or stable IDs. The EN/RU broad-family and Professional-term resolvers move with the
shared `Fovium.ColorSemantics` model, while the Picker remains only a presentation client. The developer report emits
both localized names from the same production catalogs and its Explorer switches EN/RU display without making locale an
input to classification, geometry, signatures, or creative matching. Research-only RU candidates remain explicitly
separate from shipped localization.

R11-A adds paired Photo Info vocabulary for Colors/Цвета, Dominant/Доминирующий, Palette/Палитра, Share/Доля, and one
bounded explanatory tooltip. Structural names reuse the shared broad/Professional resolver; creative names reuse the
separate stable-ID display catalog and remain secondary. HEX, OKLCH, raw RGB, weights, semantic identities, and palette
order remain locale-independent; percentage and visible names are formatted only at the presentation boundary.

R11-B replaces only the Photo Info signal labels with Characteristic/Характерный and Frequent shades/Частые оттенки,
then adds Notable colors/Заметные цвета. Stage's persisted/user-visible Dominant identity remains unchanged. Notable
selection, order, RGB, and semantic identities are locale-independent; localization cannot alter admission or ranking.

## Locale resolution

Before an explicit user choice:

```text
supported OS locale
→ matching Fovium locale

unsupported OS locale
→ en
```

At minimum, a Russian environment resolves to `ru`; an unsupported environment resolves to `en`. Once the user selects a
language in Settings → General, preserve that choice until reset or another explicit selection.

## Fallback and failure

Lookup follows:

```text
requested locale
↓ missing key
English
↓ missing English key
visible key + diagnostic warning
```

Missing translations do not crash or prevent photograph viewing. R1 falls back from the resolved locale to English and
then returns the visible key while emitting a diagnostic trace. Catalog parse/load failure is currently an
application-startup boundary failure rather than a recoverable missing-key case; future user-editable catalogs would
require a different policy.

## Translation boundary

Translate user-interface concepts, for example Settings, Background, Fit, Actual size, Photo information, and
Performance.

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

Advanced metadata may retain canonical tag names. User-entered paths, filenames, profile names, camera models, and
embedded source text remain source data. The product name **Fovium** never changes by locale.
