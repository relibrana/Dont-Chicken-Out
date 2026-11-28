using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Testing aid, editor only (nov 2026): choose which items the capsules can
/// give, to try them one by one. A panel shows up on the lobby's Rules screen
/// (MatchConfigScreen) with one toggle per item of the pool, "Solo" to keep a
/// single one, and "Todos" to go back to normal. The choice is kept between
/// play sessions (EditorPrefs); while it filters anything, a yellow reminder
/// stays in the bottom-left corner of every scene so it is never forgotten.
///
/// With a filter on, the item is still rolled by its weight in the breaker's
/// third, among the allowed ones only; if none of those has weight there, it
/// is picked evenly among them (so moco/teleport can be tested anywhere).
///
/// In builds none of this exists: <see cref="Allows"/> is always true and
/// <see cref="Active"/> always false.
/// </summary>
public static class ItemTestFilter
{
#if UNITY_EDITOR
    private const string PrefsKey = "DCO.ItemTestFilter.Disabled";

    private static HashSet<string> _disabled;

    private static HashSet<string> Disabled
    {
        get
        {
            if (_disabled != null) return _disabled;
            _disabled = new HashSet<string>();
            string saved = EditorPrefs.GetString(PrefsKey, "");
            foreach (string name in saved.Split('|'))
                if (!string.IsNullOrEmpty(name)) _disabled.Add(name);
            return _disabled;
        }
    }

    /// <summary>True when some item is switched off.</summary>
    public static bool Active => Disabled.Count > 0;

    public static bool Allows(GameObject prefab) => prefab == null || !Disabled.Contains(prefab.name);

    public static void Set(GameObject prefab, bool allowed)
    {
        if (prefab == null) return;
        bool changed = allowed ? Disabled.Remove(prefab.name) : Disabled.Add(prefab.name);
        if (changed) Save();
    }

    public static void AllowAll()
    {
        Disabled.Clear();
        Save();
    }

    private static void Save() => EditorPrefs.SetString(PrefsKey, string.Join("|", Disabled));

    /// <summary>The project's item pool, straight from the assets (the lobby scene does not reference it).</summary>
    public static ItemsPoolSO FindPool()
    {
        string[] guids = AssetDatabase.FindAssets("t:ItemsPoolSO");
        return guids.Length > 0 ? AssetDatabase.LoadAssetAtPath<ItemsPoolSO>(AssetDatabase.GUIDToAssetPath(guids[0])) : null;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void CreatePanel()
    {
        var go = new GameObject(nameof(ItemTestFilterPanel));
        go.AddComponent<ItemTestFilterPanel>();
        Object.DontDestroyOnLoad(go);
    }
#else
    public static bool Active => false;
    public static bool Allows(GameObject prefab) => true;
#endif
}

#if UNITY_EDITOR
/// <summary>IMGUI panel for <see cref="ItemTestFilter"/>. Editor only, used with the mouse.</summary>
public sealed class ItemTestFilterPanel : MonoBehaviour
{
    private ItemsPoolSO _pool;
    private MatchConfigScreen _rules;
    private float _nextLookup;
    private Vector2 _scroll;

    private GUIStyle _title, _label, _toggle, _button, _reminder;

    private void Update()
    {
        // Cheap lookup a few times a second: the Rules screen only exists in the Lobby.
        if (Time.unscaledTime < _nextLookup) return;
        _nextLookup = Time.unscaledTime + 0.25f;
        _rules = FindAnyObjectByType<MatchConfigScreen>(FindObjectsInactive.Exclude);
        if (_pool == null) _pool = ItemTestFilter.FindPool();
    }

    private void OnGUI()
    {
        if (_pool == null || _pool.items == null) return;
        EnsureStyles();

        if (_rules != null && _rules.isActiveAndEnabled) DrawPanel();
        else if (ItemTestFilter.Active) DrawReminder();
    }

    private void DrawPanel()
    {
        float unit = Mathf.Max(18f, Screen.height / 40f);
        float width = unit * 13f;
        float rowH = unit * 1.3f;
        float height = Mathf.Min(Screen.height - unit * 2f, rowH * (_pool.items.Count + 3) + unit);
        var box = new Rect(Screen.width - width - unit, unit, width, height);

        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = Color.white;

        GUILayout.BeginArea(new Rect(box.x + unit * 0.5f, box.y + unit * 0.4f, box.width - unit, box.height - unit * 0.6f));
        GUILayout.Label("Ítems de prueba (solo editor)", _title);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Todos", _button)) ItemTestFilter.AllowAll();
        GUILayout.Label(ItemTestFilter.Active ? $"{AllowedCount()} de {_pool.items.Count}" : "Normal", _label);
        GUILayout.EndHorizontal();

        _scroll = GUILayout.BeginScrollView(_scroll);
        foreach (ItemData item in _pool.items)
        {
            if (item == null || item.prefab == null) continue;

            GUILayout.BeginHorizontal(GUILayout.Height(rowH));
            bool allowed = ItemTestFilter.Allows(item.prefab);
            bool now = GUILayout.Toggle(allowed, " " + item.prefab.name, _toggle);
            if (now != allowed) ItemTestFilter.Set(item.prefab, now);

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Solo", _button, GUILayout.Width(unit * 3f)))
                OnlyThis(item.prefab);
            GUILayout.EndHorizontal();
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawReminder()
    {
        var content = new GUIContent($"Ítems de prueba: {AllowedCount()} de {_pool.items.Count} (editor)");
        Vector2 size = _reminder.CalcSize(content);
        var rect = new Rect(8f, Screen.height - size.y - 8f, size.x + 12f, size.y + 4f);

        GUI.color = new Color(0f, 0f, 0f, 0.6f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(rect.x + 6f, rect.y + 2f, size.x, size.y), content, _reminder);
    }

    private void OnlyThis(GameObject keep)
    {
        foreach (ItemData item in _pool.items)
            if (item != null && item.prefab != null)
                ItemTestFilter.Set(item.prefab, item.prefab == keep);
    }

    private int AllowedCount()
    {
        int n = 0;
        foreach (ItemData item in _pool.items)
            if (item != null && item.prefab != null && ItemTestFilter.Allows(item.prefab)) n++;
        return n;
    }

    private void EnsureStyles()
    {
        int size = Mathf.Max(13, Screen.height / 50);
        if (_title != null && _label.fontSize == size) return;

        _title    = new GUIStyle(GUI.skin.label)  { fontSize = size + 2, fontStyle = FontStyle.Bold };
        _label    = new GUIStyle(GUI.skin.label)  { fontSize = size, alignment = TextAnchor.MiddleRight };
        _toggle   = new GUIStyle(GUI.skin.toggle) { fontSize = size };
        _button   = new GUIStyle(GUI.skin.button) { fontSize = size };
        _reminder = new GUIStyle(GUI.skin.label)  { fontSize = size };
        _title.normal.textColor = _label.normal.textColor = Color.white;
        _toggle.normal.textColor = _toggle.onNormal.textColor = _toggle.hover.textColor = _toggle.onHover.textColor = Color.white;
        _reminder.normal.textColor = new Color(1f, 0.85f, 0.2f);
    }
}
#endif
