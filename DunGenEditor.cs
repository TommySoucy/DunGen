using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(DunGen))]
public class DunGenEditor : Editor
{
    public override void OnInspectorGUI()
    {

        DunGen script = (DunGen)target;

        if (DrawDefaultInspector())
        {
            if (Application.isPlaying)
            {
                script.Generate();
            }
        }

        if (GUILayout.Button("Generate"))
        {
            if (Application.isPlaying)
            {
                script.Generate();
            }
        }
    }
}

