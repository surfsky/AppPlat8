# App.EleUI

App.EleUI is a standalone Razor Class Library that provides:

- Strongly typed Razor TagHelpers for Element Plus based UI composition.
- A bundled browser runtime (`eleui.js`) for form/table/list/manager behaviors.
- Static web assets served from `/_content/App.EleUI/eleui/`.

## Usage

1. Reference the package in your ASP.NET Core app.
2. Add script in layout:

```html
<script src="/_content/App.EleUI/eleui/eleui.js" type="module"></script>
```

3. Enable tag helpers in `_ViewImports.cshtml`:

```cshtml
@addTagHelper *, App.EleUI
```

## Build static assets

The project runs frontend build automatically during `dotnet build` and emits:

- `wwwroot/eleui/eleui.js`

The runtime module already includes required EleUI CSS at build time.