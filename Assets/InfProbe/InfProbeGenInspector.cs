using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(InfProbeGen))]
public class InfProbeGenInspector : Editor
{
    private InfProbeGen probeGen;


    private void _generateProbes()
    {

    }


    private void OnEnable()
    {
        probeGen = (InfProbeGen)target;
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
    }

    private void OnSceneGUI()
    {
        { // render AABB
            var vDisplayExtents = Handles.DoPositionHandle(probeGen.vAABBExtents + probeGen.transform.position, Quaternion.identity);
            probeGen.vAABBExtents = vDisplayExtents - probeGen.transform.position;
            probeGen.vAABBExtents.x = Mathf.Max(0, probeGen.vAABBExtents.x);
            probeGen.vAABBExtents.y = Mathf.Max(0, probeGen.vAABBExtents.y);
            probeGen.vAABBExtents.z = Mathf.Max(0, probeGen.vAABBExtents.z);

            Handles.DrawWireCube(probeGen.transform.position, probeGen.vAABBExtents * 2);
        }
        _generateProbes();
    }
}