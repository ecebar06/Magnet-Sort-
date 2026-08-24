const blankWord = text => ({ text, hasSprite: false, useIcon: false, spriteKey: "", syllablePartA: "", syllablePartB: "" });
const clone = value => JSON.parse(JSON.stringify(value));

const state = {
  library: { id: "", categories: [] },
  level: { levelNumber: 1, moveCount: 35, expectedCategoryCount: 8, visibleRowCount: 4, categories: [], orderedWords: [] },
  dragIndex: -1,
  dataDirectoryHandle: null,
  levelDirectoryHandle: null,
  wordLibraryFileHandle: null,
  levelFiles: [],
  currentLevelFileHandle: null,
  currentLevelFileName: "",
  dirty: false
};

const $ = id => document.getElementById(id);
const controls = ["levelNumber", "moveCount", "categoryCount", "rowCount"];

function normalizeWord(word) {
  return { text: word?.text || "", hasSprite: !!word?.hasSprite, useIcon: !!word?.useIcon, spriteKey: word?.spriteKey || "", syllablePartA: word?.syllablePartA || "", syllablePartB: word?.syllablePartB || "" };
}

function normalizeCategory(category) {
  return {
    id: category.id || slug(category.name), name: category.name || category.id || "Category",
    words: (category.words || []).map(normalizeWord), transformsOnComplete: !!category.transformsOnComplete,
    transformResult: normalizeWord(category.transformResult), transformResultCategoryId: category.transformResultCategoryId || ""
  };
}

function normalizeLevel(data) {
  return {
    levelNumber: Math.max(1, +data?.levelNumber || 1),
    moveCount: Math.max(1, +data?.moveCount || 12),
    expectedCategoryCount: Math.max(1, +data?.expectedCategoryCount || data?.categories?.length || 1),
    visibleRowCount: Math.max(1, +data?.visibleRowCount || data?.categories?.length || 1),
    categories: (data?.categories || []).map(normalizeCategory),
    orderedWords: (data?.orderedWords || []).map(entry => ({ categoryId: entry.categoryId || "", word: normalizeWord(entry.word) }))
  };
}

function slug(value) { return String(value || "category").trim().toLowerCase().replace(/[^a-z0-9]+/g, "_").replace(/^_|_$/g, ""); }

function syncInputs() {
  $("levelNumber").value = state.level.levelNumber;
  $("moveCount").value = state.level.moveCount;
  $("categoryCount").value = state.level.expectedCategoryCount;
  $("rowCount").value = state.level.visibleRowCount;
}

function readInputs() {
  state.level.levelNumber = Math.max(1, +$("levelNumber").value || 1);
  state.level.moveCount = Math.max(1, +$("moveCount").value || 1);
  state.level.expectedCategoryCount = Math.max(1, +$("categoryCount").value || 1);
  state.level.visibleRowCount = Math.max(1, +$("rowCount").value || 1);
  markDirty();
  render();
}

function markDirty() {
  state.dirty = true;
  updateFolderControls();
}

function markClean() {
  state.dirty = false;
  updateFolderControls();
}

function updateFolderControls() {
  const connected = !!state.levelDirectoryHandle;
  $("levelSelector").disabled = !connected || !state.levelFiles.length;
  $("saveLevel").disabled = !connected;
  $("saveLevel").textContent = state.dirty ? "Save Level *" : "Save Level";
  const swapTargets = state.levelFiles.filter(record => record.name !== state.currentLevelFileName);
  $("swapLevelSelector").disabled = !connected || !state.currentLevelFileName || !swapTargets.length;
  $("swapLevels").disabled = $("swapLevelSelector").disabled;
}

function recommendedMoveCount() {
  const categories = Math.max(1, +$("categoryCount").value || state.level.expectedCategoryCount || 1);
  const visibleRows = Math.max(1, +$("rowCount").value || state.level.visibleRowCount || 1);
  const transformations = Math.max(0, categories - visibleRows);
  return categories * 3 + transformations;
}

function renderRecommendedMoves() {
  const recommended = recommendedMoveCount();
  $("recommendedMoves").textContent = `Recommended Moves: ${recommended} (estimate)`;
  $("useRecommendedMoves").textContent = `Use ${recommended}`;
}

function readJsonFile(file, callback) {
  if (!file) return;
  const reader = new FileReader();
  reader.onload = () => { try { callback(JSON.parse(reader.result)); } catch { toast("This JSON file could not be read."); } };
  reader.readAsText(file);
}

function renderLibrary() {
  const query = $("librarySearch").value.trim().toLowerCase();
  const matches = state.library.categories.filter(c => `${c.name} ${c.id} ${(c.words || []).map(w => w.text).join(" ")}`.toLowerCase().includes(query));
  const categories = matches.slice(0, 200);
  const iconCount = state.library.categories.flatMap(category => category.words).filter(word => word.hasSprite).length;
  $("libraryCount").textContent = `${state.library.categories.length} categories · ${iconCount} icons`;
  $("libraryEmpty").hidden = state.library.categories.length > 0;
  $("libraryList").innerHTML = (matches.length > 200 ? `<p class="word-preview">Showing the first 200 of ${matches.length} results. Search to narrow the list.</p>` : "") + categories.map(category => {
    const added = state.level.categories.some(c => c.id === category.id);
    const iconRows = category.words.map((word, wordIndex) => `<div class="icon-word-row">
      <span>${word.hasSprite ? "◆ " : ""}${escapeHtml(word.text)}${renderUsedBefore(word.text)}</span>
      <label><input type="checkbox" data-library-icon="${escapeAttr(category.id)}" data-library-word-index="${wordIndex}" ${word.hasSprite ? "checked" : ""}> Has Sprite</label>
      <input type="text" data-library-sprite-key="${escapeAttr(category.id)}" data-library-word-index="${wordIndex}" value="${escapeAttr(word.spriteKey)}" placeholder="sprite key" ${word.hasSprite ? "" : "disabled"}>
    </div>`).join("");
    return `<article class="library-category"><header><div><strong>${escapeHtml(category.name)}</strong><br><small>${escapeHtml(category.id)}</small></div><button class="mini-button" data-add="${escapeAttr(category.id)}" ${added ? "disabled" : ""}>${added ? "Added" : "Add"}</button></header><div class="word-preview-list">${category.words.map(word => `<span>${word.hasSprite ? "◆ " : ""}${escapeHtml(word.text)}${renderUsedBefore(word.text)}</span>`).join("")}</div><details class="icon-settings"><summary>Icon word settings</summary>${iconRows}</details></article>`;
  }).join("");
}

