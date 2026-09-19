# Fovium UI design

Role: Project-owned composition and control language for Fovium application surfaces.
Read when: Creating or changing a window, dialog, Settings page, card, button, swatch, or scroll surface.
Authoritative for: Reference surfaces, shared visual primitives, typography hierarchy, secondary-window chrome,
control variants, scrolling treatment, spacing/radius principles, and prohibited one-off UI patterns.
Not authoritative for: Exact palette values, application-theme selection, photograph/Stage rendering, interaction
bindings, or persisted settings semantics.

## Principles and references

The photograph remains the primary UI. Application surfaces are quiet, dark, spatially restrained, and use violet
accent only for a selected state, the primary action, or focus—not as decoration. Home and Settings are the reference
surfaces: Home owns the more expressive entry composition; Settings owns application depth, cards, control feel, and
secondary-window chrome. New surfaces should feel related without forcing both references into the same layout.

Exact resources live in `Fovium/Themes`, not this document:

- `FoviumTokens.axaml` owns semantic colors/brushes, dimensions, radii, icon geometry, fades, and the scrollbar thumb;
- `FoviumControls.axaml` owns typography, cards, button variants, close chrome, swatches, and local Stage rail roles;
- `FoviumWindows.axaml` owns the Fovium secondary-window and inner surface;
- `EdgeFadeScrollViewer` owns vertical overflow state, edge fades, and the inset scrollbar composition.

Use semantic resources such as App Background, Window/Sidebar/Card/Elevated Surface, Subtle/Interactive Border,
Primary/Secondary Text, Accent states, Focus Ring, Warning, and the shared spacing/radius tokens. A new raw color or
dimension is justified only when it represents content data or a genuinely new role; it must not be a local spelling of
an existing role. Theme semantics remain owned by [`THEMES.md`](THEMES.md).

## Typography and surfaces

Use the shared hierarchy: page title and description, dialog title, card title, body, supporting text, caption, and
numeric value. Labels should not invent nearby font sizes or secondary colors. Cards use `fovium-card`; elevated
content should remain bounded, padded, and separated from the physical window edge. Repeated small panels may use a
documented shared role, but a page should not create a private visual epoch.

Spacing follows a small-to-large rhythm: compact gaps inside a control, ordinary gaps inside cards, larger gaps between
cards or window regions. Small radii belong to controls, medium radii to cards, and large radii to secondary-window
surfaces. Exact values remain resource-owned.

## Buttons, close chrome, and swatches

Buttons use shared `fovium-button` variants: the base style is Secondary, `primary` is the single emphasized action,
`quiet` is low-emphasis, `icon-button` is a bounded icon action, and `segmented` is a compact mode choice. Shared styles
own content centering, typography, height, padding, focus, hover, pressed, and disabled states. Labels must work without
one-off margins in both English and Russian.

Every Settings-family secondary window uses `fovium-secondary-window`, `fovium-window-surface`, and the one
`window-close` template. The template owns one vector geometry, optical size, button size/radius, and interaction
states. Each window supplies the localized Close automation name and tooltip. These windows are decoration-free,
owner-relative, omitted from the taskbar where appropriate, and never globally Topmost.

Interactive colors use `ColorSwatchButton`. It is a real focusable Button with a semantic border and shared hover/focus
states; the caller supplies the brush plus localized automation name and tooltip. It is used for Stage Custom, Matte,
Cursor Highlight, default Markup, and active Viewer Markup. Static before/after color previews are data and need not
pretend to be buttons.

## Scrolling and overflow

Settings pages keep title/description fixed and scroll only their cards. `EdgeFadeScrollViewer` is the canonical
vertical composition for Settings content and bounded mode lists. It shows the top fade only when content exists above
and the bottom fade only when content exists below; fades never hit-test. The shared fade depth and brushes come from
tokens, so content disappears into the owning surface rather than a hard opaque clip.

The vertical scrollbar separates usability from appearance: a comfortably wide transparent pointer target surrounds a
thin centered visual thumb. Hover feedback is immediate and restrained; the track stays visually quiet and inset from
the physical window edge. Wheel, keyboard, focus reveal, and drag retain normal Avalonia semantics. A mode rail uses
the same overflow language and brings the selected item into view.

## Dialog and contextual-editor composition

Dialogs use a short title/supporting-text hierarchy, bounded cards only where grouping helps, and primary/secondary
actions aligned consistently. A contextual editor keeps selection separate from properties: a compact keyboard-aware
rail on the left and a stable property panel on the right. Only meaningful controls are visible. If a selection has no
options, show a quiet explanatory message rather than an empty or disabled form.

## Prohibited patterns

- native titlebar chrome on a Fovium secondary dialog;
- a local copy of the close-button style or a text `×` used as window chrome;
- local Primary/Secondary/accent button styles that duplicate shared variants;
- Border-plus-Button copies instead of `ColorSwatchButton`;
- one-off RGB utility dialogs when the shared HSV/RGB/HEX picker fits;
- interactive controls or scrolling content touching a physical window edge;
- full-width opaque clipping for Settings or bounded mode lists without directional edge fades;
- making the thin scrollbar visual equal to its pointer hit target;
- exposing irrelevant disabled controls instead of contextual composition;
- using application accent to alter Stage, photograph pixels, analysis truth, or color-management input.
