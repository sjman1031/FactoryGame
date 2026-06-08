#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>Tools > 📊 Data Importer — CSV → ScriptableObject 자동 생성/갱신.</summary>
public class DataImporter : EditorWindow
{
    private const string CSV_ROOT = "Assets/Data/CSV/";
    private const string SO_ROOT  = "Assets/Data/ScriptableObjects/";

    private Vector2      _scroll;
    private List<string> _log = new();

    [MenuItem("Tools/📊 Data Importer")]
    public static void Open() => GetWindow<DataImporter>("📊 Data Importer");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("기획 데이터 임포터", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("CSV → ScriptableObject 자동 변환\n경로: " + CSV_ROOT, MessageType.Info);
        EditorGUILayout.Space(8);

        if (GUILayout.Button("📦  아이템 임포트",   GUILayout.Height(30))) ImportItems();
        if (GUILayout.Button("🔧  레시피 임포트",   GUILayout.Height(30))) ImportRecipes();
        if (GUILayout.Button("⛏️  자원노드 임포트", GUILayout.Height(30))) ImportResourceNodes();

        EditorGUILayout.Space(8);
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
        if (GUILayout.Button("🔄  전체 임포트", GUILayout.Height(44)))
        { _log.Clear(); ImportItems(); ImportRecipes(); ImportResourceNodes(); Log("✅ 완료!"); }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("로그", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(160));
        foreach (var l in _log) EditorGUILayout.LabelField(l, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
        if (GUILayout.Button("로그 지우기")) _log.Clear();
    }

    private void ImportItems()
    {
        EnsureDir(SO_ROOT + "Items/");
        var rows = ReadCSV(CSV_ROOT + "items.csv"); int count = 0;
        foreach (var row in rows)
        {
            string id = row["id"], path = $"{SO_ROOT}Items/{id}.asset";
            var so = LoadOrCreate<ItemData>(path);
            so.itemId = id; so.displayName = row.GetOr("name_kr", id);
            so.category = ParseEnum<ItemCategory>(row.GetOr("category", "RAW"));
            so.stackSize = ParseInt(row.GetOr("stack_size", "50"), 50);
            Save(so, path); count++;
        }
        AssetDatabase.Refresh(); Log($"📦 아이템 {count}개 완료");
    }

    private void ImportRecipes()
    {
        EnsureDir(SO_ROOT + "Recipes/");
        var rows = ReadCSV(CSV_ROOT + "recipes.csv"); int count = 0;
        foreach (var row in rows)
        {
            string id = row["id"], path = $"{SO_ROOT}Recipes/{id}.asset";
            var so = LoadOrCreate<RecipeData>(path);
            so.recipeId = id; so.displayName = row.GetOr("display_name", id);
            so.machineType = ParseEnum<MachineType>(row.GetOr("machine_type", "MANUFACTURER"));
            so.craftTime = ParseFloat(row.GetOr("craft_time", "4"), 4f);
            var inputs = new List<RecipeIngredient>();
            for (int i = 1; i <= 4; i++)
            {
                string itemId = row.GetOr($"input{i}_id", "");
                if (string.IsNullOrEmpty(itemId)) break;
                var itemSO = AssetDatabase.LoadAssetAtPath<ItemData>($"{SO_ROOT}Items/{itemId}.asset");
                if (itemSO != null) inputs.Add(new RecipeIngredient { item = itemSO, amount = ParseInt(row.GetOr($"input{i}_amount", "1"), 1) });
            }
            so.inputs = inputs.ToArray();
            so.output = AssetDatabase.LoadAssetAtPath<ItemData>($"{SO_ROOT}Items/{row.GetOr("output_id","")}.asset");
            so.outputAmount = ParseInt(row.GetOr("output_amount", "1"), 1);
            Save(so, path); count++;
        }
        AssetDatabase.Refresh(); Log($"🔧 레시피 {count}개 완료");
    }

    private void ImportResourceNodes()
    {
        EnsureDir(SO_ROOT + "ResourceNodes/");
        var rows = ReadCSV(CSV_ROOT + "resource_nodes.csv"); int count = 0;
        foreach (var row in rows)
        {
            string id = row["id"], path = $"{SO_ROOT}ResourceNodes/{id}.asset";
            var so = LoadOrCreate<ResourceNodeData>(path);
            so.nodeId = id;
            so.item = AssetDatabase.LoadAssetAtPath<ItemData>($"{SO_ROOT}Items/{row.GetOr("item_id","")}.asset");
            so.purity = ParseEnum<NodePurity>(row.GetOr("purity", "NORMAL"));
            so.baseOutputPerMinute = ParseFloat(row.GetOr("base_output_per_min", "60"), 60f);
            Save(so, path); count++;
        }
        AssetDatabase.Refresh(); Log($"⛏️ 자원노드 {count}개 완료");
    }

    private List<Dictionary<string, string>> ReadCSV(string path)
    {
        var result = new List<Dictionary<string, string>>();
        if (!File.Exists(path)) { Log($"❌ 없음: {path}"); return result; }
        var lines = File.ReadAllLines(path);
        List<string> headers = null;
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line) || line.TrimStart().StartsWith("#")) continue;
            var values = ParseLine(line);
            if (headers == null) { headers = values; continue; }
            var row = new Dictionary<string, string>();
            for (int i = 0; i < headers.Count; i++) row[headers[i]] = i < values.Count ? values[i] : "";
            result.Add(row);
        }
        return result;
    }

    private List<string> ParseLine(string line)
    {
        var result = new List<string>(); bool inQ = false;
        var cur = new System.Text.StringBuilder();
        foreach (char c in line)
        {
            if (c == '"') { inQ = !inQ; continue; }
            if (c == ',' && !inQ) { result.Add(cur.ToString().Trim()); cur.Clear(); continue; }
            cur.Append(c);
        }
        result.Add(cur.ToString().Trim()); return result;
    }

    private T LoadOrCreate<T>(string path) where T : ScriptableObject
        => AssetDatabase.LoadAssetAtPath<T>(path) ?? ScriptableObject.CreateInstance<T>();
    private void Save(ScriptableObject so, string path)
    {
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) == null)
            AssetDatabase.CreateAsset(so, path);
        else EditorUtility.SetDirty(so);
    }
    private void EnsureDir(string path) { if (!Directory.Exists(path)) Directory.CreateDirectory(path); }
    private int   ParseInt(string s, int def)     => int.TryParse(s, out int v)    ? v : def;
    private float ParseFloat(string s, float def)  => float.TryParse(s, out float v) ? v : def;
    private T     ParseEnum<T>(string s) where T : struct => Enum.TryParse<T>(s.ToUpper(), out T v) ? v : default;
    private void  Log(string msg) { _log.Add(msg); Debug.Log($"[DataImporter] {msg}"); Repaint(); }
}

internal static class DictExt
{
    public static string GetOr(this Dictionary<string, string> d, string k, string fb)
        => d.TryGetValue(k, out string v) && !string.IsNullOrEmpty(v) ? v : fb;
}
#endif
