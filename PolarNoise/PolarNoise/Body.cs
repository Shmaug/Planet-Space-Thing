using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using SimplexNoise;

namespace PolarNoise {
    public struct VertexPositionColorNormal{
        public Vector3 Position;
        public Vector3 Normal;
        public Color Color;

        public VertexPositionColorNormal(Vector3 pos, Vector3 norm, Color col){
            Position = pos;
            Normal = norm;
            Color = col;
        }

        public static VertexDeclaration VertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector3, VertexElementUsage.Normal, 0),
            new VertexElement(24, VertexElementFormat.Color, VertexElementUsage.Color, 0)
            );
    }
    public struct VertexPositionColorPolar {
        public Vector3 Position;
        public Vector2 Polar;
        public Color Color;

        public VertexPositionColorPolar(Vector3 pos, Vector2 pol, Color col) {
            Position = pos;
            Polar = pol;
            Color = col;
        }

        public static VertexDeclaration VertexDeclaration = new VertexDeclaration(
            new VertexElement(0, VertexElementFormat.Vector3, VertexElementUsage.Position, 0),
            new VertexElement(12, VertexElementFormat.Vector2, VertexElementUsage.Position, 1),
            new VertexElement(20, VertexElementFormat.Color, VertexElementUsage.Color, 0)
            );
    }
    public class Icosphere {
        static readonly float t = 1f + (float)Math.Sqrt(5) / 2f;
        public static readonly Vector3[] Verticies = new Vector3[] {
            new Vector3(-1,  t,  0),
            new Vector3( 1,  t,  0),
            new Vector3(-1, -t,  0),
            new Vector3( 1, -t,  0),

            new Vector3( 0, -1,  t),
            new Vector3( 0,  1,  t),
            new Vector3( 0, -1, -t),
            new Vector3( 0,  1, -t),

            new Vector3( t, 0, -1),
            new Vector3( t, 0,  1),
            new Vector3(-t, 0, -1),
            new Vector3(-t, 0,  1) };

        public static readonly int[] Indicies = new int[] {
            // 5 faces around point 0
            5, 11, 0,
            1, 5, 0,
            7, 1, 0,
            10, 7, 0,
            11, 10, 0,
            // 5 adjacent faces
            9, 5, 1,
            4, 11, 5,
            2, 10, 11,
            6, 7, 10,
            8, 1, 7,
            // 5 faces around point 3
            4, 9, 3,
            2, 4, 3,
            6, 2, 3,
            8, 6, 3,
            9, 8, 3,
            // 5 adjacent faces
            5, 9, 4,
            11, 4, 2,
            10, 2, 6,
            7, 6, 8,
            1, 8, 9 };
    }
    public class Cube {
        public readonly static Vector3[] Verticies = new Vector3[]{
            new Vector3(1, 1, -1),
            new Vector3(1, 1, 1),
            new Vector3(-1, 1, 1),
            new Vector3(-1, 1, -1),
            
            new Vector3(1, -1, -1),
            new Vector3(1, -1, 1),
            new Vector3(-1, -1, 1),
            new Vector3(-1, -1, -1)
        };
        public readonly static int[] Indicies = new int[]{
            0, 1, 5, // left
            0, 5, 4,

            3, 7, 6, // right
            3, 6, 2,

            0, 2, 1, // top
            0, 3, 2,

            4, 6, 5, // bottom
            4, 7, 6,

            2, 5, 1, // back
            2, 6, 5,

            3, 0, 4, // front
            3, 4, 7
        };
    }
    struct Wave {
        public float A;
        public float Q;
        public Vector2 D;
        public float w;
        public float p;
        public Wave(float A, float Q, Vector2 D, float w, float p) {
            this.A = A;
            this.Q = Q;
            this.D = D;
            this.w = w;
            this.p = p;
        }
    }
    class Body {
        public static List<Body> Bodies = new List<Body>();

        public VertexPositionColorNormal[] VertexBuffer;
        public Dictionary<int, int[]> IndexBuffer;
        public VertexPositionColorPolar[] WaterVertexBuffer;
        public VertexPositionColorNormal[] AtmosphereVertexBuffer;

        public Vector3 Position;
        public Vector3 Rotation;
        public Vector3 AngularVelocity;
        public Vector3 Velocity;
        public float Radius;
        public float AtmosphereHeight;
        public float landHeightMultiplier;
        public float Mass;
        public float Time = 0;

        public float Brightness = 1;

        public RenderTarget2D scatterTarg;
        public RenderTarget2D bloomTarg;

        bool built;
        bool building;
        public Thread genThread;

        Wave[] waves;
        public bool HasWater;
        public bool HasAtmosphere;

        public bool Smooth;

        public bool IsStar;
        
        public Color landColor;
        public Color oceanFloorColor;
        public Color waterColor;
        public Color atmosphereColor;

        List<int> LODv;
        int lod;
        public int LODlevel {
            get {
                return lod;
            }
            set {
                if (value < 0)
                    lod = 0;
                else if (value >= LODv.Count)
                    lod = LODv.Count - 1;
                else
                    lod = value;
            }
        }

        public Body() {
            built = false;
            Position = Vector3.Zero;
            Radius = 1f;
            waves = new Wave[8];
            HasWater = true;
            landColor = Color.Gray;
            oceanFloorColor = Color.Black;
            waterColor = Color.DarkGray;
            atmosphereColor = Color.White;
            landHeightMultiplier = 1f;
            Mass = 1f;
            IsStar = false;
        }

        void generate(object ctx){
            building = true;

            List<int> inds = new List<int>(Icosphere.Indicies);
            List<VertexPositionColorNormal> verts = new List<VertexPositionColorNormal>();
            for (int i = 0; i < Icosphere.Verticies.Length; i++) {
                verts.Add(new VertexPositionColorNormal(Icosphere.Verticies[i] / Icosphere.Verticies[i].Length(), Icosphere.Verticies[i], landColor));
            }

            IndexBuffer = new Dictionary<int, int[]>();
            IndexBuffer[0] = inds.ToArray();

            LODv = new List<int>();
            LODv.Add(verts.Count);

            int levels = (int)ctx;
            for (int l = 1; l < levels; l++) {
                List<int> newinds = new List<int>();

                for (int i = 0; i < inds.Count; i+=3) {
                    int i1 = inds[i], i2 = inds[i + 1], i3 = inds[i + 2];
                    Vector3 v1 = verts[i1].Position, v2 = verts[i2].Position, v3 = verts[i3].Position;
                    Vector3 v4 = (v1 + v3) / 2f;
                    Vector3 v5 = (v1 + v2) / 2f;
                    Vector3 v6 = (v3 + v2) / 2f;
                    v4.Normalize(); v5.Normalize(); v6.Normalize();

                    verts.Add(new VertexPositionColorNormal(v4, v4, landColor));
                    verts.Add(new VertexPositionColorNormal(v5, v5, landColor));
                    verts.Add(new VertexPositionColorNormal(v6, v6, landColor));

                    int i4 = verts.Count - 3;
                    int i5 = verts.Count - 2;
                    int i6 = verts.Count - 1;

                    newinds.AddRange(new int[] { i1, i5, i4 });
                    newinds.AddRange(new int[] { i4, i5, i6 });
                    newinds.AddRange(new int[] { i3, i4, i6 });
                    newinds.AddRange(new int[] { i6, i5, i2 });
                }
                inds = new List<int>(newinds);
                IndexBuffer[l] = inds.ToArray();
                LODv.Add(verts.Count);
            }

            VertexBuffer = verts.ToArray();
            if (HasWater)
                WaterVertexBuffer = new VertexPositionColorPolar[VertexBuffer.Length];
            if (HasAtmosphere)
                AtmosphereVertexBuffer = new VertexPositionColorNormal[VertexBuffer.Length];
            // set terrain heights
            for (int i = 0; i < VertexBuffer.Length; i++) {
                // water verts
                if (HasWater){
                    WaterVertexBuffer[i] = new VertexPositionColorPolar(VertexBuffer[i].Position, Vector2.Zero, waterColor);
                    WaterVertexBuffer[i].Polar.X = (float)Math.Asin(WaterVertexBuffer[i].Position.Z);
                    WaterVertexBuffer[i].Polar.Y = (float)Math.Atan2(WaterVertexBuffer[i].Position.Y, WaterVertexBuffer[i].Position.X);
                    WaterVertexBuffer[i].Position *= Radius;
                }
                // atmo verts
                if (HasAtmosphere) {
                    AtmosphereVertexBuffer[i] = new VertexPositionColorNormal(VertexBuffer[i].Position, VertexBuffer[i].Position, atmosphereColor);
                    AtmosphereVertexBuffer[i].Normal.Normalize();
                    AtmosphereVertexBuffer[i].Position *= AtmosphereHeight;
                }

                // land verts
                VertexBuffer[i].Color = landColor;
                float h = getHeight(VertexBuffer[i].Position);
                if (h <= Radius)
                    VertexBuffer[i].Color = oceanFloorColor;
                VertexBuffer[i].Position *= h;
                VertexBuffer[i].Normal = Vector3.Zero;
            }
            for (int i = 0; i < IndexBuffer[levels - 1].Length; i+=3) {
                int i1 = IndexBuffer[levels - 1][i];
                int i2 = IndexBuffer[levels - 1][i + 1];
                int i3 = IndexBuffer[levels - 1][i + 2];
                Vector3 norm = -Vector3.Cross(VertexBuffer[i1].Position - VertexBuffer[i2].Position, VertexBuffer[i1].Position - VertexBuffer[i3].Position);
                VertexBuffer[i1].Normal += norm;
                VertexBuffer[i2].Normal += norm;
                VertexBuffer[i3].Normal += norm;
            }
            for (int i = 0; i < VertexBuffer.Length; i++)
                VertexBuffer[i].Normal.Normalize();
            
            if (HasWater){
                waves[0] = new Wave(.5f, 2, Vector2.UnitX, 50, 1f);
                waves[1] = new Wave(.2f, 2, Vector2.UnitY, 50, .2f);
            }

            built = true;
            building = false;
        }

        float sc = 1.25f;
        float getHeight(Vector3 pos) {
            float n = Noise.Generate(pos.X * sc, pos.Y * sc, pos.Z * sc);
            if (n > 0) {
                float lat = Math.Abs((float)Math.Asin(pos.Z));
                float lon = Math.Abs((float)Math.Atan2(pos.Y, pos.X));
                float n2 = (Noise.Generate(lat * sc * 1.25f, lon * sc * 1.25f) + 1f);
                if (Smooth)
                    n *= n2;
                else
                    n = .5f * n2;
            } else {
                if (!Smooth)
                    n = -.25f;
            }
            float h = (Radius * .9f) + (Radius * .1f * landHeightMultiplier) * (n + 1f);

            return h;
        }

        public void Gen(int detail) {
            if (!building)
                generate(detail);
        }

        public void GenAsync(int detail) {
            if (!building) {
                building = true;
                genThread = new Thread(generate);
                genThread.Start(detail);
            }
        }

        #region waves
        float[] getWaveA() {
            float[] a = new float[8];
            for (int i = 0; i < 8; i++)
                a[i] = waves[i].A;
            return a;
        }
        float[] getWaveQ() {
            float[] q = new float[8];
            for (int i = 0; i < 8; i++)
                q[i] = waves[i].Q;
            return q;
        }
        Vector2[] getWaveD() {
            Vector2[] d = new Vector2[8];
            for (int i = 0; i < 8; i++)
                d[i] = waves[i].D;
            return d;
        }
        float[] getWaveW() {
            float[] w = new float[8];
            for (int i = 0; i < 8; i++)
                w[i] = waves[i].w;
            return w;
        }
        float[] getWaveP() {
            float[] p = new float[8];
            for (int i = 0; i < 8; i++)
                p[i] = waves[i].p;
            return p;
        }
        #endregion

        public void Render(GraphicsDevice device, SpriteBatch batch, Effect effect, bool occuld=false) {
            if (built) {
                Matrix world = Matrix.CreateRotationX(Rotation.X) * Matrix.CreateRotationY(Rotation.Y) * Matrix.CreateRotationZ(Rotation.Z) * Matrix.CreateTranslation(Position);

                if (IsStar) {
                    effect.Parameters["World"].SetValue(world);
                    foreach (EffectPass p in effect.CurrentTechnique.Passes) {
                        p.Apply();
                        device.DrawUserIndexedPrimitives<VertexPositionColorNormal>(PrimitiveType.TriangleList, VertexBuffer, 0, LODv[lod], IndexBuffer[lod], 0, IndexBuffer[lod].Length / 3, VertexPositionColorNormal.VertexDeclaration);
                    }
                } else {
                    effect.Parameters["World"].SetValue(world);
                    effect.Parameters["WorldTranspose"].SetValue(Matrix.Invert(Matrix.Transpose(world)));

                    if (HasAtmosphere && !occuld) {
                        device.DepthStencilState = DepthStencilState.DepthRead;
                        effect.CurrentTechnique = effect.Techniques["Atmosphere"];

                        foreach (EffectPass p in effect.CurrentTechnique.Passes) {
                            p.Apply();
                            device.DrawUserIndexedPrimitives<VertexPositionColorNormal>(PrimitiveType.TriangleList, AtmosphereVertexBuffer, 0, LODv[lod], IndexBuffer[lod], 0, IndexBuffer[lod].Length / 3, VertexPositionColorNormal.VertexDeclaration);
                        }
                        device.DepthStencilState = DepthStencilState.Default;
                    }

                    effect.CurrentTechnique = effect.Techniques["Land"];
                    foreach (EffectPass p in effect.CurrentTechnique.Passes) {
                        p.Apply();
                        device.DrawUserIndexedPrimitives<VertexPositionColorNormal>(PrimitiveType.TriangleList, VertexBuffer, 0, LODv[lod], IndexBuffer[lod], 0, IndexBuffer[lod].Length / 3, VertexPositionColorNormal.VertexDeclaration);
                    }

                    if (HasWater) {
                        /*effect.Parameters["Time"].SetValue(Time);
                        effect.Parameters["waveA"].SetValue(getWaveA());
                        effect.Parameters["waveQ"].SetValue(getWaveQ());
                        effect.Parameters["waveD"].SetValue(getWaveD());
                        effect.Parameters["waveW"].SetValue(getWaveW());
                        effect.Parameters["waveP"].SetValue(getWaveP());*/
                        effect.CurrentTechnique = effect.Techniques["Water"];
                        foreach (EffectPass p in effect.CurrentTechnique.Passes) {
                            p.Apply();
                            device.DrawUserIndexedPrimitives<VertexPositionColorPolar>(PrimitiveType.TriangleList, WaterVertexBuffer, 0, LODv[lod], IndexBuffer[lod], 0, IndexBuffer[lod].Length / 3, VertexPositionColorPolar.VertexDeclaration);
                        }
                    }
                }
            }
        }
    }
}
