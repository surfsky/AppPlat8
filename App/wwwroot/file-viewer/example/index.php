<?php

declare(strict_types=1);

$renderers = [
    'pdf' => 'pdfjs-dist',
    'ofd' => 'DLTech21/ofd.js',
    'dxf' => '@flyfish-dev/cad-viewer',
    'dwg' => '@flyfish-dev/cad-viewer',
    'dwf' => '@flyfish-dev/cad-viewer',
    'dwfx' => '@flyfish-dev/cad-viewer',
    'xps' => '@flyfish-dev/cad-viewer',
    'json' => 'highlight.js',
    'php' => 'highlight.js',
];

function extensionOf(string $filename): string
{
    return strtolower(pathinfo($filename, PATHINFO_EXTENSION));
}

function buildPreviewPlan(array $files, array $renderers): array
{
    return array_map(static function (string $filename) use ($renderers): array {
        $extension = extensionOf($filename);
        $renderer = $renderers[$extension] ?? 'fallback';

        return [
            'filename' => $filename,
            'extension' => $extension,
            'renderer' => $renderer,
            'async' => $renderer !== 'fallback',
        ];
    }, $files);
}

$plans = buildPreviewPlan([
    'invoice.ofd',
    'house.dwfx',
    'controller.php',
], $renderers);

header('Content-Type: application/json; charset=utf-8');
echo json_encode($plans, JSON_PRETTY_PRINT | JSON_UNESCAPED_SLASHES);
