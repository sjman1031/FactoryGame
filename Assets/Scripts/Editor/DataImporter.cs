using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Tools > 📊 Data Importer — CSV → ScriptableObject 자동 생성/갱신.
///
/// 검증 원칙: 문제가 있는 행은 에셋을 갱신하지 않고, 파일명·행 번호·이유를 로그 패널에 남긴다.
/// (에디터 도구는 성공 로그 전에 예외가 나면 아무것도 안 보이고 조용히 실패한다 — 그래서 모든 버튼을 감싼다)
/// </summary>
public class DataImporter : EditorWindow
{
    private const string CSV_ROOT = "Assets/Data/CSV/";
    private const string SO_ROOT  = "Assets/Data/ScriptableObjects/";
    private const string DB_PATH  = "Assets/Resources/" + GameDatabase.RESOURCE_PATH + ".asset";
    private const string LINE_KEY = "__line";   // 행 딕셔너리에 원본 행 번호를 넣어 두는 키

    private Vector2      _scroll;
    private List<string> _log = new();
    private int          _warnings;
    private string       _currentFile;

    [MenuItem("Tools/📊 Data Importer")]
    public static void Open() => GetWindow<DataImporter>("📊 Data Importer");

    private void OnGUI()
    {
        EditorGUILayout.LabelField("기획 데이터 임포터", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("CSV → ScriptableObject 자동 변환\n경로: " + CSV_ROOT, MessageType.Info);
        EditorGUILayout.Space(8);

        if (GUILayout.Button("📦  아이템 임포트",   GUILayout.Height(30))) Run(ImportItems);
        if (GUILayout.Button("🔧  레시피 임포트",   GUILayout.Height(30))) Run(ImportRecipes);
        if (GUILayout.Button("⛏️  자원노드 임포트", GUILayout.Height(30))) Run(ImportResourceNodes);

        EditorGUILayout.Space(8);
        GUI.backgroundColor = new Color(0.3f, 0.8f, 0.4f);
        if (GUILayout.Button("🔄  전체 임포트", GUILayout.Height(44)))
        { _log.Clear(); Run(() => { ImportItems(); ImportRecipes(); ImportResourceNodes(); }); }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(8);
        EditorGUILayout.LabelField("로그", EditorStyles.boldLabel);
        _scroll = EditorGUILayout.BeginScrollView(_scroll, GUILayout.Height(160));
        foreach (var l in _log) EditorGUILayout.LabelField(l, EditorStyles.wordWrappedLabel);
        EditorGUILayout.EndScrollView();
        if (GUILayout.Button("로그 지우기")) _log.Clear();
    }

    /// <summary>임포트를 실행하고 DB를 갱신한다. 예외는 로그 패널에 보여 준다. 끝에 경고 수를 요약한다.</summary>
    private void Run(Action import)
    {
        _warnings = 0;
        try
        {
            import();
            RebuildDatabase();
            Log(_warnings == 0 ? "✅ 완료!" : $"⚠️ 완료 — 경고 {_warnings}건. 위 로그를 확인하세요.");
        }
        catch (Exception e)
        {
            Log($"❌ 임포트 중 예외: {e.Message}");
            Debug.LogException(e);
        }
        finally
        {
            _currentFile = null;
        }
    }

    private void ImportItems()
    {
        EnsureDir(SO_ROOT + "Items/");
        var rows = ReadCSV(CSV_ROOT + "items.csv"); if (rows == null) return;
        int count = 0;
        foreach (var row in rows)
        {
            if (!TryGetId(row, out string id)) continue;
            string path = $"{SO_ROOT}Items/{id}.asset";
            var so = LoadOrCreate<ItemData>(path);
            so.itemId      = id;
            so.displayName = row.GetOr("name_kr", id);
            so.category    = ParseEnum(row, "category", ItemCategory.Raw);
            so.stackSize   = Mathf.Max(1, ParseInt(row, "stack_size", 50));
            Save(so, path); count++;
        }
        AssetDatabase.Refresh(); Log($"📦 아이템 {count}개 완료");
    }

    private void ImportRecipes()
    {
        EnsureDir(SO_ROOT + "Recipes/");
        var rows = ReadCSV(CSV_ROOT + "recipes.csv"); if (rows == null) return;
        int count = 0;
        foreach (var row in rows)
        {
            if (!TryGetId(row, out string id)) continue;

            // 재료와 결과물을 먼저 전부 확인한다. 하나라도 못 찾으면 이 행은 에셋을 갱신하지 않는다.
            // (이전에는 못 찾은 재료를 경고 없이 빼서 재료가 덜 드는 레시피가 생겼고,
            //  결과물을 못 찾으면 output = null로 저장돼 런타임 Tick에서 예외가 났음)
            bool ok = true;
            var inputs = new List<RecipeIngredient>();
            for (int i = 1; i <= 4; i++)
            {
                string itemId = row.GetOr($"input{i}_id", "");
                if (string.IsNullOrEmpty(itemId)) break;   // CSV 규칙: input1부터 연속으로 — 빈 칸 뒤는 읽지 않는다
                var itemSO = LoadItem(itemId);
                if (itemSO == null) { Warn(row, $"레시피 '{id}'의 재료 input{i}_id '{itemId}' 아이템 없음"); ok = false; continue; }
                inputs.Add(new RecipeIngredient { item = itemSO, amount = Mathf.Max(1, ParseInt(row, $"input{i}_amount", 1)) });
            }

            string outputId = row.GetOr("output_id", "");
            var output = string.IsNullOrEmpty(outputId) ? null : LoadItem(outputId);
            if (output == null) { Warn(row, $"레시피 '{id}'의 결과물 output_id '{outputId}' 아이템 없음"); ok = false; }

            if (!ok) { Warn(row, $"레시피 '{id}' — 건너뜀 (기존 에셋은 그대로 둠)"); continue; }

            string path = $"{SO_ROOT}Recipes/{id}.asset";
            var so = LoadOrCreate<RecipeData>(path);
            so.recipeId     = id;
            so.displayName  = row.GetOr("display_name", id);
            so.machineType  = ParseEnum(row, "machine_type", MachineType.Manufacturer);
            so.craftTime    = Mathf.Max(0.01f, ParseFloat(row, "craft_time", 4f));
            so.inputs       = inputs.ToArray();
            so.output       = output;
            so.outputAmount = Mathf.Max(1, ParseInt(row, "output_amount", 1));
            Save(so, path); count++;
        }
        AssetDatabase.Refresh(); Log($"🔧 레시피 {count}개 완료");
    }

    private void ImportResourceNodes()
    {
        EnsureDir(SO_ROOT + "ResourceNodes/");
        var rows = ReadCSV(CSV_ROOT + "resource_nodes.csv"); if (rows == null) return;
        int count = 0;
        foreach (var row in rows)
        {
            if (!TryGetId(row, out string id)) continue;

            string itemId = row.GetOr("item_id", "");
            var item = string.IsNullOrEmpty(itemId) ? null : LoadItem(itemId);
            if (item == null) { Warn(row, $"자원노드 '{id}'의 item_id '{itemId}' 아이템 없음 — 건너뜀"); continue; }

            string path = $"{SO_ROOT}ResourceNodes/{id}.asset";
            var so = LoadOrCreate<ResourceNodeData>(path);
            so.nodeId              = id;
            so.item                = item;
            so.purity              = ParseEnum(row, "purity", NodePurity.Normal);
            so.baseOutputPerMinute = Mathf.Max(0f, ParseFloat(row, "base_output_per_min", 60f));
            Save(so, path); count++;
        }
        AssetDatabase.Refresh(); Log($"⛏️ 자원노드 {count}개 완료");
    }

    /// <summary>
    /// SO 폴더의 에셋을 모아 런타임 조회용 GameDatabase를 갱신한다.
    /// SaveAssets까지 해서 기존 에셋의 SetDirty 변경도 이때 디스크에 기록된다.
    /// </summary>
    private void RebuildDatabase()
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources")) AssetDatabase.CreateFolder("Assets", "Resources");
        var db = LoadOrCreate<GameDatabase>(DB_PATH);
        db.items   = LoadAllAssets<ItemData>(SO_ROOT + "Items");
        db.recipes = LoadAllAssets<RecipeData>(SO_ROOT + "Recipes");
        db.nodes   = LoadAllAssets<ResourceNodeData>(SO_ROOT + "ResourceNodes");
        Save(db, DB_PATH);
        AssetDatabase.SaveAssets();
        Log($"📚 GameDatabase 갱신 — 아이템 {db.items.Count} · 레시피 {db.recipes.Count} · 노드 {db.nodes.Count}");
    }

