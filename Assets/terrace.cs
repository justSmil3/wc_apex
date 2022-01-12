using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public enum Process
{
    Default,
    Residual,
    Gentle
}

//[RequireComponent(typeof(MeshFilter))]
public class terrace : MonoBehaviour
{

    public TerrainData terra;
    public Texture2D heightmap;

    [Range(0,1000)]
    public int terraces = 1000;
    [Range(0, 100)]
    public int Uniformity = 100;
    [Range(0, 100)]
    public int Steepness = 85;
    [Range(0, 100)]
    public int Intensity = 100;

    [Range(0, 100)]
    public int topfalloff = 100;

    [Range(-10,10)]
    public float rotX, rotY;

    public bool SoftFalloff = false;
    public bool Reprocess = false;

    public int Seed = 0;

    public Process process = Process.Default;

    private float[,] terrainPlane;
    private float[,] rotPlane;

    private int m_res = 1024;

    public bool test = false;


    private int m_strength = 0; 
    private int m_Uniformity = 100;
    private int m_Steepness = 85;
    private int m_Intensity = 100;
    private int m_topfalloff = 100;
    private float m_rotX = 0, m_rotY = 0;
    public delegate void OnStrengthChangeDelegate();
    public event OnStrengthChangeDelegate OnStrengthChange;

    private float highestValue = 0;

    /// <summary>
    ///  so essentialy i need to use the load terrain methode just like this but 
    ///  without the hightest point value, instead using the lowest point walue to put this exact 
    ///  value as the start of the <= 0 threshhold, means that if a height value is lower than that, the 
    ///  program automaticly sets it 0 
    ///  
    ///  change i made at home is that I will normalize the height values so that only if steepness = 100 everything will be 0. 
    /// 
    ///  than I go recursively from center to outwards to search for a point <= 0 
    ///  once this is found i recalculate the height with the distance.
    /// </summary>
    /// <param name="fileName"></param>
    /// <param name="res"></param>
    /// <param name="resetRotation"></param>


    void LoadTerrain(string fileName, int res, bool resetRotation = true)
    {
        terrainPlane = new float[res,res];

        if (resetRotation)
            rotPlane = new float[res, res];

        using (var file = System.IO.File.OpenRead(fileName))
        using (var reader = new System.IO.BinaryReader(file))
        {
            for (int i = 0; i < res * res; i++)
            {
                float v = (float)reader.ReadUInt16() / 0xFFFF;
                // have to think of always 3 points
                int x = i % res;
                int y = (int)i / res;
                terrainPlane[x,y] = v;

                if (resetRotation)
                    rotPlane[x, y] = v > 0 ? 1 : 0;

                if (v > highestValue)
                    highestValue = v;
            }
        }

    }

    void SetA (ref int[] A, ref uint idx, int v)
    {
        A[idx] = v;
        idx++;
    }

    // Update is called once per frame
    void Start()
    {
        LoadTerrain("Assets/Mountain.raw", m_res);
        terra = this.GetComponent<Terrain>().terrainData;
        m_res = terra.heightmapResolution;
        terra.SetHeights(0, 0, terrainPlane);
        //terrainPlane = new float[m_res, m_res];
        //terrainPlane = terra.GetHeights(0,0, m_res, m_res);
    }

    private void Update()
    {
        if (terraces != m_strength || 
            Uniformity != m_Uniformity ||
            m_Steepness != Steepness ||
            m_Intensity != Intensity ||
            m_topfalloff != topfalloff)
        {
            m_Steepness = Steepness;
            m_Uniformity = Uniformity;
            m_strength = terraces;
            m_Intensity = Intensity;
            m_topfalloff = topfalloff;
            CreateTerraces();
        }
        if(m_rotX != rotX || m_rotY != rotY)
        {
            m_rotX = rotX;
            m_rotY = rotY;
            rotate();
        }
    }


    void rotate()
    {
        for (int x = 0; x < rotPlane.GetLength(0); x++)
        for (int y = 0; y < rotPlane.GetLength(1); y++)
            {
                float tmpy = (1 - 
                    ((y / ((float)rotPlane.GetLength(1) - 1)) - .5f)* rotY);

                float tmpx = (1 -
                    ((x / ((float)rotPlane.GetLength(0) - 1)) - .5f) * rotX);

                float tmp = (tmpx + tmpy) / 2;
                rotPlane[x, y] = tmp;
            }
        CreateTerraces();
    }

    void CreateTerraces()
    {
        // int kernel = (int)m_res / terraces;
        LoadTerrain("Assets/Mountain.raw", 1024, false);
        if (terraces != 1000)
            for (int x = 0; x < terrainPlane.GetLength(0); x++)
                for (int y = 0; y < terrainPlane.GetLength(1); y++)
                {
                    // get current height at position
                    float v = terrainPlane[x, y];

                    v *= terraces;

                    // calculate Uniformity (that is what does not work)
                    float uni = 100 - Uniformity; // Idear: work with sin

                    // calc terraces
                    int steepTerraces = (int)(v + 0.5f);
                    // calc dif between terraces and normal heights (important for steepenss)
                    float dif = v - steepTerraces;


                    // calculate intensity with factoring in the top falloff
                    float intFactor = Mathf.Max(1 - (float)Intensity / 100, 
                        (terrainPlane[x, y] /  highestValue) * ((float)topfalloff / 100));

                    // calculate the steepnessfactor
                    float SteepFactor = (float)Steepness / 200;
                    v = steepTerraces;

                    // calc steepness
                    if (Mathf.Abs(dif) > SteepFactor)
                    {
                        float steepadd = (dif * ((Mathf.Abs(dif) - SteepFactor) * 
                            Mathf.Max(3, (float)Steepness / 10)));
                        if (Mathf.Abs(steepadd) > Mathf.Abs(dif))
                            steepadd = dif;

                        v += steepadd;

                        // remove applied steepness from diff so that intensity works fine
                        dif -= steepadd;
                    }

                    // apply intensity
                    v += (dif * intFactor);

                    // bring everything to the correct height again
                    v /= terraces;

                    // rotate the terraces with the rotation plane 
                    float vAdd = v * rotPlane[x, y];

                    // gaea hatte das aber irgendwie ist das net soo nützlich find ich
                    if(process == Process.Gentle)
                    {
                        if (vAdd <= terrainPlane[x, y])
                            continue;
                    }
                    // das geht noch net
                    if (process == Process.Residual)
                    {
                        if ((int)(terrainPlane[x, y] + 0.5f) <= terrainPlane[x, y])
                            continue;
                    }
                    terrainPlane[x, y] = vAdd;
                }
        terra.SetHeights(0, 0, terrainPlane);
    }

}

/// for the rotation what I personaly think the best solution would be is 
/// to just take a second height array map and have it multiplied with the 
/// original heights, the second map can than be manipulated. after this is done
/// I shall look into makeing the same operation without an secon array but for
/// now i believe this is the best solution. just left to try for the multiplication
/// bevore and after the terraces are made 