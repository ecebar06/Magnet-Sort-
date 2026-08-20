# Word Sort Web Level Editor

Open `index.html` in current Chrome or Edge. The editor works locally and does not upload project data anywhere.

1. Click **Open Unity Data Folder** and select `Assets/_Game/Resources/Data`.
2. Select any existing level from the level dropdown to edit it.
3. `WordLibrary.json` loads automatically from the selected Data folder.
4. Set level, move, category and visible-row counts; add categories and transformations.
5. Generate a solvable order and optionally drag words to swap them.
6. Click a word in **Level Categories** to toggle icon use for that specific level.
7. Click **Save Level**. Existing levels and WordLibrary icon metadata are updated in place; a new level is created in `Levels` as `Level_XXX.json`.

Selecting an icon automatically enables `hasSprite` and creates a sprite key in `WordLibrary.json` when needed. If no sprite is assigned in Unity, the game safely falls back to text.

The browser asks for folder permission because websites cannot access local project files without explicit approval. Export/import is not required after the Data folder is connected.