function previousLevelNumbersForWord(wordText) {
  const currentNumber = Math.max(1, +state.level.levelNumber || 1);
  const wanted = wordText.toLowerCase();
  return state.levelFiles
    .filter(record => record.levelNumber < currentNumber)
    .filter(record => (record.data.categories || []).some(category =>
      (category.words || []).some(word => (word.text || "").toLowerCase() === wanted) ||
      (category.transformsOnComplete && (category.transformResult?.text || "").toLowerCase() === wanted)))
    .map(record => record.levelNumber)
    .filter((number, index, numbers) => numbers.indexOf(number) === index)
    .sort((a, b) => a - b);
}

function renderUsedBefore(wordText) {
  const levels = previousLevelNumbersForWord(wordText);
  return levels.length ? `<small class="used-before">Used before: L${levels.join(", L")}</small>` : "";
}

function syncLibraryWord(categoryId, wordIndex) {
  const libraryCategory = state.library.categories.find(category => category.id === categoryId);
  const source = libraryCategory?.words[wordIndex];
  if (!source) return;
  const selectedCategory = state.level.categories.find(category => category.id === categoryId);
  const selectedWord = selectedCategory?.words.find(word => word.text === source.text);
  if (selectedWord) syncWordMetadata(selectedWord, source);
  for (const entry of state.level.orderedWords)
    if (entry.categoryId === categoryId && entry.word.text === source.text) syncWordMetadata(entry.word, source);
  for (const category of state.level.categories)
    if (category.transformResultCategoryId === categoryId && category.transformResult?.text === source.text)
      syncWordMetadata(category.transformResult, source);
}

function syncWordMetadata(levelWord, libraryWord) {
  const useIcon = !!levelWord.useIcon;
  levelWord.hasSprite = !!libraryWord.hasSprite;
  levelWord.spriteKey = libraryWord.spriteKey || "";
  levelWord.syllablePartA = libraryWord.syllablePartA || "";
  levelWord.syllablePartB = libraryWord.syllablePartB || "";
  levelWord.useIcon = useIcon;
}

function renderSelected() {
  $("selectedCount").textContent = `${state.level.categories.length} / ${state.level.expectedCategoryCount}`;
  if (!state.level.categories.length) {
    $("selectedCategories").innerHTML = `<div class="empty-state"><strong>No categories yet</strong><p>Add categories from the library on the left.</p></div>`;
    return;
  }
  $("selectedCategories").innerHTML = state.level.categories.map((category, index) => {
    const target = state.level.categories.find(item => item.id === category.transformResultCategoryId);
    const targetOptions = state.level.categories.map(item => `<option value="${escapeAttr(item.id)}" ${item.id === category.transformResultCategoryId ? "selected" : ""}>${escapeHtml(item.name)}</option>`).join("");
    const wordOptions = (target?.words || []).map(word => `<option value="${escapeAttr(word.text)}" ${word.text.toLowerCase() === category.transformResult.text.toLowerCase() ? "selected" : ""}>${escapeHtml(word.text)}</option>`).join("");
    const transformControls = category.transformsOnComplete
      ? `<div class="transform-row"><select data-target="${index}"><option value="">Target category…</option>${targetOptions}</select><select data-result="${index}" ${target ? "" : "disabled"}><option value="">Result word…</option>${wordOptions}</select></div>`
      : "";
    return `<article class="selected-card"><header><h4>${index + 1}. ${escapeHtml(category.name)}</h4><button class="mini-button remove" data-remove="${index}">Remove</button></header>
      <div class="card-body"><div class="chips">${category.words.map((word, wordIndex) => `<button type="button" class="chip icon-choice ${word.useIcon ? "selected" : ""}" data-level-icon-category="${escapeAttr(category.id)}" data-level-icon-word-index="${wordIndex}" title="${word.useIcon ? "Use text in this level" : "Use icon in this level"}">${word.useIcon ? "◆ " : ""}${escapeHtml(word.text)}</button>`).join("")}</div>
        <p class="icon-choice-hint">Click a word to toggle icon use for this level.</p>
        <label class="transform-toggle"><input type="checkbox" data-transform="${index}" ${category.transformsOnComplete ? "checked" : ""}> Transform when completed</label>
        ${transformControls}
      </div></article>`;
  }).join("");
}

function renderOrder() {
  const visibleCount = Math.min(state.level.orderedWords.length, state.level.visibleRowCount * 4);
  $("orderCount").textContent = `${state.level.orderedWords.length} words`;
  drawWords("visibleOrder", state.level.orderedWords.slice(0, visibleCount), 0);
  drawWords("queueOrder", state.level.orderedWords.slice(visibleCount), visibleCount);
}

function drawWords(containerId, words, offset) {
  $(containerId).innerHTML = words.length ? words.map((entry, i) => `<div class="word-card" draggable="true" data-word-index="${offset + i}" title="${escapeAttr(entry.categoryId)}">${entry.word.useIcon ? "◆ " : ""}${escapeHtml(entry.word.text)}</div>`).join("") : `<div class="empty-state" style="grid-column:1/-1"><p>Nothing generated yet.</p></div>`;
}

