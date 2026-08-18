using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "MainWordIconLibrary", menuName = "Word Game/Word Icon Library")]
public class WordIconLibrary : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        public string key;
        public Sprite sprite;
    }

    [SerializeField] private List<Entry> icons = new List<Entry>();
    private Dictionary<string, Sprite> lookup;

    private void OnEnable()
    {
        RebuildLookup();
    }

    private void OnValidate()
    {
        RebuildLookup();
    }

    public bool TryGetIcon(string key, out Sprite sprite)
    {
        if (lookup == null) RebuildLookup();
        return lookup.TryGetValue(key ?? string.Empty, out sprite) && sprite != null;
    }

    public void SetIcon(string key, Sprite sprite)
    {
        if (string.IsNullOrWhiteSpace(key))
            return;

        Entry entry = icons.Find(item => item.key == key);
        if (entry == null)
        {
            entry = new Entry { key = key };
            icons.Add(entry);
        }

        entry.sprite = sprite;
        RebuildLookup();
    }

    public bool RemoveIcon(string key)
    {
        int removed = icons.RemoveAll(item => item != null && item.key == key);
        if (removed > 0) RebuildLookup();
        return removed > 0;
    }

    public void SynchronizeKeys(IEnumerable<string> validKeys)
    {
        HashSet<string> keys = new HashSet<string>(validKeys ?? Array.Empty<string>(), StringComparer.Ordinal);
        icons.RemoveAll(item => item == null || string.IsNullOrWhiteSpace(item.key) || !keys.Contains(item.key));

        foreach (string key in keys)
        {
            if (icons.Exists(item => item != null && item.key == key)) continue;
            icons.Add(new Entry { key = key, sprite = null });
        }

        RebuildLookup();
    }

    private void RebuildLookup()
    {
        lookup = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        foreach (Entry entry in icons)
        {
            if (entry == null || string.IsNullOrWhiteSpace(entry.key) || entry.sprite == null)
                continue;

            if (!lookup.ContainsKey(entry.key))
                lookup.Add(entry.key, entry.sprite);
        }
    }
}
