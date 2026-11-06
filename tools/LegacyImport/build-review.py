"""Build an escaped, self-contained review from persisted migration audit snapshots."""
import html
import json
import sys
from pathlib import Path


def esc(value):
    return html.escape(str(value or ""))


def refs(items):
    return "".join("<p class='ref'>↗ " + esc(r.get("displayText") or r.get("target")) +
                   "</p>" for r in items or [])


def markers(items):
    return " ".join("<span class='tag'>" + esc(m["category"] + ": " + m["value"]) +
                    "</span>" for m in items or [])


def senses(items):
    result = ""
    for sense in items:
        result += "<div class='sense'><b>" + str(sense["number"]) + ".</b> "
        result += markers(sense.get("markers")) + "<p>" + esc(sense["definition"]) + "</p>"
        result += refs(sense.get("crossReferences"))
        for example in sense["examples"]:
            result += "<div class='example'><strong>" + esc(example["zh"]) + "</strong><br>"
            result += esc(example["ka"]) or "<em>ქართული თარგმანი აკლია</em>"
            result += markers(example.get("markers")) + refs(example.get("crossReferences")) + "</div>"
        result += "</div>"
    return result


rows = json.loads(Path(sys.argv[1]).read_text(encoding="utf-8-sig"))
parts = ["""<!doctype html><html lang='ka'><meta charset='utf-8'>
<title>პირველი 20 სტატიის მიგრაცია</title><style>
body{font:16px/1.7 system-ui,sans-serif;max-width:1380px;margin:40px auto;padding:0 24px;color:#192c39;background:#f6f8fa}
article{background:white;border:1px solid #dae1e7;border-radius:12px;padding:24px;margin:24px 0}
.columns{display:grid;grid-template-columns:1fr 1fr;gap:28px}pre{white-space:pre-wrap;overflow-wrap:anywhere;font:inherit;background:#f7f8fa;padding:16px}
.tag{display:inline-block;background:#edf0fb;color:#354782;padding:1px 8px;border-radius:6px;font-size:13px}
.sense{border-left:3px solid #bdcbd5;padding-left:16px;margin:16px 0}.example{background:#f1f7f6;padding:12px;margin:8px 0}
.ref{color:#245fa0}summary{cursor:pointer;font-weight:600}.issues{color:#87531c}h2{margin-top:0}
@media(max-width:850px){.columns{grid-template-columns:1fr}}@media print{article{break-inside:avoid}.columns{grid-template-columns:1fr 1fr}}
</style><h1>პირველი 20 სტატიის სატესტო მიგრაცია</h1>
<p>2026-11-04 · სატესტო სერვერი 178.104.138.129 · ID 35–54 · ყველა ჩანაწერი new_entry და შესამოწმებლად მონიშნული.</p>
<p>მარცხნივ უცვლელი საწყისი ტექსტია; მარჯვნივ — ბაზაში შენახული ახალი სტრუქტურა. გაფრთხილებები რედაქტორის შემოწმებას საჭიროებს.</p>"""]
for row in rows:
    snap = row["snapshot"]
    source = snap["source"]
    content = snap["content"]
    converted = ""
    for group in content["gramGroups"]:
        converted += "<h3>POS: " + esc(group.get("pos")) + "</h3>" + markers(group.get("markers"))
        converted += refs(group.get("crossReferences")) + senses(group["senses"])
    converted += senses(content["senses"])
    forms = snap["forms"]
    form_text = " · ".join(esc(f["value"]) for f in forms["pinyins"] + forms["complexForms"])
    parts.append("<article id='entry-" + str(row["entryId"]) + "'><h2>" + esc(source["word"]) +
                 " <small>ძველი #" + esc(source["sourceId"]) + " → ახალი #" + str(row["entryId"]) + "</small></h2>" +
                 "<p>" + form_text + "</p><div class='columns'><section><h3>ძველი ტექსტი</h3><pre>" +
                 esc(source["translation"]) + "</pre></section><section><h3>ახალი სტრუქტურა</h3>" + converted + "</section></div>")
    parts.append("<details><summary>დამხმარე მასალა და კომენტარი</summary><div class='columns'><pre>" +
                 esc(source.get("workingSet")) + "</pre><section>" +
                 "".join("<h4>" + k.upper() + "</h4><pre>" + esc(v) + "</pre>" for k, v in snap["reference"].items()) +
                 "</section></div><h4>კომენტარი</h4><pre>" + esc(source.get("comment")) + "</pre></details>")
    parts.append("<details class='issues'><summary>შემოწმების შენიშვნები (" + str(len(snap["issues"])) + ")</summary><ul>" +
                 "".join("<li><b>" + esc(i["code"]) + "</b><pre>" + esc(i["fragment"]) + "</pre></li>" for i in snap["issues"]) +
                 "</ul></details></article>")
parts.append("</html>")
Path(sys.argv[2]).write_text("\n".join(parts), encoding="utf-8")
print(f"Wrote review for {len(rows)} persisted entries.")
