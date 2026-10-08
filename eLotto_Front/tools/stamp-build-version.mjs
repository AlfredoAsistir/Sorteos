import { randomUUID } from 'node:crypto';
import { existsSync, readFileSync, writeFileSync } from 'node:fs';
import { join } from 'node:path';

const placeholder = '__APP_VERSION__';
const packageJson = JSON.parse(readFileSync('package.json', 'utf8'));
const outputBase = join('dist', 'eLotto');

const outputDirectory = [join(outputBase, 'browser'), outputBase]
  .find(candidate => existsSync(join(candidate, 'index.html')));

if (!outputDirectory) throw new Error(`No se encontro index.html dentro de ${outputBase}.`);

const generatedAt = new Date().toISOString();
const timestamp = generatedAt.replace(/[-:.TZ]/g, '');
const version = `${packageJson.version}+${timestamp}.${randomUUID().slice(0, 8)}`;
const indexPath = join(outputDirectory, 'index.html');
const indexHtml = readFileSync(indexPath, 'utf8');

if (!indexHtml.includes(placeholder)) {
  throw new Error(`No se encontro el marcador ${placeholder} en ${indexPath}.`);
}

writeFileSync(indexPath, indexHtml.replaceAll(placeholder, version));
writeFileSync(join(outputDirectory, 'version.json'), `${JSON.stringify({ version, generatedAt }, null, 2)}\n`);
console.log(`Version de aplicacion generada: ${version}`);

