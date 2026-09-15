import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { JSDOM } from 'jsdom';
import postcss from 'postcss';

// Import styles and icons only. Prototype scripts and demonstration records are never shipped.
const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../..');
const source = path.join(root, 'docs/design-reference');
const target = path.join(root, 'frontend/editorial-panel/src');
const panel = new JSDOM(fs.readFileSync(path.join(source, 'Editorial Panel_v1.html'), 'utf8')).window.document;
const entry = new JSDOM(fs.readFileSync(path.join(source, 'Entry_Editor_v1.html'), 'utf8')).window.document;
const base = postcss.parse(panel.querySelector('style').textContent);
base.walkRules(rule => {
  if (rule.parent.type === 'atrule' && /keyframes$/.test(rule.parent.name)) return;
  if (rule.selector.includes(':root') && rule.selector !== ':root') { rule.remove(); return; }
  rule.selector = rule.selectors.map(s => s === ':root' || /^(html\s*>\s*body|body|html)$/.test(s)
    ? '.handoff-ui' : `:where(.handoff-ui) ${s}`).join(', ');
});
const styles = [...panel.querySelectorAll('[data-handoff-page] style')].map(s => s.textContent.replace(/--font-size-base:\s*15px/g, '--font-size-base: var(--panel-font-size, 15px)'));
styles.push(...[...entry.querySelectorAll('#cx-entry-v2 style')].map(s => s.textContent));
fs.writeFileSync(path.join(target, 'handoff-reference.css'), '/* Imported from the supplied HTML. Regenerate with tools/import-design-reference.mjs. */\n' + base + '\n' + styles.join('\n'));
const buttons = [...panel.querySelectorAll('#cge-home-final .cge-actions button')];
const icons = buttons.map(b => b.querySelector('svg').innerHTML);
const names = ['home','messages','notifications','new','media','statistics','team'];
const iconPath = path.join(target, 'app/shared/panel-icon');
fs.mkdirSync(iconPath, { recursive: true });
const cases = icons.map((v,i) => `@case ('${names[i]}') { ${v} }`).join('\n');
fs.writeFileSync(path.join(iconPath, 'panel-icon.ts'), `import { ChangeDetectionStrategy, Component, input } from '@angular/core';
@Component({selector: 'app-panel-icon', template: \`<svg width="27" height="27" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">@switch(name()) {${cases}
@case ('logout') {<path d="M10 17v4H3V3h7v4M8 12h13m-5-5 5 5-5 5"/>}
@case ('edit') {<path d="m15 5 4 4M4 20l4-1L20 7a2 2 0 0 0-4-4L4 15z"/>}
}</svg>\`, styles: [':host {display:inline-flex;align-items:center}'], changeDetection: ChangeDetectionStrategy.OnPush})
export class PanelIcon { readonly name = input.required<string>(); }
`);
console.log('Imported reference styles and seven navigation icons.');
