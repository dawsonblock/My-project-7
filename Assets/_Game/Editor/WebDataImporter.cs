using System;
using System.Collections.Generic;
using System.IO;
using Escape.Data;
using UnityEditor;
using UnityEngine;

namespace Escape.EditorTools
{
    /// <summary>
    /// Tools → Escape the Elites → Import Web Content.
    /// Reads the frozen web-build JSON in MigrationReference/ and generates
    /// or updates definition ScriptableObjects under Resources/Definitions.
    /// Deterministic file names; existing assets are updated, not duplicated.
    /// </summary>
    public static class WebDataImporter
    {
        private const string RefDir = "Assets/_Game/MigrationReference";
        private const string OutDir = "Assets/_Game/Resources/Definitions";

        [Serializable] private class IdRef { public string[] ids = new string[0]; }
        [Serializable] private class EvidenceDto { public string id; public string title; public string description; public string category; public string[] corroborates = new string[0]; public string[] contradicts = new string[0]; public bool requiredForBroadcast; public int endingWeight; }
        [Serializable] private class EvidenceFile { public EvidenceDto[] evidence; }
        [Serializable] private class ObjectiveDto { public string id; public string title; public string description; public string[] requiredObjectives = new string[0]; public string[] requiredEvidence = new string[0]; public string completion; public bool optional; }
        [Serializable] private class ObjectiveFile { public ObjectiveDto[] objectives; }
        [Serializable] private class InsightDto { public string id; public string title; public string description; public string[] requiredEvidence = new string[0]; public string unlocksObjective; }
        [Serializable] private class InsightFile { public InsightDto[] insights; }
        [Serializable] private class DocumentDto { public string id; public string title; public string body; public string grantsEvidence; }
        [Serializable] private class DocumentFile { public DocumentDto[] documents; }
        [Serializable] private class TerminalCommandDto { public string command; public string label; public string successText; public string failureText; public string action; public string targetId; public string completesObjective; public string[] requiredEvidence = new string[0]; public string[] requiredObjectives = new string[0]; public string[] requiredInsights = new string[0]; public bool hideUntilUnlocked; }
        [Serializable] private class TerminalDto { public string id; public string displayName; public string unlockCode; public string requiredObjective; public string[] associatedDoors = new string[0]; public string[] associatedCameras = new string[0]; public TerminalCommandDto[] commands = new TerminalCommandDto[0]; }
        [Serializable] private class TerminalFile { public TerminalDto[] terminals; }
        [Serializable] private class EndingDto { public string id; public string title; public string description; public int minPrimaryEvidence; public string[] requiredEvidence = new string[0]; public string[] requiredInsights = new string[0]; public int priority; public bool isSecret; }
        [Serializable] private class EndingFile { public EndingDto[] endings; }

        [MenuItem("Tools/Escape the Elites/Import Web Content")]
        public static void ImportAll()
        {
            EnsureFolder(OutDir);
            var evidence = ImportEvidence();
            var objectives = ImportObjectives();
            var insights = ImportInsights();
            var documents = ImportDocuments();
            var endings = ImportEndings();
            var terminals = ImportTerminals();
            EnsureTuning();
            AssetDatabase.SaveAssets();
            Debug.Log($"[WebDataImporter] Imported {evidence} evidence, {objectives} objectives, " +
                      $"{insights} insights, {documents} documents, {endings} endings, {terminals} terminals.");
        }

        // ---------- Pass 1: create/update shells ----------

        private static int ImportEvidence()
        {
            var file = ReadJson<EvidenceFile>("evidence.json");
            if (file?.evidence == null) return 0;
            EnsureFolder(OutDir + "/Evidence");
            var byId = new Dictionary<string, EvidenceDefinition>();
            foreach (var dto in file.evidence)
            {
                var so = GetOrCreate<EvidenceDefinition>($"{OutDir}/Evidence/EVD_{dto.id}.asset");
                so.Id = dto.id;
                so.Title = dto.title;
                so.Description = dto.description;
                so.Category = Enum.TryParse(dto.category, true, out EvidenceCategory c) ? c : EvidenceCategory.Operational;
                so.RequiredForBroadcast = dto.requiredForBroadcast;
                so.EndingWeight = dto.endingWeight;
                byId[dto.id] = so;
                EditorUtility.SetDirty(so);
            }
            // Pass 2: resolve relationships now that all shells exist.
            foreach (var dto in file.evidence)
            {
                var so = byId[dto.id];
                so.Corroborates = Resolve(dto.corroborates, byId);
                so.Contradicts = Resolve(dto.contradicts, byId);
                EditorUtility.SetDirty(so);
            }
            return file.evidence.Length;
        }

