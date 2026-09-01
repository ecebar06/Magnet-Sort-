# Word icon production pipeline

## Approved visual direction

- Friendly, polished casual-game icon.
- Soft 2.5D clay-like rendering with chunky, rounded forms.
- Deep dark-purple outer outline and a restrained lower-right shadow.
- Bright, harmonious colors and one gentle glossy highlight.
- Exactly one subject, centered, with a strong silhouette readable at 64 px.
- No word-button/magnet background, text, letters, badge, border, scene,
  watermark, character additions, or unrelated objects.

## Reusable generation prompt

Replace all values inside `<ANGLE BRACKETS>` for each word.

```text
Use case: stylized-concept.
Asset type: 512x512 mobile game word icon.
Create exactly one centered <WORD> icon.
Exact subject: <UNAMBIGUOUS SUBJECT DESCRIPTION, VIEW, COLOR AND REQUIRED DETAILS>.
Friendly polished casual mobile game art matching Word Sort: soft 2.5D
clay-like rendering, chunky clean silhouette, subtly rounded forms, deep
dark-purple outline, soft lower-right shadow contained close to the object,
bright but harmonious colors, gentle glossy highlight. <VIEW> view. The object
occupies about 76% of the square canvas with even padding and remains
recognizable at 64px. Genuine transparent RGBA background, alpha zero outside
the icon. No cream magnet tile behind it. No text, letters, scene, border,
badge, frame, watermark, duplicate subject or extra objects.
```

Add subject-specific negatives when ambiguity is likely. Example: Guitar adds
`no person, no hands`; Dog adds `no collar`; Apple adds `no extra fruit`.

## Production steps

1. Select a `hasSprite: true` word from `WordLibrary.json` and use its existing
   `spriteKey` as the filename, e.g. `music__guitar.png`.
2. Write a literal subject description. Do not rely on the word alone.
3. Generate one icon per request using the reusable prompt. Do not create a
   multi-icon sheet.
4. Reject outputs with an incorrect subject, additional objects, embedded text,
   cropped silhouettes, inconsistent outline, or excessive detail.
5. Verify the file technically before import: exactly 512x512, PNG RGBA, and
   corner alpha must be 0. A visible checkerboard is not transparency.
6. Review at both 512px and 64px. At 64px the subject must remain immediately
   recognizable.
7. Save the approved file under `Assets/_Game/Resources/Icons/Generated/` using
   the exact `spriteKey` filename.
8. Unity import settings: Texture Type `Sprite (2D and UI)`, Sprite Mode
   `Single`, Alpha Is Transparency enabled, mipmaps disabled, Wrap Mode Clamp,
   Filter Mode Bilinear, Max Size 512, no lossy compression during review.
9. Assign the imported Sprite to the matching key in `MainWordIconLibrary.asset`
   and test it inside the real WordButton at the target phone resolution.

## Test concepts

`Concepts/` contains Apple, Dog, and Guitar style tests generated with the
built-in image generation tool. They intentionally remain concepts because the
generator returned 1254x1254 RGB files with a baked checkerboard instead of
true alpha. Do not assign them to `MainWordIconLibrary.asset`.

The three concepts confirm that the style works for fruit, animals and long
thin objects. Guitar is near the upper detail limit; future prompts should ask
for simplified strings/frets if it loses clarity at 64px.
