using System;
using System.IO;
using DontTrustYourLuck.EditorTools.Graphics;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DontTrustYourLuck.EditorTools
{
    /// <summary>Repeatable integration checks for the circuit release; retains a copy of unsaved scene work.</summary>
    public static class CircuitValidation
    {
        [MenuItem("Tools/Don't Trust Your Luck/Circuit/Validate Tests and Capture")]
        public static void Run()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling) return;
            Directory.CreateDirectory("Logs/CircuitValidation");
            var scene = EditorSceneManager.GetActiveScene();
            if (scene.isDirty)
            {
                string stamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
                if (!string.IsNullOrEmpty(scene.path)) File.Copy(scene.path, $"Logs/CircuitValidation/SceneOnDisk-{stamp}.unity", true);
                if (!EditorSceneManager.SaveScene(scene, $"Temp/CircuitSceneBackup-{stamp}.unity", true))
                { Debug.LogError("Could not back up the unsaved scene. Validation stopped."); return; }
                if (!EditorSceneManager.SaveScene(scene)) return;
            }
            GfxTestChain.Run("Logs/CircuitValidation/tests.txt", ok =>
            {
                Debug.Log($"[CircuitValidation] Tests passed: {ok}");
                Capture();
            });
        }

        [MenuItem("Tools/Don't Trust Your Luck/Circuit/Capture")]
        public static void Capture()
        {
            GfxCaptureRunner.Run("Circuit", new[] { new GfxCaptureJob
            { Name = "Circuit", Note = "Circuit board, occupancy and follow camera", Shots = GfxShots.Showcase } });
        }
    }
}