        private static int ImportObjectives()
        {
            var file = ReadJson<ObjectiveFile>("objectives.json");
            if (file?.objectives == null) return 0;
            EnsureFolder(OutDir + "/Objectives");
            var evById = LoadAll<EvidenceDefinition>(OutDir + "/Evidence");
            var byId = new Dictionary<string, ObjectiveDefinition>();
            foreach (var dto in file.objectives)
            {
                var so = GetOrCreate<ObjectiveDefinition>($"{OutDir}/Objectives/OBJ_{dto.id}.asset");
                so.Id = dto.id;
                so.Title = dto.title;
                so.Description = dto.description;
                so.Optional = dto.optional;
                so.Completion = Enum.TryParse(dto.completion, true, out ObjectiveCompletionMode mode)
                    ? mode : ObjectiveCompletionMode.Evidence;
                so.RequiredEvidence = Resolve(dto.requiredEvidence, evById);
                byId[dto.id] = so;
                EditorUtility.SetDirty(so);
            }
            foreach (var dto in file.objectives)
            {
                byId[dto.id].RequiredObjectives = Resolve(dto.requiredObjectives, byId);
                EditorUtility.SetDirty(byId[dto.id]);
            }
            return file.objectives.Length;
        }

        private static int ImportInsights()
        {
            var file = ReadJson<InsightFile>("insights.json");
            if (file?.insights == null) return 0;
            EnsureFolder(OutDir + "/Insights");
            var evById = LoadAll<EvidenceDefinition>(OutDir + "/Evidence");
            var objById = LoadAll<ObjectiveDefinition>(OutDir + "/Objectives");
            foreach (var dto in file.insights)
            {
                var so = GetOrCreate<InsightDefinition>($"{OutDir}/Insights/INS_{dto.id}.asset");
                so.Id = dto.id;
                so.Title = dto.title;
                so.Description = dto.description;
                so.RequiredEvidence = Resolve(dto.requiredEvidence, evById);
                so.UnlocksObjective = !string.IsNullOrEmpty(dto.unlocksObjective) &&
                    objById.TryGetValue(dto.unlocksObjective, out var o) ? o : null;
                EditorUtility.SetDirty(so);
            }
            return file.insights.Length;
        }

        private static int ImportDocuments()
        {
            var file = ReadJson<DocumentFile>("documents.json");
            if (file?.documents == null) return 0;
            EnsureFolder(OutDir + "/Documents");
            var evById = LoadAll<EvidenceDefinition>(OutDir + "/Evidence");
            foreach (var dto in file.documents)
            {
                var so = GetOrCreate<DocumentDefinition>($"{OutDir}/Documents/DOC_{dto.id}.asset");
                so.Id = dto.id;
                so.Title = dto.title;
                so.Body = dto.body;
                so.GrantsEvidence = !string.IsNullOrEmpty(dto.grantsEvidence) &&
                    evById.TryGetValue(dto.grantsEvidence, out var e) ? e : null;
                EditorUtility.SetDirty(so);
            }
            return file.documents.Length;
        }

        private static int ImportEndings()
        {
            var file = ReadJson<EndingFile>("endings.json");
            if (file?.endings == null) return 0;
            EnsureFolder(OutDir + "/Endings");
            var evById = LoadAll<EvidenceDefinition>(OutDir + "/Evidence");
            var insById = LoadAll<InsightDefinition>(OutDir + "/Insights");
            foreach (var dto in file.endings)
            {
                var so = GetOrCreate<EndingDefinition>($"{OutDir}/Endings/END_{dto.id}.asset");
                so.Id = dto.id;
                so.Title = dto.title;
                so.Description = dto.description;
                so.MinPrimaryEvidence = dto.minPrimaryEvidence;
                so.Priority = dto.priority;
                so.IsSecret = dto.isSecret;
                so.RequiredEvidence = Resolve(dto.requiredEvidence, evById);
                so.RequiredInsights = Resolve(dto.requiredInsights, insById);
                EditorUtility.SetDirty(so);
            }
            return file.endings.Length;
        }