    private static List<T> LoadAllAssets<T>(string folder) where T : ScriptableObject
    {
        var list = new List<T>();
        if (!AssetDatabase.IsValidFolder(folder)) return list;
        foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }))
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
            if (asset != null) list.Add(asset);
        }
        return list;
    }

    private static ItemData LoadItem(string itemId)
        => AssetDatabase.LoadAssetAtPath<ItemData>($"{SO_ROOT}Items/{itemId}.asset");

    // ── CSV 읽기 ───────────────────────────────────────────────
    /// <summary>CSV를 행 딕셔너리 목록으로 읽는다. 파일을 읽지 못하면 null.</summary>
    private List<Dictionary<string, string>> ReadCSV(string path)
    {
        _currentFile = Path.GetFileName(path);
        if (!File.Exists(path)) { Log($"❌ 없음: {path}"); _warnings++; return null; }

        string[] lines;
        try
        {
            lines = File.ReadAllLines(path, Encoding.UTF8);
        }
        catch (IOException e)
        {
            Log($"❌ {_currentFile} 읽기 실패 (Excel에서 열려 있나요?): {e.Message}");
            _warnings++;
            return null;
        }
        catch (UnauthorizedAccessException e)
        {
            Log($"❌ {_currentFile} 접근 거부: {e.Message}");
            _warnings++;
            return null;
        }

        var result = new List<Dictionary<string, string>>();
        List<string> headers = null;
        for (int lineNo = 1; lineNo <= lines.Length; lineNo++)
        {
            string line = lines[lineNo - 1];
            if (IsBlankOrComment(line)) continue;

            var values = ParseLine(line);
            if (values.TrueForAll(string.IsNullOrEmpty)) continue;   // Excel이 남기는 ",,,," 빈 행

            if (headers == null)
            {
                headers = values.ConvertAll(h => h.Trim('﻿', ' '));
                if (!headers.Contains("id"))
                { Log($"❌ {_currentFile} {lineNo}행: 헤더에 'id' 열이 없습니다 — 파일을 건너뜁니다. 헤더: {string.Join(",", headers)}"); _warnings++; return null; }
                continue;
            }

            var row = new Dictionary<string, string> { [LINE_KEY] = lineNo.ToString() };
            for (int i = 0; i < headers.Count; i++)
                if (!string.IsNullOrEmpty(headers[i])) row[headers[i]] = i < values.Count ? values[i] : "";
            result.Add(row);
        }
        if (headers == null) { Log($"❌ {_currentFile}: 헤더 행이 없습니다"); _warnings++; return null; }
        return result;
    }

    // Excel은 쉼표가 들어간 셀을 따옴표로 감싸므로 "# 주석, ..."처럼 저장된다.
    // 앞의 BOM·공백·따옴표를 벗긴 뒤 #를 검사해야 주석 줄이 헤더로 읽히지 않는다.
    private static bool IsBlankOrComment(string line)
    {
        if (string.IsNullOrWhiteSpace(line)) return true;
        return line.TrimStart('﻿', ' ', '\t', '"').StartsWith("#");
    }

    // RFC 4180: 따옴표 안의 ""는 따옴표 하나다 (이전에는 따옴표마다 토글만 해서 사라졌음)
    private static List<string> ParseLine(string line)
    {
        var result = new List<string>(); bool inQ = false;
        var cur = new StringBuilder();
        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (c == '"')
            {
                if (inQ && i + 1 < line.Length && line[i + 1] == '"') { cur.Append('"'); i++; }
                else inQ = !inQ;
                continue;
            }
            if (c == ',' && !inQ) { result.Add(cur.ToString().Trim()); cur.Clear(); continue; }
            cur.Append(c);
        }
        result.Add(cur.ToString().Trim()); return result;
    }

    // ── 행 검증·파싱 ──────────────────────────────────────────
    // id가 없거나 비어 있으면 경고 후 건너뛴다 (이전에는 row["id"]가 KeyNotFoundException을 냈음)
    private bool TryGetId(Dictionary<string, string> row, out string id)
    {
        if (!row.TryGetValue("id", out id) || string.IsNullOrWhiteSpace(id))
        { Warn(row, "id 없음 — 건너뜀"); id = null; return false; }
        if (id.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        { Warn(row, $"id '{id}'에 파일명으로 쓸 수 없는 문자가 있음 — 건너뜀"); return false; }
        return true;
    }

    // 숫자는 문화권과 무관하게 '.' 소수점으로 읽는다. 값이 있는데 못 읽으면 셀 위치를 남긴다.
    private int ParseInt(Dictionary<string, string> row, string col, int def)
    {
        string s = row.GetOr(col, null);
        if (s == null) return def;
        if (int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out int v)) return v;
        Warn(row, $"'{col}' 값 '{s}'은 정수가 아님 — 기본값 {def}");
        return def;
    }

    private float ParseFloat(Dictionary<string, string> row, string col, float def)
    {
        string s = row.GetOr(col, null);
        if (s == null) return def;
        if (float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)) return v;
        Warn(row, $"'{col}' 값 '{s}'은 숫자가 아님 — 기본값 {def}");
        return def;
    }

    private T ParseEnum<T>(Dictionary<string, string> row, string col, T def) where T : struct
    {
        string s = row.GetOr(col, null);
        if (s == null) return def;
        if (Enum.TryParse(s, ignoreCase: true, out T v) && Enum.IsDefined(typeof(T), v)) return v;
        Warn(row, $"'{col}' 값 '{s}'은 {typeof(T).Name}에 없음 — 기본값 {def}");
        return def;
    }

    private void Warn(Dictionary<string, string> row, string msg)
    {
        _warnings++;
        string line = row.TryGetValue(LINE_KEY, out var n) ? n : "?";
        Log($"⚠️ {_currentFile} {line}행: {msg}");
    }

    // ── 에셋 ──────────────────────────────────────────────────
    private static T LoadOrCreate<T>(string path) where T : ScriptableObject
    {
        var existing = AssetDatabase.LoadAssetAtPath<T>(path);
        return existing != null ? existing : ScriptableObject.CreateInstance<T>();
    }

    private static void Save(ScriptableObject so, string path)
    {
        if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(path) == null)
            AssetDatabase.CreateAsset(so, path);
        else EditorUtility.SetDirty(so);
    }

    private static void EnsureDir(string path) { if (!Directory.Exists(path)) Directory.CreateDirectory(path); }
    private void Log(string msg) { _log.Add(msg); Debug.Log($"[DataImporter] {msg}"); Repaint(); }
}

internal static class DictExt
{
    public static string GetOr(this Dictionary<string, string> d, string k, string fb)
        => d.TryGetValue(k, out string v) && !string.IsNullOrEmpty(v) ? v : fb;
}
