import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { resolve, relative, isAbsolute } from 'node:path';

const root = fileURLToPath(new URL('../docs/', import.meta.url));
const entries = JSON.parse(readFileSync(resolve(root, 'gallery.json'), 'utf8'));
if (!Array.isArray(entries) || entries.length === 0) throw new Error('Gallery must contain screenshots.');
const ids = new Set();
const games = new Set();
const escape = value => String(value).replace(/[&<>"']/g, c => ({'&':'&amp;','<':'&lt;','>':'&gt;','"':'&quot;',"'":'&#39;'}[c]));
for (const entry of entries) {
  if (typeof entry.id !== 'string' || !/^[a-z0-9-]+$/.test(entry.id) || ids.has(entry.id)) throw new Error('Invalid or duplicate image ID.');
  ids.add(entry.id);
  for (const key of ['title', 'caption', 'alt', 'image', 'thumbnail']) if (typeof entry[key] !== 'string' || !entry[key].trim()) throw new Error(`Missing ${key}.`);
  const game = entry.title.trim().toLowerCase();
  if (games.has(game)) throw new Error(`Keep one screenshot per game: ${entry.title}`);
  games.add(game);
  for (const key of ['width', 'height']) if (!Number.isInteger(entry[key]) || entry[key] < 1) throw new Error(`Invalid ${key}.`);
  if (typeof entry.featured !== 'boolean') throw new Error('featured must be true or false.');
  for (const key of ['image', 'thumbnail']) {
    if (!/^assets\/screenshots\/[a-z0-9-]+\.png$/.test(entry[key])) throw new Error('Images must use local screenshot PNG paths.');
    const path = resolve(root, entry[key]);
    if (isAbsolute(relative(root, path)) || relative(root, path).startsWith('..') || !existsSync(path)) throw new Error(`Missing asset: ${entry[key]}`);
    const bytes = readFileSync(path);
    if (bytes.length < 24 || bytes.subarray(0, 8).toString('hex') !== '89504e470d0a1a0a') throw new Error(`Not a PNG: ${entry[key]}`);
    const width = bytes.readUInt32BE(16), height = bytes.readUInt32BE(20);
    if (key === 'image' && (width !== entry.width || height !== entry.height)) throw new Error(`Incorrect image dimensions: ${entry.id}`);
    if (key === 'thumbnail' && width * entry.height !== height * entry.width) throw new Error(`Incorrect thumbnail proportions: ${entry.id}`);
  }
}
if (!entries.some(e => e.featured)) throw new Error('Choose at least one featured screenshot.');
const cards = list => list.map(e => `<figure class="shot" id="${e.id}"><a href="${escape(e.image)}" data-image data-id="${e.id}" data-title="${escape(e.title)}" data-caption="${escape(e.caption)}" aria-label="Enlarge ${escape(e.title)}: ${escape(e.caption)}"><img src="${escape(e.thumbnail)}" width="${e.width}" height="${e.height}" loading="lazy" decoding="async" alt="${escape(e.alt)}"></a><figcaption><h3>${escape(e.title)}</h3><p>${escape(e.caption)}</p></figcaption></figure>`).join('\n      ');
const viewer = `<dialog class="viewer" id="image-viewer" aria-labelledby="viewer-title" aria-describedby="viewer-caption">
    <div class="viewer-top"><p data-counter aria-live="polite"></p><button class="button" type="button" data-close autofocus>Close</button></div>
    <figure><img alt=""><figcaption><h2 id="viewer-title"></h2><p id="viewer-caption"></p></figcaption></figure>
    <p class="viewer-error" data-error hidden>The image could not be loaded. Try opening the original below.</p>
    <div class="viewer-bottom"><button class="button" type="button" data-prev aria-label="Previous screenshot">Previous</button><a data-original href="gallery.html">Open original</a><button class="button" type="button" data-next aria-label="Next screenshot">Next</button></div>
  </dialog>`;
const check = process.argv.includes('--check');
for (const name of ['index.html', 'gallery.html']) {
  const path = resolve(root, name);
  const before = readFileSync(path, 'utf8');
  let output = before;
  for (const [marker, html] of [['GALLERY', cards(name === 'index.html' ? entries.filter(e => e.featured).slice(0, 3) : entries)], ['VIEWER', viewer]]) {
    const pattern = new RegExp(`<!-- ${marker}:START -->[\\s\\S]*?<!-- ${marker}:END -->`);
    if (!pattern.test(output)) throw new Error(`Missing ${marker} marker in ${name}`);
    output = output.replace(pattern, `<!-- ${marker}:START -->\n      ${html}\n      <!-- ${marker}:END -->`);
  }
  if (check && output !== before) throw new Error(`${name} needs regeneration: node scripts/build-website.mjs`);
  if (!check) writeFileSync(path, output);
}
console.log(`${check ? 'Checked' : 'Generated'} gallery: ${entries.length} screenshots.`);
