# Application themes

Role: Contract for application UI theme semantics and future visual-system structure.
Read when: Styling Settings, menus, overlays, dialogs, metadata UI, error surfaces, temporary controls, or window chrome.
Authoritative for: Supported application themes, default theme, semantic visual roles, and separation from photographic Stage.
Not authoritative for: Stage appearance, photograph rendering, exact palette values, or product input.

## Initial themes

Fovium initially supports these application UI themes:

```text
Dark
Light
```

Dark is the default.

Application theme applies only to application UI, including Settings, context menus, requested overlays, dialogs, metadata surfaces, errors, temporary controls, and future window chrome where applicable.

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

Changing application theme must not silently change Stage mode. Changing Stage must not change Settings, menus, or dialogs. Neither system may modify the original photograph. Stage product semantics remain owned by [`PROJECT-VISION.md`](PROJECT-VISION.md).

## Visual-system direction

Future implementation should centralize semantic roles rather than scatter raw brushes or color constants through views. Expected roles include:

```text
Surface
ElevatedSurface
TextPrimary
TextSecondary
Border
Hover
Selected
Accent
Error
```

Exact colors, contrast values, platform chrome treatment, and theme resource mechanics wait for implementation evidence. Fovium should remain visually quiet and purpose-built rather than becoming a generic framework-theme demonstration.

R1 fixes the application UI to Dark and uses Avalonia Fluent for its context menus, Settings, and small dialogs/error surfaces. There is no selector or final semantic palette yet. Stage background, custom colors, Ambient treatment, and independent Matte do not derive from Dark theme resources. Light remains a contracted future option.
