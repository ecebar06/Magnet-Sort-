using System;
using System.Collections.Generic;

[Serializable]
public class WordItem
{
    public string text;
    public bool hasSprite;
    public string spriteKey;
    public string syllablePartA;
    public string syllablePartB;
}

[Serializable]
public class Category
{
    public string id;
    public string name;
    public List<WordItem> words;
    public bool transformsOnComplete;
    public WordItem transformResult;
    public string transformResultCategoryId;
}

[Serializable]
public class WordLibrary
{
    public string id;
    public List<Category> categories;
}

[Serializable]
public class GameLevel
{
    public string id;
    public int moveCount;
    public List<Category> categories;
}

[Serializable]
public class LevelWordEntry
{
    public string categoryId;
    public WordItem word;
}
