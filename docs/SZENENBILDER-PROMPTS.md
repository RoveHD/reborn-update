# Szenenbilder und Prompts

Erzeugt am 25.10.2026 mit dem eingebauten `image_gen`-Werkzeug (kein CLI/API-Fallback). Bilder sind Projektassets unter `src/RebornLauncher/assets/scenery/`, keine fertigen UI-Mockups. Originalausgaben bleiben erhalten. Bestehende Pokémon-Artworks werden separat eingeblendet. Die Vorlage dient als Gestaltungsrichtung.

## Landschaften

Gemeinsamer Prompt:

Use case: stylized-concept. Production asset for a cinematic Pokemon-inspired desktop game launcher, painterly richly detailed landscape, widescreen 16:9, 2048x1152 intended. ONLY environment: absolutely no creatures, people, Pokemon, logos, letters, text, interface, watermark, borders, or collage. Large quiet low-detail central 40 percent of composition for real overlaid interactive logo and controls, middle should be dark enough for light text. Side framing and low foreground ledges on outer left and right for separately rendered Pokemon. Beautiful atmospheric perspective, high quality painted game environment. Original scene, not a copy of any screenshot.

### standard

Moonlit enchanted forest opening onto an ancient quiet city. Violet-blue night, crescent moon above left trees, distant towers on right, tiny amber window lights. Mossy stone paths, silver leaves, subtle fireflies at sides, deep navy open center. No pumpkins or Halloween props.

### winter

Snowy mountain village seen between snow-laden pines. Turquoise aurora in navy blue starry sky, tiny amber cabin windows far right, clean snow banks and icy stream along bottom, frost-coated branches framing outer corners. Peaceful crystalline winter night, no Christmas text, keep central sky subdued.

### spring

Lush spring garden at blue hour. Winding stone path between flowering trees, violet pink and white blossoms at outer edges, a tiny cottage far left, green moss and flowers on low side ledges. Distant dusky lavender hills through a wide quiet central opening, soft warm lanterns low on sides. Easter feeling without any painted eggs; interactive eggs added by code.

### summer

Warm coastal evening after sunset. Tropical coastline with curving palms on far edges, calm turquoise sea, orange lavender horizon, small beach houses at far left with warm lights, rounded stones and beach grasses at bottom corners. Center dark subdued mauve blue sky and open sea, no umbrellas or balls (live props added separately).

### harvest

Golden autumn woodland lane beside an old stone village. Copper amber and crimson trees framing sides, cool dusky blue-violet distant valley in wide central opening, fallen leaves on wet cobblestone ground with lantern reflections. Village windows glow at outer right. Cozy dusk, no Halloween faces or pumpkins.

### newyear

Festive winter town waterfront at deep indigo night. Elegant old town buildings on far left and right, warm lantern strings near side edges, deep blue sky wide open above calm reflective lake, stone promenades at bottom corners dusted with snow, distant lit clocktower at outer right. No baked fireworks: fireworks are drawn live by the app. No numbers, no text.

### arceus

Majestic celestial ancient sanctuary in the clouds. Luminous ivory marble ruins and cracked columns at far edges, gold trimmed stone ledges low left and right, deep indigo and violet cosmic sky with subtle nebula and tiny stars, ethereal pale gold atmospheric rays on edges. Broad quiet deep-blue center, grand and mysterious. No creature, no symbol/logo.

### halloween (Bearbeitung der Nutzervorlage)

Remove all interface and characters, reconstruct the atmospheric dark violet Halloween landscape with moon, Gothic manor, autumn trees, low stone walls, lanterns and pumpkins. Preserve wide 16:9 composition with deep dark clean central 40 percent for interactive UI. No text, logo, UI, creatures, borders or watermark. Painted cinematic environment with low side ledges for live sprites.

## Transparente Vordergründe

Gemeinsamer Prompt:

Generate a production 2.5D foreground overlay for a cinematic painted game landscape. Wide 16:9 composition. TRANSPARENT BACKGROUND with real alpha, no backdrop. Only sparse framing elements confined to outermost 12 percent of left and right edges and outer top corners. Central 76 percent is entirely transparent, bottom middle transparent. Keep most of the entire canvas empty/transparent. Natural rich painterly detail, soft atmospheric edges, not flat vector clipart. No characters, Pokemon, logos, UI, letters, borders or watermark. This is one standalone transparent foreground layer for live mouse parallax.

### standard

Dark navy-violet leafy tree branches at upper left and upper right corners, tiny silver leaves; low mossy rocks at lower outer corners.

### halloween

