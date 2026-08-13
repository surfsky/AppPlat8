data class PreviewFormat(
    val extension: String,
    val renderer: String,
    val lazy: Boolean = true
)

fun main() {
    val pdf = PreviewFormat("pdf", "pdfjs")
    println("${pdf.extension} -> ${pdf.renderer}")
}
