using Lz4;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using static PIG2FBX.PigModel;

namespace PIG2FBX
{
    internal class PigReader : BinaryReader
    {

        public PigReader(Stream stream) : base(stream) { }
        public PigReader(FileStream stream) : base(stream) { }
        public PigReader(string path) : base(File.OpenRead(path)) { }

        public void ReadMarker()
        {
            int marker = ReadInt32();
            if (marker != 64) throw new InvalidMarkerException(marker, BaseStream.Position);
        }

        public override string ReadString()
        {
            byte[] stringData = new byte[ReadInt16()];
            Read(stringData, 0, stringData.Length);
            return Encoding.UTF8.GetString(stringData);
        }

        public PigNode ReadNode()
        {
            try
            {
                PigNode node = new PigNode();

                ReadMarker();
                node.name = ReadString();
                byte nbyte = ReadByte();
                node.parentID = ReadInt16();

                node.position[0] = ReadSingle();
                node.position[1] = ReadSingle();
                node.position[2] = ReadSingle();
                node.rotation[0] = ReadSingle();
                node.rotation[1] = ReadSingle();
                node.rotation[2] = ReadSingle();
                node.rotation[3] = ReadSingle();
                node.scale[0] = ReadSingle();
                node.scale[1] = ReadSingle();
                node.scale[2] = ReadSingle();

                float afloat = ReadSingle();
                short ashort = ReadInt16();

                return node;
            } catch { throw; }
        }

        public short ReadNodeCount()
        {
            try
            {
                ReadMarker();
                return ReadInt16();
            } catch { throw; }
        }

        public short ReadObjectCount()
        {
            return ReadInt16();
        }

        public string DecompressPVR(string tgaFile)
        {
            string outFile = Path.GetDirectoryName(tgaFile) + "\\" + Path.GetFileNameWithoutExtension(tgaFile);
            bool compressed = false;

            using (BinaryReader tgaStream = new BinaryReader(File.OpenRead(tgaFile)))
            {
                int head = tgaStream.ReadInt32();

                switch (head)
                {
                    case 1481919403:
                        {
                            outFile += ".ktx";
                            break;
                        }
                    case 55727696:
                        {
                            outFile += ".pvr";
                            int imageSize = 52;
                            tgaStream.BaseStream.Position = 44;
                            int mipCount = tgaStream.ReadInt32();
                            int metaSize = tgaStream.ReadInt32();
                            if (metaSize > 0)
                            {
                                int JETtest = tgaStream.ReadInt32();
                                int LZ4test = tgaStream.ReadInt32();
                                if (JETtest == 1413827072 && LZ4test == 878332928) //lz4
                                {
                                    compressed = true;
                                    List<Lm4ZIP> lz4mipData = new List<Lm4ZIP>();

                                    int dataSize = tgaStream.ReadInt32();
                                    for (int i = 0; i < mipCount; i++)
                                    {//assume no surfaces or faces
                                        Lm4ZIP amip = new Lm4ZIP();
                                        amip.offset = tgaStream.ReadInt32();
                                        amip.compressedSize = tgaStream.ReadInt32();
                                        amip.uncompressedSize = tgaStream.ReadInt32();
                                        imageSize += amip.uncompressedSize;
                                        lz4mipData.Add(amip);
                                    }

                                    byte[] imageBuffer = new byte[imageSize];
                                    tgaStream.BaseStream.Position = 0;
                                    tgaStream.Read(imageBuffer, 0, 48); //excluding meta data size which will be 0
                                    int imageOffset = 52;

                                    foreach (var amip in lz4mipData)
                                    {
                                        tgaStream.BaseStream.Position = 52 + metaSize + amip.offset;
                                        byte[] lz4buffer = new byte[amip.compressedSize];
                                        tgaStream.Read(lz4buffer, 0, amip.compressedSize);

                                        using (var inputStream = new MemoryStream(lz4buffer))
                                        {
                                            var decoder = new Lz4DecoderStream(inputStream);

                                            byte[] mipBuffer = new byte[amip.uncompressedSize];
                                            for (; ; )
                                            {
                                                int nRead = decoder.Read(mipBuffer, 0, amip.uncompressedSize);
                                                if (nRead == 0)
                                                    break;
                                            }

                                            Buffer.BlockCopy(mipBuffer, 0, imageBuffer, imageOffset, amip.uncompressedSize);
                                            imageOffset += amip.uncompressedSize;
                                        }
                                    }

                                    using (BinaryWriter pvrStream = new BinaryWriter(File.Open(outFile, FileMode.Create)))
                                    {
                                        pvrStream.Write(imageBuffer);
                                        pvrStream.Close();
                                    }

                                    return outFile;
                                }
                            }
                            break;
                        }
                }


            }

            if (!compressed)
            {
                File.Move(tgaFile, outFile);
                return outFile;
            }
            else { return tgaFile; }
        }

