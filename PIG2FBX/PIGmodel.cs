using System;
using System.Collections.Generic;
using System.IO;
using Lz4;

namespace PIG2FBX
{
    public class PigNode
    {
        public string name;
        public short parentID;
        public float[] position = new float[3] { 0, 0, 0 };
        public float[] rotation = new float[4] { 0, 0, 0, 1 };
        public float[] scale = new float[3] { 1, 1, 1 };
    }

    public class PigObject
    {
        public int nodeID;
        public List<PigMesh> meshList = new List<PigMesh>();
    }

    public class PigMesh
    {
        public byte LODnum;
        public float[] position = new float[3] { 0, 0, 0 };
        public float[] scale = new float[3] { 1, 1, 1 };
        public ushort vertexCount;
        public string materialName;
        public int materialID;
        public float[] vertices;
        public float[] normals;
        public float[] colors;
        public float[] texture0;
        public float[] texture1;
        public ushort[] indices;
    }

    public class PigMaterial
    {
        public string name;
        public int diffuseID = -1;
        public int normalID = -1;
    }

    public class PigTexture
    {
        public string name;
        public string filename = "";
    }

    public class PigModel
    {
        public List<PigNode> nodes = new List<PigNode>();
        public List<PigObject> objects = new List<PigObject>();

        public int usedTexCount = 0;
        
        public class PigMaterialComparer : IComparer<PigMaterial>
        {
            public int Compare(PigMaterial x, PigMaterial y)
            {
                return x.name.CompareTo(y.name);
            }
        }

        public class PigTextureComparer : IComparer<PigTexture>
        {
            public int Compare(PigTexture x, PigTexture y)
            {
                return x.name.CompareTo(y.name);
            }
        }
        

        public class Lm4ZIP
        {
            public int offset;
            public int compressedSize;
            public int uncompressedSize;
        }

        public PigModel(string PIGfile)
        {
            try
            {
                using (PigReader pigStream = new PigReader(PIGfile))
                {
                    short nodeCount = pigStream.ReadNodeCount();

                    for (int n = 0; n < nodeCount; n++)
                        nodes.Add(pigStream.ReadNode());

                    byte abyte = pigStream.ReadByte();
                    short objectCount = pigStream.ReadObjectCount();

                    for (int o = 0; o < objectCount; o++)
                    {
                        
                        objects.Add(pigStream.ReadObject());
                    }
                }
            } catch { throw; }
        }



        
    }
}
