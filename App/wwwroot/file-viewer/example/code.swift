struct PreviewFormat {
    let name: String
    let asyncRenderer: Bool
}

let formats = [
    PreviewFormat(name: "pdf", asyncRenderer: true),
    PreviewFormat(name: "markdown", asyncRenderer: false)
]