        public PigObject ReadObject()
        {
            PigObject pigObject = new PigObject();

            int geometryCount = 0;
            int usedTexCount = 0;

            List<PigMaterial> materials = new List<PigMaterial>();
            List<PigTexture> textures = new List<PigTexture>();

            PigMaterialComparer materialComparer = new PigMaterialComparer();
            PigTextureComparer textureComparer = new PigTextureComparer();

        ReadMarker(); //0x64
            pigObject.nodeID = ReadInt32();
            int LODcount = ReadInt16();

            for (int l = 0; l < LODcount; l++)
            {
                byte LODnum = ReadByte();
                ReadMarker(); //0x64
                short edata = ReadInt16();
                BaseStream.Position += 24; //bounding box
                short meshCount = ReadInt16();
                geometryCount += meshCount;

                for (int m = 0; m < meshCount; m++)
                {
                    PigMesh newmesh = new PigMesh();
                    newmesh.LODnum = LODnum;

                    ReadMarker(); //0x64
                    BitArray bitflags = new BitArray(new int[1] { ReadInt32() });
                    int FVFcode = ReadInt32();
                    BaseStream.Position += 12; //mpivot

                    if (bitflags[0])
                    {
                        newmesh.position[0] = ReadSingle();
                        newmesh.position[1] = ReadSingle();
                        newmesh.position[2] = ReadSingle();
                        newmesh.scale[0] = ReadSingle();
                        newmesh.scale[1] = ReadSingle();
                        newmesh.scale[2] = ReadSingle();
                    }

                    ushort vertexCount = ReadUInt16();
                    newmesh.vertexCount = vertexCount;
                    int indexCount = ReadInt32();
                    string materialName = ReadString();
                    newmesh.materialName = materialName;
                    newmesh.materialID = materials.Count;

                    PigMaterial pmat = new PigMaterial();
                    pmat.name = materialName;

                    short texureCount = ReadInt16();
                    for (int t = 0; t < texureCount; t++)
                    {
                        string textureName = ReadString();

                        if (textureName.Length > 0)
                        {
                            PigTexture ptex = new PigTexture();
                            ptex.name = textureName;
                            int ptexID = textures.Count;
                            var existingTexIndex = textures.BinarySearch(ptex, textureComparer);
                            if (existingTexIndex >= 0) { ptexID = existingTexIndex; }
                            else
                            {
                                //get filename
                                string[] texFiles = Directory.GetFiles(Path.GetDirectoryName((BaseStream as FileStream).Name) + "\\..\\", Path.GetFileNameWithoutExtension(textureName) + ".pvr", SearchOption.AllDirectories);
                                if (texFiles.Length == 0)
                                {
                                    texFiles = Directory.GetFiles(Path.GetDirectoryName((BaseStream as FileStream).Name) + "\\..\\", textureName, SearchOption.AllDirectories);
                                    if (texFiles.Length > 0)
                                    {
                                        var pvrfile = DecompressPVR(texFiles[0]);
                                        ptex.filename = pvrfile;
                                    }
                                }
                                else { ptex.filename = Path.GetFullPath(texFiles[0]); }

                                textures.Add(ptex);
                            }

                            switch (t)
                            {
                                case 0: //diffuse
                                    pmat.diffuseID = ptexID;
                                    usedTexCount += 1;
                                    break;
                                case 2: //normal
                                    pmat.normalID = ptexID;
                                    usedTexCount += 1;
                                    break;
                            }
                        }
                    }

                    //add material only if it wasn't added before
                    var existingMatIndex = materials.BinarySearch(pmat, materialComparer);
                    if (existingMatIndex >= 0)
                    {
                        newmesh.materialID = existingMatIndex;
                    }
                    else { materials.Add(pmat); }


                    byte abyte = ReadByte();
                    int bufferSize = ReadInt32();
                    byte[] geobuffer = new byte[bufferSize];

                    if (bufferSize == 0) //lz4 compression
                    {
                        int compressedSize = ReadInt32();
                        int uncompressedSize = ReadInt32();

                        byte[] lz4buffer = new byte[compressedSize];
                        Read(lz4buffer, 0, compressedSize);

                        using (var inputStream = new MemoryStream(lz4buffer))
                        {
                            var decoder = new Lz4DecoderStream(inputStream);

                            geobuffer = new byte[uncompressedSize]; //is this ok?
                            for (; ; )
                            {
                                int nRead = decoder.Read(geobuffer, 0, geobuffer.Length);
                                if (nRead == 0)
                                    break;
                            }
                        }
                        /*using (BinaryWriter debugStream = new BinaryWriter(File.Open((Path.GetDirectoryName(fileName) + "\\" + Path.GetFileNameWithoutExtension(fileName) + "_" + o + "_" + l + "_" + m), FileMode.Create)))
                        {
                            debugStream.Write(geobuffer);
                            debugStream.Close();
                        }*/
                    }
                    else { Read(geobuffer, 0, bufferSize); }

                    newmesh.indices = new ushort[indexCount];

                    using (BinaryReader geostream = new BinaryReader(new MemoryStream(geobuffer)))
                    {
                        geostream.BaseStream.Position = 0; //is this needed?

                        #region positions
                        if ((FVFcode | 1) == FVFcode) //positions
                        {
                            newmesh.vertices = new float[vertexCount * 3];
                            short align = geostream.ReadInt16();
                            geostream.BaseStream.Position += align;

                            //test if components are 16bit shorts or 32bit floats
                            int predictedAlign = 16 - ((vertexCount * 8) % 16) - 2;
                            geostream.BaseStream.Position += vertexCount * 8;
                            align = geostream.ReadInt16();
                            geostream.BaseStream.Position -= vertexCount * 8 + 2;

                            if (align != predictedAlign)
                            {
                                for (int v = 0; v < vertexCount * 3; v++)
                                {
                                    newmesh.vertices[v] = geostream.ReadSingle();
                                }
                            }
                            else
                            {
                                for (int v = 0; v < vertexCount; v++)
                                {
                                    newmesh.vertices[v * 3] = (float)geostream.ReadInt16() / 32767;
                                    newmesh.vertices[v * 3 + 1] = (float)geostream.ReadInt16() / 32767;
                                    newmesh.vertices[v * 3 + 2] = (float)geostream.ReadInt16() / 32767;
                                    geostream.BaseStream.Position += 2; //w component
                                }
                            }

                        }
                        #endregion

                        #region normals
                        if ((FVFcode | 2) == FVFcode) //normals
                        {
                            newmesh.normals = new float[vertexCount * 3];
                            short align = geostream.ReadInt16();
                            geostream.BaseStream.Position += align;

                            for (int v = 0; v < vertexCount; v++)
                            {
                                newmesh.normals[v * 3] = (float)geostream.ReadSByte() / 127;
                                newmesh.normals[v * 3 + 1] = (float)geostream.ReadSByte() / 127;
                                newmesh.normals[v * 3 + 2] = (float)geostream.ReadSByte() / 127;
                                geostream.BaseStream.Position += 1;
                            }
                        }
                        #endregion

                        #region misc
                        if ((FVFcode | 4) == FVFcode) //tangents
                        {
                            short align = geostream.ReadInt16();
                            geostream.BaseStream.Position += align;
                            geostream.BaseStream.Position += vertexCount * 4;
                        }

                        if ((FVFcode | 8) == FVFcode) //??
                        {
                            short align = geostream.ReadInt16();
                            geostream.BaseStream.Position += align;
                            geostream.BaseStream.Position += vertexCount * 4;
                        }

                        if ((FVFcode | 16) == FVFcode) //??
                        {
                            short align = geostream.ReadInt16();
                            geostream.BaseStream.Position += align;
                            geostream.BaseStream.Position += vertexCount * 4;
                        }

                        if ((FVFcode | 32) == FVFcode) //??
                        {
                            short align = geostream.ReadInt16();
                            geostream.BaseStream.Position += align;
                            geostream.BaseStream.Position += vertexCount * 4;
                        }

                        if ((FVFcode | 64) == FVFcode) //not colors
                        {
                            //newmesh.colors = new float[vertexCount * 4];
                            short align = geostream.ReadInt16();
                            geostream.BaseStream.Position += align;
                            geostream.BaseStream.Position += vertexCount * 4;
                            /*for (int v = 0; v < vertexCount * 4; v++)
                            {
                                newmesh.colors[v] = (float)geostream.ReadByte() / 255;
                            }*/
                        }
                        #endregion

                        #region texture coords
                        if ((FVFcode | 128) == FVFcode) //texture0
                        {
                            newmesh.texture0 = new float[vertexCount * 2];
                            short align = geostream.ReadInt16();
                            geostream.BaseStream.Position += align;

                            //test if components are 16bit shorts or 32bit floats
                            int predictedAlign = 16 - ((vertexCount * 4) % 16) - 2;
                            geostream.BaseStream.Position += vertexCount * 4;
                            align = geostream.ReadInt16();
                            geostream.BaseStream.Position -= vertexCount * 4 + 2;

                            if (align != predictedAlign)
                            {
                                for (int v = 0; v < vertexCount; v++)
                                {
                                    newmesh.texture0[v * 2] = geostream.ReadSingle();
                                    newmesh.texture0[v * 2 + 1] = 1f - geostream.ReadSingle();
                                }
                            }
                            else
                            {
                                for (int v = 0; v < vertexCount; v++)
                                {
                                    newmesh.texture0[v * 2] = (float)geostream.ReadInt16() / 32767;
                                    newmesh.texture0[v * 2 + 1] = 1f - (float)geostream.ReadInt16() / 32767;
                                }
                            }

                        }

                        if ((FVFcode | 256) == FVFcode) //texture1
                        {
                            newmesh.texture1 = new float[vertexCount * 2];
                            short align = geostream.ReadInt16();
                            geostream.BaseStream.Position += align;

                            //test if components are 16bit shorts or 32bit floats
                            int predictedAlign = 16 - ((vertexCount * 4) % 16) - 2;
                            geostream.BaseStream.Position += vertexCount * 4;
                            align = geostream.ReadInt16();
                            geostream.BaseStream.Position -= vertexCount * 4 + 2;

                            if (align != predictedAlign)
                            {
                                for (int v = 0; v < vertexCount; v++)
                                {
                                    newmesh.texture1[v * 2] = geostream.ReadSingle();
                                    newmesh.texture1[v * 2] = 1f - geostream.ReadSingle();
                                }
                            }
                            else
                            {
                                for (int v = 0; v < vertexCount; v++)
                                {
                                    newmesh.texture1[v * 2] = (float)geostream.ReadInt16() / 32767;
                                    newmesh.texture1[v * 2] = 1f - (float)geostream.ReadInt16() / 32767;
                                }
                            }

                        }
                        #endregion

                        #region misc2
                        if ((FVFcode | 512) == FVFcode) //??
                        {
                            short align = geostream.ReadInt16();
                            geostream.BaseStream.Position += align;

                            int predictedAlign = 16 - ((vertexCount * 4) % 16) - 2;
                            geostream.BaseStream.Position += vertexCount * 4;
                            align = geostream.ReadInt16();
                            geostream.BaseStream.Position -= 2;
                            if (align != predictedAlign) { geostream.BaseStream.Position += vertexCount * 4; } //happened in car_ford_mustang_2015_rfx.pig
                        }

                        if ((FVFcode | 1024) == FVFcode) //??
                        {
                            short align = geostream.ReadInt16();
                            geostream.BaseStream.Position += align;
                            geostream.BaseStream.Position += vertexCount * 8;
                        }
                        #endregion

                        geostream.BaseStream.Position = geostream.BaseStream.Length - (indexCount * 2); //failsafe in case vertex properties are misread
                                                                                                        //note that IB alignment is skipped
                        for (int i = 0; i < indexCount; i++)
                        {
                            newmesh.indices[i] = geostream.ReadUInt16();
                        }
                    }


                    for (int e = 0; e < edata; e++)
                    {
                        short short1 = ReadInt16();
                        short short2 = ReadInt16();
                        int extraSize = ReadInt32();
                        if (extraSize == 0)
                        {
                            int compressedSize = ReadInt32();
                            int uncompressedSize = ReadInt32();
                            BaseStream.Position += compressedSize;
                        }
                        else { BaseStream.Position += extraSize; }
                    }

                    pigObject.meshList.Add(newmesh);
                }
            }

            return pigObject;
        }
    }

    internal class InvalidMarkerException : Exception
    {
        public InvalidMarkerException(int received, long position) : base($"Invalid marker. Expected: 64, received: {received} at {position}") { }
    }
}
