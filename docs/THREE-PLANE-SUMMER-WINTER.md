# Drei Parallax-Ebenen als Mindestumfang

Bei weiteren Theme-Änderungen bleiben mindestens drei sichtbare, unabhängig bewegte Tiefenebenen erhalten. Drei Bilddateien mit demselben Transform zählen nicht als drei Tiefenstufen. Reduzierte Bewegung darf die Kamera selbstverständlich anhalten.

Geprüft: `test-results/three-plane-final-depth/checks.json` (175 Kamera-/Fensterfälle in sieben Themes), `test-results/three-plane-reference/result.json` (64 Layout-Kombinationen). Sommer und Winter zusätzlich an beiden diagonalen Kameraextremen gerendert und visuell geprüft. Lokaler selbständiger Build: `dist/three-plane-preview/launcher.exe`; isolierte Einstiege: `Vorschau-Sommer.cmd`, `Vorschau-Winter.cmd`. Keine Veröffentlichung.

Sommer: ferne Kulisse; durchgehende Wasser-/Uferfläche mit Schiggy, Lapras und Mantirps; nahes Blattwerk. Lapras und Mantirps samt Wasserkontakt sind in dieselbe Wasserebene gemalt. Keine separate Bewegung der schwimmenden Pokémon gegenüber dem Wasser.

Winter: vorhandene Landschaft mit Pokémon; obere Äste des vorhandenen transparenten Rahmens; untere Schneebüsche und Felsen des Rahmens. Die beiden Rahmenteile haben getrennte Kamera-Transforms. Der Eiszapfen folgt den oberen Ästen. Die abgelehnte neue Winter-Freistellung wird nicht verwendet; Originalgrafiken bleiben erhalten.

## Sommergrafik

Werkzeug: integriertes Imagegen, Bearbeitung der vorhandenen Grafik. Ziel: `src/RebornLauncher/assets/scenery/parallax/summer-terrain.png`. Vorherige Grafik: `artifacts/three-plane-originals/summer-terrain.png`.

Verwendeter Prompt:

> Use case: compositing. Edit target image1 summer transparent terrain; image2 far sea for exact water reference. Output a single 1672x941 PNG transparent parallax MIDDLE WATER/TERRAIN plane. Preserve exact composition, positions and scale of Squirtle on left rocks, Lapras and Mantyke on right, rocks, shoreline, foliage. ADD the full continuous sea from image2 behind all three characters and shoreline, from the exact horizontal horizon y=523 down to bottom941. Water must be opaque, connected, reflecting sunset and matching existing illustration. Lapras and Mantyke INCLUDING their water contact rings are embedded in this WATER plane. Entire sky and distant islands/buildings above horizon transparent; left coast distant village remains excluded. Alpha edge at sea horizon, foreground land opaque, sky transparent. No new creatures, no redesign, no text. Preserve source registration1672x941; no crop, no reposition.

Korrektur der Küstenkante (integriertes Imagegen):

> Use case: precise-object-edit. Target image1 current transparent summer-water layer, image2 original far landscape alignment reference. Keep exact1672x941 canvas and ALL existing Pokemon and shoreline positions unchanged. Fix ONLY the upper edge of the opaque water: currently it starts around y503, which is20pixels TOO HIGH and cuts through the distant coastal village when composited. Make alpha fully transparent ABOVE the original sea horizon at y523. On far LEFT, continue transparency lower along the exact distant coastline contour from image2 so the distant houses and shore are not covered: coast ends around x500,y545, leftmost shoreline y550. Blend only within 2pixel natural waterline, not a broad fade. Keep Lapras's head and Mantyke intact, including any part above y523. Keep ALL water below horizon continuously OPAQUE, no alpha holes. Preserve the exact creatures, shore, lighting and water color; no movement of anything and no additions. TransparentPNG same source registration.
