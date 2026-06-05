#if UNITY_EDITOR
using UnityEditor;
using System.IO;

public static class BuildScript
{
    public static void BuildWebGL()
    {
        string buildPath = "Builds/WebGL";
        Directory.CreateDirectory(buildPath);

        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = new[] { "Assets/Scenes/castle.unity" };
        options.locationPathName = buildPath;
        options.target = BuildTarget.WebGL;
        options.options = BuildOptions.None;

        BuildPipeline.BuildPlayer(options);
    }

    public static void BuildAndroid()
    {
        string buildPath = "Builds/Android/bomber-run.apk";
        Directory.CreateDirectory(Path.GetDirectoryName(buildPath));

        BuildPlayerOptions options = new BuildPlayerOptions();
        options.scenes = new[] { "Assets/Scenes/castle.unity" };
        options.locationPathName = buildPath;
        options.target = BuildTarget.Android;
        options.options = BuildOptions.None;

        BuildPipeline.BuildPlayer(options);
    }
}
#endif
