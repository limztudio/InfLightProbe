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

                Handles.DrawLine(probeGen.vTetPositions[probeGen.vTetIndices[probeFinder.iLastProbe]._0], probeGen.vTetPositions[probeGen.vTetIndices[probeFinder.iLastProbe]._1]);
                Handles.DrawLine(probeGen.vTetPositions[probeGen.vTetIndices[probeFinder.iLastProbe]._0], probeGen.vTetPositions[probeGen.vTetIndices[probeFinder.iLastProbe]._2]);
                Handles.DrawLine(probeGen.vTetPositions[probeGen.vTetIndices[probeFinder.iLastProbe]._0], probeGen.vTetPositions[probeGen.vTetIndices[probeFinder.iLastProbe]._3]);

                Handles.DrawLine(probeGen.vTetPositions[probeGen.vTetIndices[probeFinder.iLastProbe]._1], probeGen.vTetPositions[probeGen.vTetIndices[probeFinder.iLastProbe]._2]);
                Handles.DrawLine(probeGen.vTetPositions[probeGen.vTetIndices[probeFinder.iLastProbe]._1], probeGen.vTetPositions[probeGen.vTetIndices[probeFinder.iLastProbe]._3]);

                Handles.DrawLine(probeGen.vTetPositions[probeGen.vTetIndices[probeFinder.iLastProbe]._2], probeGen.vTetPositions[probeGen.vTetIndices[probeFinder.iLastProbe]._3]);
            }

            {
                var vSize = new Vector3(0.2f, 0.2f, 0.2f);

                Handles.color = probeFinder.vProbeVisibility._0 ? Color.red : Color.gray;
                Handles.DrawWireCube(probeGen.vTetPositions[probeGen.vTetIndices[probeFinder.iLastProbe]._0], vSize);

                Handles.color = probeFinder.vProbeVisibility._1 ? Color.green : Color.gray;
                Handles.DrawWireCube(probeGen.vTetPositions[probeGen.vTetIndices[probeFinder.iLastProbe]._1], vSize);

                Handles.color = probeFinder.vProbeVisibility._2 ? Color.blue : Color.gray;
                Handles.DrawWireCube(probeGen.vTetPositions[probeGen.vTetIndices[probeFinder.iLastProbe]._2], vSize);

                Handles.color = probeFinder.vProbeVisibility._3 ? Color.magenta : Color.gray;
                Handles.DrawWireCube(probeGen.vTetPositions[probeGen.vTetIndices[probeFinder.iLastProbe]._3], vSize);
            }
        }
    }
}