function validate() {
  const errors = [];
  if (state.level.categories.length !== state.level.expectedCategoryCount) errors.push(`Expected ${state.level.expectedCategoryCount} categories, currently ${state.level.categories.length}.`);
  if (state.level.categories.some(c => c.words.length !== 4)) errors.push("Every category must contain exactly 4 words.");
  const categoryIds = state.level.categories.map(category => category.id.toLowerCase());
  if (new Set(categoryIds).size !== categoryIds.length) errors.push("A category ID is repeated inside the level.");
  const wordLocations = new Map();
  for (const category of state.level.categories) for (const word of category.words) {
    const key = word.text.trim().toLowerCase();
    if (!wordLocations.has(key)) wordLocations.set(key, { text: word.text.trim(), categories: [] });
    wordLocations.get(key).categories.push(category.name);
  }
  const repeatedWord = [...wordLocations.values()].find(item => item.categories.length > 1);
  if (repeatedWord) {
    const categoryNames = [...new Set(repeatedWord.categories)];
    errors.push(categoryNames.length > 1
      ? `The word “${repeatedWord.text}” exists in both “${categoryNames[0]}” and “${categoryNames[1]}” categories.`
      : `The word “${repeatedWord.text}” appears more than once in “${categoryNames[0]}”.`);
  }
  for (const c of state.level.categories.filter(c => c.transformsOnComplete)) {
    if (!c.transformResult.text || !c.transformResultCategoryId) errors.push(`${c.name} needs a result and target category.`);
    else {
      const target = state.level.categories.find(x => x.id === c.transformResultCategoryId);
      if (!target || !target.words.some(w => w.text.toLowerCase() === c.transformResult.text.toLowerCase())) errors.push(`${c.transformResult.text || "Result"} must exist in its target category.`);
    }
  }
  const transformingById = new Map(state.level.categories
    .filter(category => category.transformsOnComplete)
    .map(category => [category.id, category]));
  let transformationCycle = null;
  for (const start of transformingById.values()) {
    const path = [];
    let current = start;
    while (current) {
      const cycleStart = path.findIndex(category => category.id === current.id);
      if (cycleStart >= 0) {
        transformationCycle = [...path.slice(cycleStart), current];
        break;
      }
      path.push(current);
      current = transformingById.get(current.transformResultCategoryId);
    }
    if (transformationCycle) break;
  }
  if (transformationCycle)
    errors.push(`Transformation cycle: ${transformationCycle.map(category => category.name).join(" → ")}. These categories wait for each other and cannot be completed.`);
  const generated = new Set(state.level.categories.filter(c => c.transformsOnComplete).map(c => c.transformResult.text.toLowerCase()));
  const playable = state.level.categories.flatMap(c => c.words).filter(w => !generated.has(w.text.toLowerCase())).length;
  const transforms = state.level.categories.filter(c => c.transformsOnComplete).length;
  const requiredTransforms = state.level.categories.length - state.level.visibleRowCount;
  if (requiredTransforms < 0)
    errors.push(`Visible Rows (${state.level.visibleRowCount}) cannot exceed the number of categories (${state.level.categories.length}).`);
  else if (transforms !== requiredTransforms)
    errors.push(`${state.level.categories.length} categories with ${state.level.visibleRowCount} visible rows require exactly ${requiredTransforms} transformations. Currently ${transforms} are set.`);
  const queue = playable - state.level.visibleRowCount * 4;
  if (queue !== transforms * 3) {
    if (transforms === 0 && queue > 0)
      errors.push(`${queue} words would remain in the queue, but this level has no transformations to bring them onto the board. Increase Visible Rows to ${playable / 4} or add transformations.`);
    else
      errors.push(`Counts create ${queue} queued words; ${transforms} transformations require exactly ${transforms * 3}.`);
  }
  if (state.level.orderedWords.length && state.level.orderedWords.length !== playable) errors.push(`Generated order contains ${state.level.orderedWords.length}/${playable} playable words.`);
  $("validationBadge").className = `status ${errors.length ? "bad" : "good"}`;
  $("validationBadge").textContent = errors.length ? `${errors.length} issue${errors.length > 1 ? "s" : ""}` : "Ready";
  $("validationMessage").className = `validation-message ${errors.length ? "error" : "success"}`;
  $("validationMessage").textContent = errors.length
    ? errors.map((message, index) => `${index + 1}. ${message}`).join("\n")
    : `✓ Valid setup: ${state.level.categories.length} categories − ${state.level.visibleRowCount} visible rows = ${transforms} transformations.`;
  return errors;
}

function generateOrder() {
  if (validate().length) return toast("Fix the level warnings before generating an order.");
  // A one-transform tutorial can legitimately need all three companion words
  // in its only refill (Dog Breeds -> Dog + Cat/Bird/Fish, for example).
  // Try the mixed generator first, then use that safe special case.
  const order = buildBalancedSolvableOrder() || buildSingleTransformTutorialOrder();
  if (!order) return toast("Could not build a mixed, solvable order. Check the transformation links and try again.");
  const { visible, queue } = order;
  const expectedVisible = state.level.visibleRowCount * 4;
  const expectedQueue = state.level.categories.filter(category => category.transformsOnComplete).length * 3;
  if (visible.length !== expectedVisible || queue.length !== expectedQueue)
    return toast(`Could not create a complete order (${visible.length}/${expectedVisible} visible, ${queue.length}/${expectedQueue} queued).`);
  state.level.orderedWords = [...visible, ...queue];
  markDirty();
  render(); toast(`Generated ${state.level.orderedWords.length} mixed, solvable words.`);
}

function buildSingleTransformTutorialOrder() {
  const transforms = state.level.categories.filter(category => category.transformsOnComplete);
  if (transforms.length !== 1) return null;

  const source = transforms[0];
  const targetId = source.transformResultCategoryId;
  const target = state.level.categories.find(category => category.id === targetId);
  if (!target) return null;

  const resultText = source.transformResult.text.toLowerCase();
  const sourceWords = source.words.filter(word => word.text.toLowerCase() !== resultText);
  const companionWords = target.words.filter(word => word.text.toLowerCase() !== resultText);
  if (sourceWords.length !== 4 || companionWords.length !== 3) return null;

  const entry = (category, word) => ({ categoryId: category.id, word: clone(word) });
  const visible = state.level.categories
    .filter(category => category.id !== targetId)
    .flatMap(category => category.words
      .filter(word => word.text.toLowerCase() !== resultText)
      .map(word => entry(category, word)));
  const queue = companionWords.map(word => entry(target, word));

  if (visible.length !== state.level.visibleRowCount * 4 || queue.length !== 3) return null;
  const mixedVisible = interleaveByCategory(visible);
  return isOrderSolvable(mixedVisible, queue, transforms) ? { visible: mixedVisible, queue } : null;
}

