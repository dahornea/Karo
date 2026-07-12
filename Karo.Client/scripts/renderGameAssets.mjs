import { readdir, stat } from 'node:fs/promises';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import sharp from 'sharp';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../src/assets/game');
const sourceFiles = await findSources(root);

for (const sourceFile of sourceFiles) {
  const outputFile = sourceFile.replace(/\\source\\([^\\]+)\.source\.svg$/i, '\\$1.webp');
  const category = relative(root, sourceFile).split(/[\\/]/)[0];
  const quality = category === 'terrain' ? 80 : 86;

  await sharp(sourceFile, { density: 144 })
    .webp({ quality, alphaQuality: 92, effort: 6 })
    .toFile(outputFile);

  process.stdout.write(`rendered ${relative(root, outputFile)}\n`);
}

async function findSources(directory) {
  const entries = await readdir(directory);
  const files = [];

  for (const entry of entries) {
    const path = join(directory, entry);
    const info = await stat(path);
    if (info.isDirectory()) {
      files.push(...await findSources(path));
    } else if (entry.endsWith('.source.svg')) {
      files.push(path);
    }
  }

  return files.sort();
}
