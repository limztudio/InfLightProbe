using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEngine.Rendering;

[CustomEditor(typeof(InfProbeFinder))]
public class InfProbeFinderInspector : Editor
{
    private InfProbeFinder probeFinder;


    private void OnEnable()
    {
        probeFinder = (InfProbeFinder)target;

        probeFinder.InitProbeFinder();
    }
    private void OnDisable()
    {
    }

    private void OnSceneGUI()
    {
        Handles.zTest = CompareFunction.Less;

        ref var probeGen = ref probeFinder.probeGen;

        probeFinder.UpdateProbe();

        if (probeFinder.iLastProbe >= 0)
        {
            {
                Handles.color = Color.gray;

                Handles.DrawLine(probeGen.vTetVertices[probeFinder.iLastProbe]._0, probeGen.vTetVertices[probeFinder.iLastProbe]._1);
                Handles.DrawLine(probeGen.vTetVertices[probeFinder.iLastProbe]._0, probeGen.vTetVertices[probeFinder.iLastProbe]._2);
                Handles.DrawLine(probeGen.vTetVertices[probeFinder.iLastProbe]._0, probeGen.vTetVertices[probeFinder.iLastProbe]._3);

                Handles.DrawLine(probeGen.vTetVertices[probeFinder.iLastProbe]._1, probeGen.vTetVertices[probeFinder.iLastProbe]._2);
                Handles.DrawLine(probeGen.vTetVertices[probeFinder.iLastProbe]._1, probeGen.vTetVertices[probeFinder.iLastProbe]._3);

                Handles.DrawLine(probeGen.vTetVertices[probeFinder.iLastProbe]._2, probeGen.vTetVertices[probeFinder.iLastProbe]._3);
            }

            {
                var vSize = new Vector3(0.2f, 0.2f, 0.2f);

                Handles.color = Color.red;
                Handles.DrawWireCube(probeGen.vTetVertices[probeFinder.iLastProbe]._0, vSize);

                Handles.color = Color.green;
                Handles.DrawWireCube(probeGen.vTetVertices[probeFinder.iLastProbe]._1, vSize);

                Handles.color = Color.blue;
                Handles.DrawWireCube(probeGen.vTetVertices[probeFinder.iLastProbe]._2, vSize);

                Handles.color = Color.magenta;
                Handles.DrawWireCube(probeGen.vTetVertices[probeFinder.iLastProbe]._3, vSize);
            }
        }
    }
}
