using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DrawRaymarchMandelbulb : MonoBehaviour
{
    [SerializeField] Material mandlbulbMaterial;

    private void OnRenderObject()
    {
        if (!mandlbulbMaterial)
            return;

        mandlbulbMaterial.SetPass(0);
        Graphics.DrawProceduralNow(MeshTopology.Triangles, 3);
    }
}
