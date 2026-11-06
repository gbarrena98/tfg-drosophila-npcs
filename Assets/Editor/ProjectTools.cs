using System;
using System.IO;
using NpcLab.Verification;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NpcLab.EditorTools
{
    public static class ProjectTools
    {
        const string ScenePath="Assets/Scenes/NavigationLab.unity";
        [MenuItem("TFG/Abrir laboratorio")]
        public static void OpenLab(){EditorSceneManager.OpenScene(ScenePath);}

        [MenuItem("TFG/Ejecutar comprobaciones del nucleo")]
        public static void ValidateCore()
        {
            string root=Directory.GetParent(Application.dataPath).FullName;
            CheckReport report=ValidationChecks.Run(root);
            string path=Path.Combine(root,"Validation/Executed/unity_editor_core_checks.json");
            Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,report.ToJson());
            Debug.Log(report.ToJson());
            if(report.Failures>0)throw new InvalidOperationException(report.Failures+" core checks failed.");
            Debug.Log("All "+report.Names.Count+" C# core checks passed inside Unity. Report: "+path);
        }

        [MenuItem("TFG/Compilar Windows 64 bits")]
        public static void BuildWindows()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play mode before building.");
            if(!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Standalone,BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Install Windows Build Support (Mono) for this Unity version.");
            ValidateCore();
            PlayerSettings.companyName="German Barrena Alonso";PlayerSettings.productName="Drosophila Navigation Lab";
            PlayerSettings.bundleVersion="0.2.0";PlayerSettings.defaultScreenWidth=1440;PlayerSettings.defaultScreenHeight=900;
            PlayerSettings.fullScreenMode=FullScreenMode.Windowed;PlayerSettings.runInBackground=true;
            PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Standalone,ScriptingImplementation.Mono2x);
            EditorSettings.serializationMode=SerializationMode.ForceText;
            EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
            string projectRoot=Directory.GetParent(Application.dataPath).FullName;
            string output=Path.Combine(projectRoot,"Builds/Windows/DrosophilaNavigationLab.exe");Directory.CreateDirectory(Path.GetDirectoryName(output));
            BuildReport report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{
                scenes=new[]{ScenePath},locationPathName=output,target=BuildTarget.StandaloneWindows64,options=BuildOptions.None});
            if(report.summary.result!=BuildResult.Succeeded)throw new InvalidOperationException("Build failed: "+report.summary.result);
            string manifest="{\"unityVersion\":\""+Application.unityVersion+"\",\"buildResult\":\"Succeeded\",\"target\":\"StandaloneWindows64\",\"coreVersion\":\"0.2.0-pec2\"}\n";
            File.WriteAllText(Path.Combine(Path.GetDirectoryName(output),"build_info.json"),manifest);
            foreach(string name in new[]{"LICENCIA.md","THIRD_PARTY_NOTICES.md"})
                File.Copy(Path.Combine(projectRoot,name),Path.Combine(Path.GetDirectoryName(output),name),true);
            File.Copy(Path.Combine(projectRoot,"Research/Bloque1/third_party/MaimonLab_MIT_LICENSE.txt"),
                Path.Combine(Path.GetDirectoryName(output),"MaimonLab_MIT_LICENSE.txt"),true);
            File.Copy(Path.Combine(projectRoot,"Docs/LEEME_PLAYER.txt"),Path.Combine(Path.GetDirectoryName(output),"LEEME.txt"),true);
            Debug.Log("Windows Player built at "+output+". Test it before packaging a release.");
        }
    }
}