// A transform replaces four words with one result and exactly three queued words.
// Keep each refill mixed, but pre-load enough of the next source category that it
// can still be completed after the refill. The simulation below verifies the plan.
function buildBalancedSolvableOrder() {
  const categories = state.level.categories;
  const transforms = categories.filter(category => category.transformsOnComplete);
  const entry = (category, word) => ({ categoryId: category.id, word: clone(word) });
  if (!transforms.length) {
    return { visible: interleaveByCategory(categories.flatMap(category => category.words.map(word => entry(category, word)))), queue: [] };
  }

  const transformOrder = getTransformCompletionOrder(transforms);
  if (!transformOrder.length) return null;
  const generatedWords = new Set(transforms.map(category => category.transformResult.text.toLowerCase()));
  const baseWords = new Map(categories.map(category => [
    category.id,
    category.words.filter(word => !generatedWords.has(word.text.toLowerCase()))
  ]));
  const incomingCount = new Map(categories.map(category => [
    category.id,
    transforms.filter(source => source.transformResultCategoryId === category.id).length
  ]));
  const transformIds = new Set(transforms.map(category => category.id));
  const initialCounts = new Map(categories.map(category => [category.id, 0]));
  const root = transformOrder[0];
  const rootWords = baseWords.get(root.id) || [];
  if (rootWords.length !== 4) return null;
  initialCounts.set(root.id, rootWords.length);

  // Every later source is deliberately one base word short. That one word is
  // delivered in a mixed refill after its dependencies have produced their result.
  for (const category of transformOrder.slice(1)) {
    const words = baseWords.get(category.id) || [];
    if (!words.length) return null;
    initialCounts.set(category.id, Math.max(0, words.length - 1));
  }

  const visibleSlots = state.level.visibleRowCount * 4;
  let slotsLeft = visibleSlots - [...initialCounts.values()].reduce((sum, count) => sum + count, 0);
  if (slotsLeft < 0) return null;

  // Fill remaining visible slots one at a time across categories. Result targets
  // are capped at two visible base words, so their three companions never appear
  // as an obvious ready-made set when a transformation happens.
  const fillVisible = relaxTargetCap => {
    let added = true;
    while (slotsLeft > 0 && added) {
      added = false;
      for (const category of categories) {
        const words = baseWords.get(category.id) || [];
        const current = initialCounts.get(category.id) || 0;
        const isTransformSource = transformIds.has(category.id);
        const isResultTarget = (incomingCount.get(category.id) || 0) > 0;
        const cap = isTransformSource
          ? current
          : isResultTarget && !relaxTargetCap
            ? Math.min(words.length, 2)
            : words.length;
        if (slotsLeft > 0 && current < cap) {
          initialCounts.set(category.id, current + 1);
          slotsLeft--;
          added = true;
        }
      }
    }
  };
  fillVisible(false);
  fillVisible(true);
  if (slotsLeft !== 0) return null;

  const visible = [];
  const queuedWordsByCategory = new Map();
  for (const category of categories) {
    const words = baseWords.get(category.id) || [];
    const initialCount = initialCounts.get(category.id) || 0;
    visible.push(...words.slice(0, initialCount).map(word => entry(category, word)));
    queuedWordsByCategory.set(category.id, words.slice(initialCount).map(word => entry(category, word)));
  }

  const queueCount = [...queuedWordsByCategory.values()].reduce((sum, words) => sum + words.length, 0);
  if (queueCount !== transforms.length * 3) return null;
  const requiredCategoryIds = transformOrder.map((category, index) => index < transformOrder.length - 1 ? transformOrder[index + 1].id : "");
  const forbiddenCategoryIds = transformOrder.map(category => category.transformResultCategoryId || "");
  const batches = createMixedQueueBatches(queuedWordsByCategory, requiredCategoryIds, forbiddenCategoryIds);
  if (!batches) return null;
  const queue = batches.flat();
  const mixedVisible = interleaveByCategory(visible);
  return isOrderSolvable(mixedVisible, queue, transforms) ? { visible: mixedVisible, queue } : null;
}

function getTransformCompletionOrder(transforms) {
  const remaining = [...transforms];
  const completed = new Set();
  const order = [];
  while (remaining.length) {
    const nextIndex = remaining.findIndex(category => transforms
      .filter(source => source.transformResultCategoryId === category.id)
      .every(source => completed.has(source.id)));
    if (nextIndex < 0) return [];
    const [next] = remaining.splice(nextIndex, 1);
    order.push(next);
    completed.add(next.id);
  }
  return order;
}

