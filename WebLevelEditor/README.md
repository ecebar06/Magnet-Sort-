# Word Sort Web Level Editor

Open `index.html` in current Chrome or Edge. The editor works locally and does not upload project data anywhere.

1. Click **Open Unity Levels Folder** and select `Assets/_Game/Resources/Data/Levels`.
2. Select any existing level from the level dropdown to edit it.
3. Import `Assets/_Game/Resources/Data/WordLibrary.json` when category browsing is needed.
4. Set level, move, category and visible-row counts; add categories and transformations.
5. Generate a solvable order and optionally drag words to swap them.
6. Click **Save Level**. Existing levels are updated in place; a new level is created directly in the Unity folder as `Level_XXX.json`.

The browser asks for folder permission because websites cannot access local project files without explicit approval. Export/import is not required for level files after the folder is connected.