Dark gnarled twig branches with sparse burnt-orange autumn leaves entering from upper corners and outermost sides. A few softly lit dark leaves at bottom corners. No pumpkins.

### winter

Snow-covered pine boughs entering the upper outer corners and frost crystals and tufts of snow at bottom outer corners.

### spring

Softly painted flowering branches with tiny blush pink and ivory blossoms entering the upper outer corners, little ferns and moss at bottom corners.

### summer

Dark green palm fronds entering upper left and upper right corners, low sea grass tufts at bottom outer corners. Dusky violet and warm sunset rim light.

### harvest

Deep russet and amber maple branches entering upper corners, rust red leaves and grass at bottom outer corners.

### newyear

Dark blue bare winter branches with a few delicate warm golden string lights entering upper corners, subtle frost at lower corners. No text, fireworks, numbers or hanging banners.

### arceus

Fractured ivory marble cornice fragments with muted gold details entering upper outer corners, a few pale stone shards at bottom outer corners; ethereal violet rim light.

## Ursprungsdateien

- `standard-landscape.png` ← `C:\Users\elias\.codex\generated_images\01a17259-2907-7d43-b31e-5e3a331e143c\exec-4283826b-b003-45c7-ae3b-e26f2c8dcd59.png`
- `standard-foreground.png` ← `C:\Users\elias\.codex\generated_images\01a17259-2907-7d43-b31e-5e3a331e143c\exec-bf54e229-9b69-48b9-ae61-8ae588dfef26.png`
- `halloween-landscape.png` ← `C:\Users\elias\.codex\generated_images\01a17259-2907-7d43-b31e-5e3a331e143c\exec-ce537cb7-6379-4ab4-96ee-4dec67809b42.png`
- `halloween-foreground.png` ← `C:\Users\elias\.codex\generated_images\01a17259-2907-7d43-b31e-5e3a331e143c\exec-241cda0a-356f-44ef-9330-e4b288c61588.png`
- `winter-landscape.png` ← `C:\Users\elias\.codex\generated_images\01a17259-2907-7d43-b31e-5e3a331e143c\exec-8bec4453-100e-474d-a398-512bab5dec31.png`
- `winter-foreground.png` ← `C:\Users\elias\.codex\generated_images\01a17259-2907-7d43-b31e-5e3a331e143c\exec-abeaab7f-0b9e-4808-8366-a98f27b1eae0.png`
- `spring-landscape.png` ← `C:\Users\elias\.codex\generated_images\01a17259-2907-7d43-b31e-5e3a331e143c\exec-5ebb1b50-54fc-49fb-a150-5c6aa5c34893.png`
- `spring-foreground.png` ← `C:\Users\elias\.codex\generated_images\01a17259-2907-7d43-b31e-5e3a331e143c\exec-ba667a36-fe36-4523-85f3-a6e06a796946.png`
- `summer-landscape.png` ← `C:\Users\elias\.codex\generated_images\01a17259-2907-7d43-b31e-5e3a331e143c\exec-055b2fdb-eb0e-4264-88aa-c58ca78de46b.png`
- `summer-foreground.png` ← `C:\Users\elias\.codex\generated_images\01a17259-2907-7d43-b31e-5e3a331e143c\exec-adfc7429-8f8a-4ea0-87d8-24ff7fa52eb2.png`
- `harvest-landscape.png` ← `C:\Users\elias\.codex\generated_images\01a17259-2907-7d43-b31e-5e3a331e143c\exec-c959803c-3413-4c0e-b0bb-bdf3391193e3.png`
- `harvest-foreground.png` ← `C:\Users\elias\.codex\generated_images\01a17259-2907-7d43-b31e-5e3a331e143c\exec-ade60d7c-877c-400d-8c63-d819765a17ed.png`
- `newyear-landscape.png` ← `C:\Users\elias\.codex\generated_images\01a17259-2907-7d43-b31e-5e3a331e143c\exec-747b0d4b-f2fe-480e-96bf-78c747d8eac5.png`
- `newyear-foreground.png` ← `C:\Users\elias\.codex\generated_images\01a17259-2907-7d43-b31e-5e3a331e143c\exec-77ec81fb-42fd-4e34-a9a3-45527bc8734f.png`
- `arceus-landscape.png` ← `C:\Users\elias\.codex\generated_images\01a17259-2907-7d43-b31e-5e3a331e143c\exec-c673c554-8d30-41c9-bebc-84721cc3a13e.png`
- `arceus-foreground.png` ← `C:\Users\elias\.codex\generated_images\01a17259-2907-7d43-b31e-5e3a331e143c\exec-6deb9d94-26aa-49f6-af33-01dc67b33915.png`