function createMixedQueueBatches(queuedWordsByCategory, requiredCategoryIds, forbiddenCategoryIds) {
  const categoryIds = [...queuedWordsByCategory.keys()];
  const counts = new Map(categoryIds.map(id => [id, (queuedWordsByCategory.get(id) || []).length]));
  const memo = new Map();
  const search = step => {
    if (step === requiredCategoryIds.length)
      return [...counts.values()].every(count => count === 0) ? [] : null;
    const memoKey = `${step}|${categoryIds.map(id => counts.get(id)).join(",")}`;
    if (memo.has(memoKey)) return null;
    const required = requiredCategoryIds[step];
    const forbidden = forbiddenCategoryIds[step];
    const available = categoryIds.filter(id => (counts.get(id) || 0) > 0)
      .sort((a, b) => (counts.get(b) || 0) - (counts.get(a) || 0));
    for (let first = 0; first < available.length; first++) for (let second = first + 1; second < available.length; second++) for (let third = second + 1; third < available.length; third++) {
      const batchIds = [available[first], available[second], available[third]];
      if (required && !batchIds.includes(required)) continue;
      if (forbidden && forbidden !== required && batchIds.includes(forbidden)) continue;
      batchIds.forEach(id => counts.set(id, counts.get(id) - 1));
      const remainingBatches = requiredCategoryIds.length - step - 1;
      const canFinish = [...counts.values()].every(count => count <= remainingBatches);
      const tail = canFinish ? search(step + 1) : null;
      batchIds.forEach(id => counts.set(id, counts.get(id) + 1));
      if (tail) return [batchIds, ...tail];
    }
    memo.set(memoKey, true);
    return null;
  };
  const batchIds = search(0);
  if (!batchIds) return null;
  const remainingWords = new Map(categoryIds.map(id => [id, [...(queuedWordsByCategory.get(id) || [])]]));
  return batchIds.map(batch => batch.map(id => remainingWords.get(id).shift()));
}

function isOrderSolvable(visible, queue, transforms) {
  const categoriesById = new Map(state.level.categories.map(category => [category.id, category]));
  const wordKey = (categoryId, wordText) => `${categoryId}::${wordText.toLowerCase()}`;
  const initialActive = new Set(visible.map(item => wordKey(item.categoryId, item.word.text)));
  const memo = new Set();
  const search = (step, active, completed) => {
    if (step === transforms.length) return true;
    const memoKey = `${step}|${[...completed].sort().join(",")}`;
    if (memo.has(memoKey)) return false;
    for (const category of transforms) {
      if (completed.has(category.id)) continue;
      const isReady = category.words.every(word => active.has(wordKey(category.id, word.text)));
      if (!isReady) continue;
      const nextActive = new Set(active);
      category.words.forEach(word => nextActive.delete(wordKey(category.id, word.text)));
      nextActive.add(wordKey(category.transformResultCategoryId, category.transformResult.text));
      queue.slice(step * 3, step * 3 + 3).forEach(item => nextActive.add(wordKey(item.categoryId, item.word.text)));
      const nextCompleted = new Set(completed);
      nextCompleted.add(category.id);
      if (search(step + 1, nextActive, nextCompleted)) return true;
    }
    memo.add(memoKey);
    return false;
  };
  return search(0, initialActive, new Set());
}

function interleaveByCategory(entries) {
  const groups = new Map();
  for (const entry of entries) {
    if (!groups.has(entry.categoryId)) groups.set(entry.categoryId, []);
    groups.get(entry.categoryId).push(entry);
  }
  const queues = [...groups.values()];
  const result = [];
  while (queues.some(queue => queue.length)) for (const queue of queues) if (queue.length) result.push(queue.shift());
  return result;
}

function addCategory(id) {
  const source = state.library.categories.find(c => c.id === id);
  if (!source || state.level.categories.some(c => c.id === id)) return;
  state.level.categories.push(clone(source)); state.level.orderedWords = []; markDirty(); render();
}

function autoSetTransformations() {
  const required = state.level.categories.length - state.level.visibleRowCount;
  if (required < 0) return toast("Visible Rows cannot exceed the category count.");
  if (state.level.categories.some(category => category.words.length !== 4))
    return toast("Every category must contain exactly 4 words first.");
  if (state.level.categories.some(category => category.transformsOnComplete) &&
      !confirm("Replace the current transformation setup with an automatic cycle-free chain?")) return;

  const requiredCount = applyAutomaticTransformations();
  markDirty();
  render();
  toast(requiredCount
    ? `Added ${requiredCount} cycle-free transformations. Review them, then generate the order.`
    : "This level does not need transformations.");
}

function applyAutomaticTransformations() {
  const required = state.level.categories.length - state.level.visibleRowCount;
  for (const category of state.level.categories) {
    category.transformsOnComplete = false;
    category.transformResult = normalizeWord();
    category.transformResultCategoryId = "";
  }
  for (let index = 0; index < required; index++) {
    const source = state.level.categories[index];
    const target = state.level.categories[index + 1];
    source.transformsOnComplete = true;
    source.transformResultCategoryId = target.id;
    source.transformResult = clone(target.words[0]);
  }

  state.level.orderedWords = [];
  return required;
}

function autoBuildLevel() {
  const desiredCount = Math.max(1, +$("categoryCount").value || 1);
  const visibleRows = Math.max(1, +$("rowCount").value || 1);
  if (!state.library.categories.length) return toast("Import WordLibrary.json first.");
  if (visibleRows > desiredCount) return toast("Visible Rows cannot exceed Category Count.");
  if (state.level.categories.length && !confirm("Replace the current categories, transformations and generated order?")) return;

  state.level.levelNumber = Math.max(1, +$("levelNumber").value || 1);
  state.level.moveCount = Math.max(1, +$("moveCount").value || 1);
  state.level.expectedCategoryCount = desiredCount;
  state.level.visibleRowCount = visibleRows;

  const candidates = state.library.categories.filter(category => {
    const words = category.words.map(word => word.text.trim().toLowerCase());
    return words.length === 4 && words.every(Boolean) && new Set(words).size === 4;
  });
  const selected = [];
  const usedWords = new Set();
  const startIndex = candidates.length ? ((state.level.levelNumber - 1) * desiredCount) % candidates.length : 0;
  for (let step = 0; step < candidates.length && selected.length < desiredCount; step++) {
    const candidate = candidates[(startIndex + step) % candidates.length];
    const words = candidate.words.map(word => word.text.trim().toLowerCase());
    if (words.some(word => usedWords.has(word))) continue;
    selected.push(clone(candidate));
    words.forEach(word => usedWords.add(word));
  }
  if (selected.length !== desiredCount)
    return toast(`Could only find ${selected.length}/${desiredCount} categories without repeated words.`);

  state.level.categories = selected;
  applyAutomaticTransformations();
  markDirty();
  syncInputs();
  render();
  generateOrder();
}

