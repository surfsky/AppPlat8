package main

import (
	"encoding/json"
	"fmt"
	"path/filepath"
	"strings"
)

type PreviewFile struct {
	Name     string `json:"name"`
	Renderer string `json:"renderer"`
	Async    bool   `json:"async"`
}

var renderers = map[string]string{
	".pdf":  "pdfjs-dist",
	".ofd":  "DLTech21/ofd.js",
	".typ":  "typst.ts",
	".dxf":  "@flyfish-dev/cad-viewer",
	".dwg":  "@flyfish-dev/cad-viewer",
	".dwf":  "@flyfish-dev/cad-viewer",
	".dwfx": "@flyfish-dev/cad-viewer",
	".xps":  "@flyfish-dev/cad-viewer",
	".json": "highlight.js",
}

func planPreview(name string) PreviewFile {
	ext := strings.ToLower(filepath.Ext(name))
	renderer := renderers[ext]
	if renderer == "" {
		renderer = "fallback"
	}

	return PreviewFile{
		Name:     name,
		Renderer: renderer,
		Async:    renderer != "fallback",
	}
}

func main() {
	files := []string{"contract.pdf", "invoice.ofd", "report.typ", "component.vue"}
	plans := make([]PreviewFile, 0, len(files))
	for _, name := range files {
		plans = append(plans, planPreview(name))
	}

	payload, err := json.MarshalIndent(plans, "", "  ")
	if err != nil {
		panic(err)
	}

	fmt.Println(string(payload))
}
