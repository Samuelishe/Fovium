# Application themes

Role: Contract for application UI theme semantics and future visual-system structure.
Read when: Styling Settings, menus, overlays, dialogs, metadata UI, error surfaces, temporary controls, or window
chrome.
Authoritative for: Supported application themes, default theme, semantic visual roles, and separation from photographic
Stage.
Not authoritative for: Stage appearance, photograph rendering, exact palette values, or product input.

## Initial themes

Fovium initially supports these application UI themes:

```text
Dark
Light
```

Dark is the default.

Application theme applies only to application UI, including Settings, context menus, requested overlays, dialogs,
metadata surfaces, errors, temporary controls, and future window chrome where applicable.

## Theme and Stage are independent

Never equate the two systems:

```text
ApplicationTheme
    Dark
    Light

StageBackground
    Black
    Neutral
    Custom
    Ambient

MatteEnabled
    false / true
```

Changing application theme must not silently change Stage mode, Matte color/width, or Matte outer style. Changing Stage
must not change Settings, menus, or dialogs. Neither system may modify the original photograph. Stage product semantics
remain owned by [`PROJECT-VISION.md`](PROJECT-VISION.md).

## Visual-system implementation

The fixed-Dark implementation centralizes semantic resources for application/window/sidebar/card/elevated surfaces,
subtle and interactive borders, primary and secondary text, accent interaction states, focus, warning, selection, and
hover/pressed states. `Fovium/Themes/FoviumTokens.axaml` owns exact values. Views consume semantic resources rather than
copying palette literals for an already-owned role. Fovium remains visually quiet and purpose-built rather than a
generic framework-theme demonstration.

The composition and control contract—typography, cards, buttons, close chrome, swatches, secondary windows, and
edge-fade scrolling—is owned by [`UI-DESIGN.md`](UI-DESIGN.md). Light remains a contracted future option; the current
resource dictionary does not claim that it is implemented.

HOME-UX-R1 applies the current fixed-Dark application surface roles to the no-image landing state: restrained layered
surfaces, one violet primary action, subdued borders, and project-authored vector geometry. It remains independent of
the photographic Stage and is removed once a photograph is presented.

HOME-UX-R1-F1 removes the duplicate in-content app masthead, keeps Settings as one restrained top-right action, and
uses a softly layered hero plus scalable ambient radial fields rather than blur shaders or a fixed wallpaper. Real
Recent previews sit on darker secondary cards; unavailable state combines reduced opacity with localized text rather
than color alone. Left/right overflow fades are non-interactive and appear only when content is actually clipped.

R1 fixes the application UI to Dark and uses Avalonia Fluent for its context menus, Settings, and small dialogs/error
surfaces. R5-F3 retains ordinary themed Button/MenuItem semantics for the movable presenter panel and context menu while
using centralized project-owned monochrome vector geometry; checked overlay state and disabled history actions remain
semantic control states rather than icon color alone. SETTINGS-UX-R1 gives Settings a restrained dark
surface/elevated-card/selected-accent hierarchy and project-owned circular close button while retaining normal hover,
focus, selection, scrolling, and platform resize behavior. UI-SYSTEM-R1 promotes those accepted roles into shared
fixed-Dark token/control dictionaries and migrates Settings, Color Picker, and Shortcut Conflict to one chrome
authority. Stage background, custom colors, Ambient treatment, and independent Matte do not derive from Dark theme
resources.

SETTINGS-UX-R1-F1 extends that same secondary-window language to the reusable Color Picker and Shortcut Conflict:
decoration-free owned dark surfaces, rounded boundaries, project close controls, normal keyboard focus, and restrained
violet selection. The Color Picker's hue field and value preview are data, so their chroma does not redefine application
accent roles. Settings and bounded mode-list fades derive from the semantic owning surface, remain non-interactive, and
appear only at clipped edges. The shared scrollbar preserves a thin visual thumb inside a wider transparent pointer
target; no blur or external theme framework is introduced.