async function openLevelsFolder() {
  if (!("showDirectoryPicker" in window)) {
    setFolderStatus("Direct folder access is not supported here. Open the editor in current Chrome or Edge.", "error");
    return;
  }
  if (state.dirty && !confirm("Discard the unsaved changes and open another Unity Data folder?")) return;
  try {
    const handle = await window.showDirectoryPicker({ id: "word-sort-data", mode: "readwrite" });
    const selectedName = handle.name.toLowerCase();
    if (selectedName !== "data" && selectedName !== "levels" &&
        !confirm(`You selected “${handle.name}”. The expected folder is Assets/_Game/Resources/Data. Use it anyway?`)) return;

    state.dataDirectoryHandle = selectedName === "data" ? handle : null;
    state.levelDirectoryHandle = selectedName === "levels"
      ? handle
      : await handle.getDirectoryHandle("Levels", { create: true });
    state.wordLibraryFileHandle = null;
    if (state.dataDirectoryHandle) {
      try {
        state.wordLibraryFileHandle = await state.dataDirectoryHandle.getFileHandle("WordLibrary.json");
        await loadConnectedWordLibrary();
      } catch {
        setFolderStatus("Levels connected, but WordLibrary.json was not found in the selected Data folder.", "error");
      }
    }
    state.currentLevelFileHandle = null;
    state.currentLevelFileName = "";
    await refreshLevelFiles(true);
  } catch (error) {
    if (error?.name !== "AbortError") setFolderStatus(`Could not open folder: ${error.message || error}`, "error");
  }
}

async function loadConnectedWordLibrary() {
  if (!state.wordLibraryFileHandle) return;
  const file = await state.wordLibraryFileHandle.getFile();
  const data = JSON.parse(await file.text());
  state.library = {
    id: data.id || "word_library",
    categories: (data.categories || []).map(normalizeCategory)
  };
}

async function saveConnectedWordLibrary() {
  if (!state.wordLibraryFileHandle || !state.library.categories.length) return false;
  const writable = await state.wordLibraryFileHandle.createWritable();
  await writable.write(serializeWordLibrary());
  await writable.close();
  return true;
}

function serializeWordLibrary() {
  const stored = clone(state.library);
  for (const category of stored.categories || []) {
    for (const word of category.words || []) delete word.useIcon;
    if (category.transformResult) delete category.transformResult.useIcon;
  }
  return JSON.stringify(stored, null, 4) + "\n";
}

async function refreshLevelFiles(loadFirst = false, preferredName = "") {
  if (!state.levelDirectoryHandle) return;
  const records = [];
  let skipped = 0;
  for await (const [name, handle] of state.levelDirectoryHandle.entries()) {
    if (handle.kind !== "file" || !/^Level_.*\.json$/i.test(name)) continue;
    try {
      const file = await handle.getFile();
      const data = JSON.parse(await file.text());
      if (!data || !Array.isArray(data.categories) || (+data.levelNumber || 0) < 1) throw new Error("Invalid level structure");
      records.push({ name, handle, data, levelNumber: +data.levelNumber });
    } catch {
      skipped++;
    }
  }
  records.sort((a, b) => a.levelNumber - b.levelNumber || a.name.localeCompare(b.name));
  state.levelFiles = records;
  renderLevelFileOptions();
  setFolderStatus(
    `Connected: ${records.length} level${records.length === 1 ? "" : "s"}${state.wordLibraryFileHandle ? " + WordLibrary.json" : " (WordLibrary not connected)"}${skipped ? `, ${skipped} invalid JSON skipped` : ""}.`,
    skipped ? "error" : "connected"
  );

  const wanted = records.find(record => record.name === preferredName) ||
    records.find(record => record.name === state.currentLevelFileName);
  if (wanted) loadLevelRecord(wanted);
  else if (loadFirst && records.length) loadLevelRecord(records[0]);
  else updateFolderControls();
}

function renderLevelFileOptions() {
  const selector = $("levelSelector");
  selector.innerHTML = state.levelFiles.length
    ? state.levelFiles.map(record => `<option value="${escapeAttr(record.name)}">Level ${record.levelNumber} · ${escapeHtml(record.name)}</option>`).join("")
    : `<option value="">No JSON levels in folder</option>`;
  if (state.currentLevelFileName && state.levelFiles.some(record => record.name === state.currentLevelFileName))
    selector.value = state.currentLevelFileName;
  const swapSelector = $("swapLevelSelector");
  const swapTargets = state.levelFiles.filter(record => record.name !== state.currentLevelFileName);
  swapSelector.innerHTML = `<option value="">Swap with…</option>` + swapTargets
    .map(record => `<option value="${escapeAttr(record.name)}">Level ${record.levelNumber}</option>`).join("");
  updateFolderControls();
}

function loadLevelRecord(record) {
  state.level = normalizeLevel(record.data);
  state.currentLevelFileHandle = record.handle;
  state.currentLevelFileName = record.name;
  $("levelSelector").value = record.name;
  syncInputs();
  render();
  markClean();
  toast(`Level ${state.level.levelNumber} loaded from Unity folder.`);
}

function createNewLevel() {
  if (state.dirty && !confirm("Discard the unsaved changes and create a new level?")) return;
  const nextNumber = state.levelFiles.reduce((maximum, record) => Math.max(maximum, record.levelNumber), 0) + 1;
  state.level = normalizeLevel({ levelNumber: nextNumber || 1, moveCount: 35, expectedCategoryCount: 8, visibleRowCount: 4, categories: [], orderedWords: [] });
  state.currentLevelFileHandle = null;
  state.currentLevelFileName = "";
  syncInputs();
  render();
  markDirty();
  $("levelSelector").value = "";
  toast(`New Level ${state.level.levelNumber} prepared. Save it to add it to the Unity folder.`);
}

