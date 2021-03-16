using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;


[CustomEditor(typeof(SHTester))]
public class SHTesterInspector : Editor
{
    private SHTester shTester;


    private void OnEnable()
    {
        shTester = (SHTester)target;

        shTester.InitUnit();
    }
    private void OnDisable()
    {
        shTester.ReleaseUnit();
    }

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();

        if (GUILayout.Button("Compute SH"))
        {
            if (shTester)
                shTester.BuildSH();
        }
    }

    private void OnSceneGUI()
    {
        if (shTester)
            shTester.DrawSH();
    }
}

