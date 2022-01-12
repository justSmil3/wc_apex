using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class wc_apex : MonoBehaviour
{

    [Range(0, 100)]
    public int Steepness = 0;
    private int m_Steepness = 0;

    private int m_res = 1024;
    private float lowestValue = 1;
    private float highestValue = 0; 
    private float[,] terrainPlane;
    private float[,] originalTerrainPlane;
    private float[] terrainData;
    private RenderTexture outputtexture;
    private Terrain terrain;
    private Vector2 lowestPointPos;

    private List<Vector2> EdgeCoordinates;

    ComputeBuffer terrainBuffer;
    ComputeBuffer edgeBuffer;
    ComputeBuffer edgeCoordinatesBuffer;

    [UnityEngine.SerializeField]
    private ComputeShader shader;


    void InitValues()
    {
        EdgeCoordinates = new List<Vector2>();
    }

    void LoadTerrain(string fileName, int res, bool resetRotation = true)
    {
        terrainPlane = new float[res, res];
        terrainData = new float[res * res];

        using (var file = System.IO.File.OpenRead(fileName))
        using (var reader = new System.IO.BinaryReader(file))
        {
            for (int i = 0; i < res * res; i++)
            {
                float v = (float)reader.ReadUInt16() / 0xFFFF;
                // have to think of always 3 points
                int x = i % res;
                int y = (int)i / res;
                terrainPlane[x, y] = v;

                terrainData[i] = v;


                if (v < lowestValue)
                {
                    lowestValue = v;
                    lowestPointPos = new Vector2(x, y);
                }

                if (v > highestValue)
                    highestValue = v;

            }
        }

        terrain = GetComponent<Terrain>();
        terrain.terrainData.SetHeights(0, 0, terrainPlane);
        originalTerrainPlane = (float[,])terrainPlane.Clone();

    }


    void DispatchEdgeDetection()
    {
        var kernalHandle = shader.FindKernel("Edge");

        // set compute buffer for checking zero points
        edgeBuffer = new ComputeBuffer(terrainData.Length, sizeof(float));
        edgeBuffer.SetData(originalTerrainPlane);
        shader.SetBuffer(kernalHandle, "Edges", edgeBuffer);

        // set compute buffer for terrain data
        terrainBuffer = new ComputeBuffer(terrainData.Length, sizeof(float));
        terrainBuffer.SetData(originalTerrainPlane);
        shader.SetBuffer(kernalHandle, "Terrain", terrainBuffer);

        // set the terrain resolution in the shader 
        shader.SetInt("resolution", m_res);

        // set the lowest point in the terrain
        shader.SetFloat("LowestPoint", lowestValue);

        // set the steepness of the shader 
        float steepness = (float)Steepness / 100;
        float height = highestValue - lowestValue;
        shader.SetFloat("steepness", steepness * height);

        // dispatch the shader function with the edge pragma 
        shader.Dispatch(kernalHandle, m_res * m_res / 32, 1, 1);

        // update terrain data and set edge coordinates
        float[] terraindata = new float[m_res * m_res];
        float[] edgeData = new float[m_res * m_res];
        terrainBuffer.GetData(terraindata);
        edgeBuffer.GetData(edgeData);
        EdgeCoordinates.Clear();
        terrainData = terraindata;

        for (int i = 0; i < terraindata.Length; i++)
        {
            // set terrain data
            int x = i % m_res;
            int y = (int)(i / m_res);
            terrainPlane[x, y] = terraindata[i];

            // update edge coordinates
            if (edgeData[i] == 1)
            {
                EdgeCoordinates.Add(new Vector2(x, y));
            }
        }
        terrain.terrainData.SetHeights(0, 0, terrainPlane);
    }

    private void DispatchShader() // TODO this entire script is a terrible mess, next time make it better, just make it better
    {
        // get function pragma
        var apexHandle = shader.FindKernel("Apex");

        // set resolution
        shader.SetInt("resolution", m_res);

        // set lowest point
        shader.SetFloat("LowestPoint", lowestValue);

        // set steepness
        shader.SetFloat("steepness", (float)Steepness / 100);

        // set lowest point 
        shader.SetFloat("LowestPoint", lowestValue);

        // set edge coordinate buffer
        edgeCoordinatesBuffer = new ComputeBuffer(EdgeCoordinates.Count, sizeof(float) * 2);
        edgeCoordinatesBuffer.SetData(EdgeCoordinates);
        shader.SetBuffer(apexHandle, "EdgeCoordinates", edgeCoordinatesBuffer);
        
        // set Terrain Buffer
        terrainBuffer = new ComputeBuffer(terrainData.Length, sizeof(float));
        terrainBuffer.SetData(terrainData);
        shader.SetBuffer(apexHandle, "Terrain", terrainBuffer);
        
        // dispatch shader
        shader.Dispatch(apexHandle, m_res * m_res / 32, 1, 1);

        // update Terrain data
        float[] terraindata = new float[m_res * m_res];
        terrainBuffer.GetData(terraindata);
        terrainData = terraindata;

        for (int i = 0; i < terraindata.Length; i++)
        {
            int x = i % m_res;
            int y = (int)(i / m_res);
            terrainPlane[x, y] = terraindata[i];
        }

        terrain.terrainData.SetHeights(0, 0, terrainPlane);
    }

    
    private void Awake()
    {
        InitValues();
        WCApex();
        // DispatchEdgeDetection();
        // DispatchShader();
    }

    private void Update()
    {
        if (m_Steepness != Steepness)
        {
            m_Steepness = Steepness;
            DispatchEdgeDetection();
            DispatchShader();
        }
    }

    void RecalculateToDefaultValues()
    {
        float steepnessfactor = (float)Steepness / 100 * (highestValue - lowestValue);
        for(int x = 0; x < terrainPlane.GetLength(0); x++)
        {
            for(int y = 0; y < terrainPlane.GetLength(1); y++)
            {
                float v = terrainPlane[x, y];
                if (v <= lowestValue + steepnessfactor)
                    v = 0;
                terrainPlane[x, y] = v;
            }
        }
    }

    void WCApex()
    {
        LoadTerrain("Assets/Perlin.raw", m_res);
        RecalculateToDefaultValues();
    }


    private void OnDestroy()
    {
        if(edgeBuffer != null)
            edgeBuffer.Dispose();
        if(edgeCoordinatesBuffer != null)
            edgeCoordinatesBuffer.Dispose();
        if(terrainBuffer != null)
            terrainBuffer.Dispose();
    }
}
