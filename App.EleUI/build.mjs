import { build, context } from "esbuild";
import { mkdir } from "node:fs/promises";
import { existsSync } from "node:fs";
import path from "node:path";
import process from "node:process";
import { fileURLToPath } from "node:url";

const rootDir = path.dirname(fileURLToPath(import.meta.url));
const outFile = path.resolve(rootDir, "wwwroot/eleui/eleui.js");
const staleFiles = [
  path.resolve(rootDir, "wwwroot/eleui/eleui.css"),
  path.resolve(rootDir, "wwwroot/eleui/eleui.css.map")
];

const buildOptions = {
  entryPoints: [path.resolve(rootDir, "EleUI/EleUIJs/EleUI.js")],
  bundle: true,
  format: "esm",
  platform: "browser",
  sourcemap: true,
  minify: true,
  target: ["es2019"],
  legalComments: "none",
  loader: {
    ".css": "text"
  },
  outfile: outFile
};

await mkdir(path.dirname(outFile), { recursive: true });
for (const file of staleFiles) {
  if (existsSync(file)) {
    await import("node:fs/promises").then((m) => m.rm(file, { force: true }));
  }
}

if (process.argv.includes("--watch")) {
  const ctx = await context(buildOptions);
  await ctx.watch();
  console.log("[App.EleUI] watch mode started");
} else {
  await build(buildOptions);
  console.log("[App.EleUI] built static assets to wwwroot/eleui");
}
