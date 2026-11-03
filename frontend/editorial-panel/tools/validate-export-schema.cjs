// Usage: node tools/validate-export-schema.cjs <directory of JSON exports>
// Uses the AJV validator installed with the Angular toolchain.
const fs = require('node:fs');
const path = require('node:path');
const Ajv = require('ajv');
const schema = require('../../../docs/schemas/chinese-ge-export-v3.schema.json');
const directory = process.argv[2];
if (!directory) throw new Error('Pass the directory containing JSON export samples.');
const validate = new Ajv({ allErrors: true }).compile(schema);
const files = fs.readdirSync(directory).filter((file) => file.endsWith('.json'));
if (!files.length) throw new Error('No JSON exports found.');
for (const file of files) {
  const data = JSON.parse(fs.readFileSync(path.join(directory, file), 'utf8'));
  if (!validate(data)) throw new Error(file + ': ' + JSON.stringify(validate.errors));
}
console.log(`Validated ${files.length} JSON exports against project schema 3.`);
