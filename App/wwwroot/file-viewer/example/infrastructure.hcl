resource "flyfish_viewer" "demo" {
  name    = "matrix-alignment"
  version = "local"

  features = [
    "lazy-renderers",
    "format-matrix",
    "safe-preview",
  ]
}