async function saveLevelDirectly() {
  if (!state.levelDirectoryHandle) return toast("Open the Unity Data folder first.");
  const errors = validate();
  if ((errors.length || !state.level.orderedWords.length) &&
      !confirm("This level still has warnings or no generated order. Save the draft anyway?")) return;

  try {
    let handle = state.currentLevelFileHandle;
    let fileName = state.currentLevelFileName;
    if (!handle) {
      fileName = `Level_${String(state.level.levelNumber).padStart(3, "0")}.json`;
      const existing = state.levelFiles.find(record => record.name.toLowerCase() === fileName.toLowerCase());
      if (existing && !confirm(`${fileName} already exists. Replace it?`)) return;
      handle = await state.levelDirectoryHandle.getFileHandle(fileName, { create: true });
    }
    const writable = await handle.createWritable();
    await writable.write(JSON.stringify(state.level, null, 2) + "\n");
    await writable.close();
    await saveConnectedWordLibrary();
    state.currentLevelFileHandle = handle;
    state.currentLevelFileName = fileName;
    markClean();
    await refreshLevelFiles(false, fileName);
    toast(`${fileName}${state.wordLibraryFileHandle ? " and WordLibrary.json" : ""} saved directly to Unity.`);
  } catch (error) {
    setFolderStatus(`Could not save level: ${error.message || error}`, "error");
    toast("Level could not be saved.");
  }
}

async function writeJsonFile(handle, data) {
  const writable = await handle.createWritable();
  await writable.write(JSON.stringify(data, null, 2) + "\n");
  await writable.close();
}

async function swapLevelsDirectly() {
  if (state.dirty) return toast("Save the current level before swapping levels.");
  const current = state.levelFiles.find(record => record.name === state.currentLevelFileName);
  const target = state.levelFiles.find(record => record.name === $("swapLevelSelector").value);
  if (!current || !target) return toast("Select another level to swap with.");
  if (!confirm(`Swap the complete contents of Level ${current.levelNumber} and Level ${target.levelNumber}?`)) return;

  const currentOriginal = clone(current.data);
  const targetOriginal = clone(target.data);
  const currentReplacement = normalizeLevel(targetOriginal);
  const targetReplacement = normalizeLevel(currentOriginal);
  currentReplacement.levelNumber = current.levelNumber;
  targetReplacement.levelNumber = target.levelNumber;

  try {
    await writeJsonFile(current.handle, currentReplacement);
    await writeJsonFile(target.handle, targetReplacement);
    await refreshLevelFiles(false, current.name);
    toast(`Level ${current.levelNumber} and Level ${target.levelNumber} swapped.`);
  } catch (error) {
    try {
      await writeJsonFile(current.handle, currentOriginal);
      await writeJsonFile(target.handle, targetOriginal);
    } catch { /* Best-effort rollback; the original error is shown below. */ }
    setFolderStatus(`Could not swap levels: ${error.message || error}`, "error");
    toast("Levels could not be swapped.");
  }
}

function setFolderStatus(message, kind = "") {
  const status = $("folderStatus");
  status.textContent = message;
  status.className = `folder-status ${kind}`.trim();
}

function exportLibrary() {
  if (!state.library.categories.length) return toast("Import a WordLibrary.json file first.");
  const json = serializeWordLibrary();
  const blob = new Blob([json], { type: "application/json" });
  const link = document.createElement("a"); link.href = URL.createObjectURL(blob); link.download = "WordLibrary.json"; link.click(); URL.revokeObjectURL(link.href);
  toast("Updated WordLibrary.json downloaded.");
}

