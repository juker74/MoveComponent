using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using UnityEngine;
// #if UNITY_EDITOR    //是Unity编辑器才引入
using UnityEditor;
// #endif


public class ATSdkUtil
{
// #if UNITY_EDITOR
    public static string GetAssetPath(string path)
    {
        var tempPath = Path.Combine("Assets", path);
        return tempPath;
    }

    public static bool Exists(string path)
    {
        return Directory.Exists(path) || File.Exists(path);
    }
// #endif
}