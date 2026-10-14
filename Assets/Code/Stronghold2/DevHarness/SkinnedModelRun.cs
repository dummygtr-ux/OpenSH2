using Assets.Code.Stronghold2.Buildings;
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Assets.Code.Stronghold2.DevHarness
{
  /// <summary>
  /// Batchmode entry point so a compile/run check can be scripted without the
  /// editor staying open:
  ///   Unity -batchmode -quit -executeMethod SkinnedModelRun.Main
  /// Runs the skinned-mesh harness across the rig families that occur in the
  /// shipped models - Bip01 humanoids of several sizes, an animal rig with its
  /// own root, a mixed model whose weapon meshes are rigid, and the 1-bone
  /// siege boxes - so one run covers the general path and the rigid fallback.
  /// </summary>
  public static class SkinnedModelRun
  {
    public static string GameRoot => Constants.GamePath;

    public static void Main()
    {
      string log = Path.Combine(Path.GetTempPath(), "sh2_skinned_report.txt");
      var report = new System.Text.StringBuilder();
      var models = new List<string>
      {
        "meshes/units/models/archer.GR2",             // 23 bones, Bip01 humanoid
        "meshes/units/models/lord.GR2",               // 27 bones + 4 rigid weapon meshes
        "meshes/units/animals/wolf.GR2",              // 46 bones, WOLF root
        "meshes/units/animals/deer_bambi.GR2",        // 24 bones, stag root
        "meshes/siege_equipment/trebuchet.GR2",       // large skinned model
        "meshes/siege_equipment/mangonel.GR2",        // skinned
        "meshes/siege_equipment/ballista_body.gr2",   // 1 bone, rigid
        "meshes/buildings/Keep_small_closed.GR2",     // negative control: no skeleton
      };
      int pass = 0, fail = 0;
      foreach (string relative in models)
      {
        try
        {
          GameObject root = S2MSkinnedModelHarness.Report(relative, GameRoot);
          if (root != null) pass++;
          else fail++;
        }
        catch (Exception ex)
        {
          fail++;
          report.AppendLine($"== {relative}\n   EXCEPTION {ex.GetType().Name}: {ex.Message}");
          Debug.LogError($"[SkinnedModelRun] {relative}: {ex}");
        }
      }
      report.AppendLine($"SUMMARY: {pass} ok, {fail} failed of {models.Count}");
      File.WriteAllText(log, report.ToString());
      Debug.Log("[SkinnedModelRun]\n" + report.ToString().TrimEnd());
    }
  }
}