function render() { renderLibrary(); renderSelected(); renderOrder(); renderRecommendedMoves(); validate(); }
function toast(message) { const el = $("toast"); el.textContent = message; el.classList.add("show"); clearTimeout(toast.timer); toast.timer = setTimeout(() => el.classList.remove("show"), 2200); }
function escapeHtml(value) { return String(value ?? "").replace(/[&<>"']/g, c => ({"&":"&amp;","<":"&lt;",">":"&gt;",'"':"&quot;","'":"&#39;"}[c])); }
function escapeAttr(value) { return escapeHtml(value); }

$("libraryFile").addEventListener("change", event => readJsonFile(event.target.files[0], data => { state.library = { id: data.id || "word_library", categories: (data.categories || []).map(normalizeCategory) }; render(); toast("Word library loaded."); }));
$("openLevelsFolder").addEventListener("click", openLevelsFolder);
$("saveLevel").addEventListener("click", saveLevelDirectly);
$("swapLevels").addEventListener("click", swapLevelsDirectly);
$("levelSelector").addEventListener("change", event => {
  const record = state.levelFiles.find(item => item.name === event.target.value);
  if (!record) return;
  if (state.dirty && !confirm("Discard unsaved changes and open the selected level?")) {
    event.target.value = state.currentLevelFileName;
    return;
  }
  loadLevelRecord(record);
});
$("newLevel").addEventListener("click", createNewLevel);
$("createCategory").addEventListener("click", () => {
  const name = prompt("Category name:"); if (!name?.trim()) return;
  const words = prompt("Enter exactly 4 words, separated by commas:");
  const values = (words || "").split(",").map(value => value.trim()).filter(Boolean);
  if (values.length !== 4) return toast("A category must contain exactly 4 words.");
  let id = slug(name), suffix = 2;
  while (state.library.categories.some(category => category.id === id)) id = `${slug(name)}_${suffix++}`;
  const category = normalizeCategory({ id, name: name.trim(), words: values.map(blankWord) });
  state.library.categories.push(category); state.level.categories.push(clone(category)); markDirty(); render(); toast(`${name.trim()} added to this level.`);
});
$("exportLibrary").addEventListener("click", exportLibrary);
$("autoBuildLevel").addEventListener("click", autoBuildLevel);
$("autoTransformations").addEventListener("click", autoSetTransformations);
$("useRecommendedMoves").addEventListener("click", () => {
  const recommended = recommendedMoveCount();
  state.level.moveCount = recommended;
  $("moveCount").value = recommended;
  markDirty();
  render();
  toast(`Move Count set to the recommended value: ${recommended}.`);
});
$("generateOrder").addEventListener("click", generateOrder);
$("clearOrder").addEventListener("click", () => { state.level.orderedWords = []; markDirty(); render(); });
$("librarySearch").addEventListener("input", renderLibrary);
controls.forEach(id => $(id).addEventListener("change", readInputs));

document.addEventListener("click", event => {
  const add = event.target.closest("[data-add]"); if (add) addCategory(add.dataset.add);
  const remove = event.target.closest("[data-remove]"); if (remove) { state.level.categories.splice(+remove.dataset.remove, 1); state.level.orderedWords = []; markDirty(); render(); }
  const iconChoice = event.target.closest("[data-level-icon-category]");
  if (iconChoice) {
    const categoryId = iconChoice.dataset.levelIconCategory;
    const wordIndex = +iconChoice.dataset.levelIconWordIndex;
    const category = state.level.categories.find(item => item.id === categoryId);
    const word = category?.words[wordIndex];
    if (word) {
      setLevelWordIconUsage(categoryId, word.text, !word.useIcon);
      markDirty();
      render();
    }
  }
});

function setLevelWordIconUsage(categoryId, wordText, useIcon) {
  const levelCategory = state.level.categories.find(category => category.id === categoryId);
  let libraryCategory = state.library.categories.find(category => category.id === categoryId);
  if (!libraryCategory && levelCategory) {
    libraryCategory = normalizeCategory({
      id: levelCategory.id,
      name: levelCategory.name,
      words: levelCategory.words.map(word => ({ ...clone(word), useIcon: false }))
    });
    state.library.categories.push(libraryCategory);
  }
  let libraryWord = libraryCategory?.words.find(word => word.text.toLowerCase() === wordText.toLowerCase());
  if (!libraryWord && libraryCategory) {
    libraryWord = normalizeWord({ text: wordText });
    libraryCategory.words.push(libraryWord);
  }
  if (useIcon && libraryWord) {
    libraryWord.hasSprite = true;
    if (!libraryWord.spriteKey) libraryWord.spriteKey = `${categoryId}__${slug(libraryWord.text)}`;
    if (state.wordLibraryFileHandle)
      saveConnectedWordLibrary().catch(error => setFolderStatus(`Could not update WordLibrary.json: ${error.message || error}`, "error"));
  }

  const apply = word => {
    if (!word || word.text.toLowerCase() !== wordText.toLowerCase()) return;
    word.useIcon = useIcon;
    if (libraryWord) syncWordMetadata(word, libraryWord);
  };
  for (const category of state.level.categories) {
    if (category.id === categoryId) category.words.forEach(apply);
    if (category.transformResultCategoryId === categoryId) apply(category.transformResult);
  }
  for (const entry of state.level.orderedWords)
    if (entry.categoryId === categoryId) apply(entry.word);
}

document.addEventListener("change", event => {
  if (event.target.matches("[data-library-icon]")) {
    const categoryId = event.target.dataset.libraryIcon;
    const wordIndex = +event.target.dataset.libraryWordIndex;
    const category = state.library.categories.find(item => item.id === categoryId);
    const word = category?.words[wordIndex];
    if (word) {
      word.hasSprite = event.target.checked;
      if (word.hasSprite && !word.spriteKey) word.spriteKey = `${category.id}__${slug(word.text)}`;
      syncLibraryWord(categoryId, wordIndex);
      markDirty();
      render();
    }
  }
  if (event.target.matches("[data-library-sprite-key]")) {
    const categoryId = event.target.dataset.librarySpriteKey;
    const wordIndex = +event.target.dataset.libraryWordIndex;
    const category = state.library.categories.find(item => item.id === categoryId);
    const word = category?.words[wordIndex];
    if (word) {
      word.spriteKey = event.target.value.trim();
      syncLibraryWord(categoryId, wordIndex);
      markDirty();
      render();
    }
  }
  if (event.target.matches("[data-transform]")) { const c = state.level.categories[+event.target.dataset.transform]; c.transformsOnComplete = event.target.checked; if (!c.transformsOnComplete) { c.transformResult = normalizeWord(); c.transformResultCategoryId = ""; } state.level.orderedWords = []; markDirty(); render(); }
  if (event.target.matches("[data-result]")) {
    const c = state.level.categories[+event.target.dataset.result];
    const target = state.level.categories.find(item => item.id === c.transformResultCategoryId);
    c.transformResult = clone(target?.words.find(word => word.text === event.target.value) || normalizeWord());
    state.level.orderedWords = []; markDirty(); render();
  }
  if (event.target.matches("[data-target]")) {
    const c = state.level.categories[+event.target.dataset.target];
    c.transformResultCategoryId = event.target.value;
    const target = state.level.categories.find(item => item.id === event.target.value);
    c.transformResult = clone(target?.words[0] || normalizeWord());
    state.level.orderedWords = []; markDirty(); render();
  }
});

document.addEventListener("dragstart", event => { const card = event.target.closest("[data-word-index]"); if (!card) return; state.dragIndex = +card.dataset.wordIndex; card.classList.add("dragging"); event.dataTransfer.effectAllowed = "move"; });
document.addEventListener("dragend", event => { event.target.closest("[data-word-index]")?.classList.remove("dragging"); document.querySelectorAll(".drag-over").forEach(x => x.classList.remove("drag-over")); });
document.addEventListener("dragover", event => { const card = event.target.closest("[data-word-index]"); if (!card) return; event.preventDefault(); document.querySelectorAll(".drag-over").forEach(x => x.classList.remove("drag-over")); card.classList.add("drag-over"); });
document.addEventListener("drop", event => { const card = event.target.closest("[data-word-index]"); if (!card) return; event.preventDefault(); const target = +card.dataset.wordIndex; if (state.dragIndex >= 0 && target !== state.dragIndex) { [state.level.orderedWords[state.dragIndex], state.level.orderedWords[target]] = [state.level.orderedWords[target], state.level.orderedWords[state.dragIndex]]; markDirty(); } state.dragIndex = -1; render(); });

syncInputs(); render();
