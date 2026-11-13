using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class UnidadSecundaria : Unidad
{


    protected override void Awake()
    {
        base.Awake();
    }

    protected override void OnDestroy()
    {
        base.OnDestroy();
    }

    public void MoverA(List<HexCelda> camino)
    {
        StartCoroutine(RecorrerCamino(camino));
    }

}
