#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(GameManager))]
public class GameManagerEditor : Editor
{
    private int _killTargetIndex = 0;

    private string[] _playerDropdownOptions = new string[0];

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        if (!Application.isPlaying)
        {
            EditorGUILayout.Space(8f);
            EditorGUILayout.HelpBox("Las herramientas de debug solo están disponibles en Play Mode.", MessageType.Info);
            return;
        }

        GameManager gameManager = (GameManager)target;

        EditorGUILayout.Space(12f);
        DrawSeparator("Debug Tools");

        EditorGUILayout.LabelField("Players", EditorStyles.boldLabel);
        EditorGUILayout.Space(2f);

        GUI.backgroundColor = new Color(0.4f, 0.8f, 0.4f);
        if (GUILayout.Button("Add Player", GUILayout.Height(28f)))
        {
            gameManager.Debug_AddPlayer();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(8f);

        EditorGUILayout.LabelField("Match", EditorStyles.boldLabel);
        EditorGUILayout.Space(2f);

        GUI.backgroundColor = new Color(0.4f, 0.6f, 1f);
        if (GUILayout.Button("Force Start Game", GUILayout.Height(28f)))
        {
            gameManager.Debug_ForceStartGame();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(8f);

        EditorGUILayout.LabelField("Kill Player", EditorStyles.boldLabel);
        EditorGUILayout.Space(2f);

        RebuildDropdownOptions(gameManager);

        if (_playerDropdownOptions.Length == 0)
        {
            EditorGUILayout.HelpBox("No hay jugadores registrados en la partida.", MessageType.Warning);
        }
        else
        {
            _killTargetIndex = Mathf.Clamp(_killTargetIndex, 0, _playerDropdownOptions.Length - 1);

            _killTargetIndex = EditorGUILayout.Popup("Target Player", _killTargetIndex, _playerDropdownOptions);

            EditorGUILayout.Space(4f);

            GUI.backgroundColor = new Color(1f, 0.4f, 0.4f);
            if (GUILayout.Button("Kill Selected Player", GUILayout.Height(28f)))
            {
                int realIndex = ParsePlayerIndex(_playerDropdownOptions[_killTargetIndex]);
                if (realIndex >= 0)
                    gameManager.Debug_KillPlayer(realIndex);
            }
            GUI.backgroundColor = Color.white;
        }

        EditorGUILayout.Space(6f);

        Repaint();
    }

    private void RebuildDropdownOptions(GameManager gameManager)
    {
        PlayerController[] players = gameManager.Debug_InGamePlayers;

        if (players == null)
        {
            _playerDropdownOptions = new string[0];
            return;
        }

        int count = 0;
        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] != null) count++;
        }

        _playerDropdownOptions = new string[count];
        int optionIndex = 0;

        for (int i = 0; i < players.Length; i++)
        {
            if (players[i] == null) continue;
            _playerDropdownOptions[optionIndex] = $"Player {i} — {players[i].gameObject.name}";
            optionIndex++;
        }
    }

    private int ParsePlayerIndex(string label)
    {
        if (string.IsNullOrEmpty(label)) return -1;

        string[] parts = label.Split(' ');
        if (parts.Length < 2) return -1;

        if (int.TryParse(parts[1], out int index))
            return index;

        return -1;
    }

    private void DrawSeparator(string title)
    {
        EditorGUILayout.Space(2f);
        Rect rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        EditorGUILayout.Space(4f);

        GUIStyle style = new GUIStyle(EditorStyles.boldLabel)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 11
        };
        EditorGUILayout.LabelField(title, style);
        EditorGUILayout.Space(4f);

        rect = EditorGUILayout.GetControlRect(false, 1f);
        EditorGUI.DrawRect(rect, new Color(0.5f, 0.5f, 0.5f, 0.5f));
        EditorGUILayout.Space(6f);
    }
}
#endif