# Editorial Exemplars

This folder stores locally saved exemplar article material for the writing pipeline.

## Files
- `index.json` → fetch/index status for all requested exemplars
- `*.md` → locally saved page extracts or fetch-failure placeholders
- `../good-article-exemplars.md` → the curated title list
- `../good-article-rubric.md` → distilled targets
- `../bad-article-patterns.md` → distilled failure patterns

## Current state
Most direct publisher fetches failed because:
- some URLs have changed
- some sites returned 404
- The Balance returned 402
- search API quota was exhausted during lookup

One exemplar successfully saved:
- NerdWallet, "How to Budget Money: A Step-By-Step Guide"

## Intended usage
These files are for:
- exemplar retrieval
- pattern extraction
- future compact prompt synthesis
- QA comparison against real article structures

These files are NOT meant to be pasted raw into every writer prompt.