        private static int ImportTerminals()
        {
            var file = ReadJson<TerminalFile>("terminals.json");
            if (file?.terminals == null) return 0;
            EnsureFolder(OutDir + "/Terminals");
            var evById = LoadAll<EvidenceDefinition>(OutDir + "/Evidence");
            var objById = LoadAll<ObjectiveDefinition>(OutDir + "/Objectives");
            var insById = LoadAll<InsightDefinition>(OutDir + "/Insights");
            foreach (var dto in file.terminals)
            {
                var so = GetOrCreate<TerminalDefinition>($"{OutDir}/Terminals/TRM_{dto.id}.asset");
                so.Id = dto.id;
                so.DisplayName = dto.displayName;
                so.UnlockCode = dto.unlockCode ?? "";
                so.RequiredObjective = !string.IsNullOrEmpty(dto.requiredObjective) &&
                    objById.TryGetValue(dto.requiredObjective, out var o) ? o : null;
                so.AssociatedDoorIds = dto.associatedDoors ?? new string[0];
                so.AssociatedCameraIds = dto.associatedCameras ?? new string[0];
                var cmds = new List<TerminalCommandDefinition>();
                foreach (var c in dto.commands)
                {
                    cmds.Add(new TerminalCommandDefinition
                    {
                        Command = c.command,
                        Label = string.IsNullOrEmpty(c.label) ? c.command : c.label,
                        SuccessText = c.successText ?? "",
                        FailureText = c.failureText ?? "COMMAND FAILED",
                        Action = Enum.TryParse(c.action, true, out TerminalActionType a)
                            ? a : TerminalActionType.ShowMessage,
                        TargetId = c.targetId ?? "",
                        CompletesObjective = !string.IsNullOrEmpty(c.completesObjective) &&
                            objById.TryGetValue(c.completesObjective, out var co) ? co : null,
                        RequiredEvidence = Resolve(c.requiredEvidence, evById),
                        RequiredObjectives = Resolve(c.requiredObjectives, objById),
                        RequiredInsights = Resolve(c.requiredInsights, insById),
                        HideUntilUnlocked = c.hideUntilUnlocked
                    });
                }
                so.Commands = cmds.ToArray();
                EditorUtility.SetDirty(so);
            }
            return file.terminals.Length;
        }

        private static void EnsureTuning()
        {
            var path = OutDir + "/StealthTuning.asset";
            if (AssetDatabase.LoadAssetAtPath<StealthTuning>(path) == null)
            {
                var t = ScriptableObject.CreateInstance<StealthTuning>();
                AssetDatabase.CreateAsset(t, path);
            }
        }

        // ---------- helpers ----------

        private static T ReadJson<T>(string fileName) where T : class
        {
            var path = Path.Combine(RefDir, fileName);
            if (!File.Exists(path))
            {
                Debug.LogWarning($"[WebDataImporter] Missing {path}");
                return null;
            }
            return JsonUtility.FromJson<T>(File.ReadAllText(path));
        }

        private static T GetOrCreate<T>(string path) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(path);
            if (existing != null) return existing;
            var so = ScriptableObject.CreateInstance<T>();
            AssetDatabase.CreateAsset(so, path);
            return so;
        }

        private static Dictionary<string, T> LoadAll<T>(string folder) where T : ScriptableObject
        {
            var map = new Dictionary<string, T>();
            foreach (var guid in AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { folder }))
            {
                var so = AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid));
                var idProp = typeof(T).GetField("Id");
                if (so != null && idProp != null) map[(string)idProp.GetValue(so)] = so;
            }
            return map;
        }

        private static T[] Resolve<T>(string[] ids, Dictionary<string, T> byId) where T : ScriptableObject
        {
            var list = new List<T>();
            foreach (var id in ids ?? new string[0])
            {
                if (byId.TryGetValue(id, out var so)) list.Add(so);
                else Debug.LogWarning($"[WebDataImporter] Unresolved {typeof(T).Name} ref '{id}'");
            }
            return list.ToArray();
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
