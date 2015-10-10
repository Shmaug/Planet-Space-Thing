using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace PolarNoise {
    public class Main : Microsoft.Xna.Framework.Game {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;

        public int WindowWidth;
        public int WindowHeight;

        Body earth;
        Body moon;
        Body star;
        Body star2;
        Camera camera;

        VertexPositionColor[] SkyVerts = new VertexPositionColor[8];

        TextureCube skybox;
        Effect worldfx;
        Effect skyfx;
        Effect starfx;
        Effect bloomfx;
        SpriteFont font;

        RenderTarget2D bloomTarg;
        RenderTarget2D postTarg;
        RenderTarget2D sceneTarg;

        Vector2[] blurOffsetX;
        Vector2[] blurOffsetY;

        MouseState ms, lastms;
        KeyboardState ks, lastks;

        public Main() {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
            graphics.PreferMultiSampling = true;
            System.Windows.Forms.Form form = (System.Windows.Forms.Form)System.Windows.Forms.Form.FromHandle(Window.Handle);
            form.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            form.MaximizeBox = true;
            form.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            form.Resize += (object sender, EventArgs e) => {
                WindowWidth = form.Width;
                WindowHeight = form.Height;
                if (camera != null)
                    camera.setAspect(WindowWidth / (float)WindowHeight);

                bloomTarg = new RenderTarget2D(GraphicsDevice, WindowWidth, WindowHeight, false, GraphicsDevice.PresentationParameters.BackBufferFormat, GraphicsDevice.PresentationParameters.DepthStencilFormat);
                postTarg = new RenderTarget2D(GraphicsDevice, WindowWidth, WindowHeight, false, GraphicsDevice.PresentationParameters.BackBufferFormat, GraphicsDevice.PresentationParameters.DepthStencilFormat);
                sceneTarg = new RenderTarget2D(GraphicsDevice, WindowWidth, WindowHeight, false, GraphicsDevice.PresentationParameters.BackBufferFormat, GraphicsDevice.PresentationParameters.DepthStencilFormat);

                if (starfx != null) {
                    float[] kernel;
                    ComputeOffsets(7, bloomTarg.Width, bloomTarg.Height, out blurOffsetX, out blurOffsetY);
                    ComputeKernel(7, 5, out kernel);

                    bloomfx.Parameters["weights"].SetValue(kernel);
                }
            };
            form.WindowState = System.Windows.Forms.FormWindowState.Maximized;
        }

        protected override void Initialize() {
            earth = new Body();
            earth.Position = new Vector3(0, 0, 0);
            earth.Velocity = new Vector3(4, 0, 0);
            earth.Radius = 100;
            earth.AtmosphereHeight = 130;
            earth.atmosphereColor = Color.Blue;
            earth.landColor = Color.Green;
            earth.oceanFloorColor = Color.SandyBrown;
            earth.waterColor = Color.SteelBlue;
            earth.atmosphereColor = Color.SkyBlue;
            earth.Mass = 100f;
            earth.AngularVelocity = new Vector3(0, MathHelper.TwoPi * .001f, 0);
            earth.HasAtmosphere = true;
            earth.GenAsync(6);
            Body.Bodies.Add(earth);

            moon = new Body();
            moon.Radius = 40;
            moon.Mass = 1f;
            moon.Position = earth.Position + new Vector3(1000, 0, 0);
            moon.Velocity = new Vector3(0, 0, 3) + earth.Velocity;
            moon.landColor = Color.Gray;
            moon.oceanFloorColor = Color.DarkGray;
            moon.HasWater = false;
            moon.Smooth = true;
            moon.landHeightMultiplier = .3f;
            moon.AngularVelocity = new Vector3(0, -MathHelper.TwoPi * .001f, 0);
            moon.GenAsync(6);
            Body.Bodies.Add(moon);

            star = new Body();
            star.Radius = 1000;
            star.Mass = 5000f;
            star.Position = new Vector3(0, 0, 30000);
            star.Velocity = new Vector3(0, 0, 0);
            star.landColor = Color.LightYellow;
            star.oceanFloorColor = Color.LightYellow;
            star.HasWater = false;
            star.landHeightMultiplier = 0;
            star.IsStar = true;
            star.Brightness = .75f;
            star.GenAsync(6);
            Body.Bodies.Add(star);

            camera = new Camera(WindowWidth / (float)WindowHeight);
            camera.Position = new Vector3(0, 30, 500);

            for (int i = 0; i < 8; i++)
                SkyVerts[i] = new VertexPositionColor(Cube.Verticies[i], Color.White);

            base.Initialize();
        }

        protected override void LoadContent() {
            spriteBatch = new SpriteBatch(GraphicsDevice);

            worldfx = Content.Load<Effect>("fx/world");
            skyfx = Content.Load<Effect>("fx/sky");
            starfx = Content.Load<Effect>("fx/star");
            bloomfx = Content.Load<Effect>("fx/bloom");
            skybox = Content.Load<TextureCube>("sky/bg2");
            font = Content.Load<SpriteFont>("font");
        }

        protected override void UnloadContent() {

        }

        float camVel = 50f;
        protected override void Update(GameTime gameTime) {
            ks = Keyboard.GetState();
            ms = Mouse.GetState();

            #region planet physics update
            float dT = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (ks.IsKeyDown(Keys.RightShift))
                dT *= 500;
            foreach (Body b in Body.Bodies) {
                float dist = Vector3.Distance(camera.Position + camera.offset, b.Position);
                b.LODlevel = Math.Max(6 - (int)((dist - b.Radius) / (b.Radius * 5)), 2);

                b.Time += (float)gameTime.ElapsedGameTime.TotalSeconds;

                foreach (Body b2 in Body.Bodies) {
                    if (b2 != b) {
                        float m = 100f * (b.Mass * b2.Mass / Vector3.DistanceSquared(b.Position, b2.Position));
                        Vector3 f = (b2.Position - b.Position);
                        f.Normalize();
                        f *= m;
                        b.Velocity += (f / b.Mass) * dT;
                    }
                }

                b.Position += b.Velocity * dT;
                b.Rotation += b.AngularVelocity * dT;
            }
            #endregion
            #region camera look
            if (!IsMouseVisible) {
                int cx = WindowWidth / 2, cy = WindowHeight / 2;
                if (ms.X != lastms.X) {
                    if (Math.Abs(ms.X - cx) < 100) {
                        float d = (ms.X - cx) * (float)gameTime.ElapsedGameTime.TotalSeconds * .25f;
                        camera.Rotation.Y -= d;
                    }
                }
                if (ms.Y != lastms.Y) {
                    if (Math.Abs(ms.Y - cy) < 100) {
                        float d = (ms.Y - cy) * (float)gameTime.ElapsedGameTime.TotalSeconds * .25f;
                        camera.Rotation.X -= d;
                    }
                }
                if (ks.IsKeyDown(Keys.Q))
                    camera.Rotation.Z += (float)gameTime.ElapsedGameTime.TotalSeconds * MathHelper.PiOver4;
                else if (ks.IsKeyDown(Keys.E))
                    camera.Rotation.Z -= (float)gameTime.ElapsedGameTime.TotalSeconds * MathHelper.PiOver4;

                Mouse.SetPosition(cx, cy);
            }

            if (ks.IsKeyDown(Keys.LeftAlt) && !lastks.IsKeyDown(Keys.LeftAlt))
                IsMouseVisible = !IsMouseVisible;
            #endregion
            #region camera move
            Vector3 move = Vector3.Zero;
            if (ks.IsKeyDown(Keys.W))
                move += Vector3.Forward;
            else if (ks.IsKeyDown(Keys.S))
                move += Vector3.Backward;
            if (ks.IsKeyDown(Keys.A))
                move += Vector3.Left;
            else if (ks.IsKeyDown(Keys.D))
                move += Vector3.Right;

            if (move != Vector3.Zero) {
                move.Normalize();
                move *= camVel;
                camVel += 50 * (float)gameTime.ElapsedGameTime.TotalSeconds;
                if (camVel > 500f)
                    camVel = 500f;
            } else
                camVel = 50f;

            if (ks.IsKeyDown(Keys.LeftShift))
                move *= 10f;

            move = Vector3.Transform(move, Matrix.CreateRotationX(camera.Rotation.X) * Matrix.CreateRotationY(camera.Rotation.Y));
            move *= (float)gameTime.ElapsedGameTime.TotalSeconds;
            camera.Position += move;

            camera.View = camera.getView();
            
            #endregion

            lastks = ks;
            lastms = ms;
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime) {
            GraphicsDevice.BlendState = BlendState.Opaque;
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            GraphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

            GraphicsDevice.SetRenderTarget(sceneTarg);
            GraphicsDevice.Clear(Color.Black);

            #region skybox
            skyfx.Parameters["tex"].SetValue(skybox);
            skyfx.Parameters["W"].SetValue(Matrix.Identity);
            skyfx.Parameters["V"].SetValue(Matrix.Invert(camera.getRot()));
            skyfx.Parameters["P"].SetValue(camera.Projection);
            skyfx.Parameters["CamPos"].SetValue(camera.View.Translation);
            GraphicsDevice.DepthStencilState = DepthStencilState.None;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            foreach (EffectPass p in skyfx.CurrentTechnique.Passes){
                p.Apply();
                GraphicsDevice.DrawUserIndexedPrimitives<VertexPositionColor>(PrimitiveType.TriangleList, SkyVerts, 0, 8, Cube.Indicies, 0, 12);
            }
            GraphicsDevice.DepthStencilState = DepthStencilState.Default;
            #endregion

            #region scene draw
            Vector4[] lights = new Vector4[4];
            int li = 0;
            foreach (Body b in Body.Bodies) {
                if (b.IsStar) {
                    lights[li] = new Vector4(b.Position, b.Brightness);
                    li++;
                    if (li > 3)
                        break;
                }
            }
            worldfx.Parameters["StarLights"].SetValue(lights);
            worldfx.Parameters["CameraPos"].SetValue(camera.Position + camera.offset);

            worldfx.Parameters["View"].SetValue(camera.View);
            worldfx.Parameters["Projection"].SetValue(camera.Projection);
            starfx.Parameters["View"].SetValue(camera.View);
            starfx.Parameters["Projection"].SetValue(camera.Projection);

            GraphicsDevice.RasterizerState = new RasterizerState() { FillMode = FillMode.Solid };
            if (ks.IsKeyDown(Keys.F))
                GraphicsDevice.RasterizerState = new RasterizerState() { FillMode = FillMode.WireFrame };

            worldfx.Parameters["OcculdMap"].SetValue(false);
            // draw worlds
            foreach (Body b in Body.Bodies)
                if (b.IsStar)
                    b.Render(GraphicsDevice, spriteBatch, starfx);
                else {
                    b.Render(GraphicsDevice, spriteBatch, worldfx);
                }

            // draw each star to its render target
            foreach (Body b in Body.Bodies)
                if (b.IsStar) {
                    if (b.scatterTarg == null || b.scatterTarg.Width != WindowWidth || b.scatterTarg.Height != WindowHeight)
                        b.scatterTarg = new RenderTarget2D(GraphicsDevice, WindowWidth, WindowHeight, false, GraphicsDevice.PresentationParameters.BackBufferFormat, GraphicsDevice.PresentationParameters.DepthStencilFormat);
                    if (b.bloomTarg == null || b.bloomTarg.Width != WindowWidth || b.bloomTarg.Height != WindowHeight)
                        b.bloomTarg = new RenderTarget2D(GraphicsDevice, WindowWidth, WindowHeight, false, GraphicsDevice.PresentationParameters.BackBufferFormat, GraphicsDevice.PresentationParameters.DepthStencilFormat);

                    GraphicsDevice.SetRenderTarget(b.bloomTarg);
                    GraphicsDevice.Clear(Color.Transparent);
                    // draw occulders
                    worldfx.Parameters["OcculdMap"].SetValue(true);
                    foreach (Body w in Body.Bodies)
                        if (!w.IsStar)
                            w.Render(GraphicsDevice, spriteBatch, worldfx, true);
                    // draw star
                    b.Render(GraphicsDevice, spriteBatch, starfx);
                }
            #endregion

            #region bloom
            foreach (Body b in Body.Bodies)
                if (b.IsStar) {
                    // draw x-blurred bloom to posttarg
                    GraphicsDevice.SetRenderTarget(postTarg);
                    GraphicsDevice.Clear(Color.Transparent);
                    bloomfx.CurrentTechnique = bloomfx.Techniques["Blur"];
                    bloomfx.Parameters["offsets"].SetValue(blurOffsetX);
                    spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, null, bloomfx);
                    spriteBatch.Draw(b.bloomTarg, Vector2.Zero, Color.White);
                    spriteBatch.End();

                    // draw y-blurred blur to star's bloom targ
                    GraphicsDevice.SetRenderTarget(b.bloomTarg);
                    GraphicsDevice.Clear(Color.Transparent);
                    bloomfx.CurrentTechnique = bloomfx.Techniques["Blur"];
                    bloomfx.Parameters["offsets"].SetValue(blurOffsetY);
                    spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, null, bloomfx);
                    spriteBatch.Draw(postTarg, Vector2.Zero, Color.White);
                    spriteBatch.End();
                }
            #endregion

            #region scatter
            foreach (Body b in Body.Bodies) {
                if (b.IsStar) {
                    GraphicsDevice.SetRenderTarget(b.scatterTarg);
                    GraphicsDevice.Clear(Color.Transparent);

                    float dist = Vector3.Distance(camera.Position + camera.offset, b.Position);
                    float w = MathHelper.Clamp((dist-10*b.Radius) / (15*b.Radius), .01f, 1f);

                    Vector3 lp = GraphicsDevice.Viewport.Project(b.Position, camera.Projection, camera.View, Matrix.Identity);
                    bloomfx.Parameters["lightPosition"].SetValue(new Vector2(lp.X, lp.Y) / new Vector2(b.scatterTarg.Width, b.scatterTarg.Height));
                    bloomfx.Parameters["Weight"].SetValue(w);
                    bloomfx.CurrentTechnique = bloomfx.Techniques["Scatter"];
                    spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, null, null, null, bloomfx);
                    spriteBatch.Draw(b.bloomTarg, Vector2.Zero, Color.White);
                    spriteBatch.End();
                }
            }
            #endregion

            #region final
            GraphicsDevice.SetRenderTarget(bloomTarg);
            GraphicsDevice.Clear(Color.Transparent);
            foreach (Body b in Body.Bodies) {
                if (b.IsStar) {
                    spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null);
                    spriteBatch.Draw(b.scatterTarg, Vector2.Zero, Color.White);
                    spriteBatch.End();
                }
            }

            GraphicsDevice.SetRenderTarget(null);
            GraphicsDevice.Clear(Color.Black);
            GraphicsDevice.Textures[1] = bloomTarg;
            bloomfx.CurrentTechnique = bloomfx.Techniques["Combine"];
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null, bloomfx);
            spriteBatch.Draw(sceneTarg, Vector2.Zero, Color.White);
            spriteBatch.End();
            #endregion

            #region UI
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend);

            spriteBatch.End();
            #endregion

            base.Draw(gameTime);
        }

        void ComputeKernel(int blurRadius, float blurAmount, out float[] kernel) {
            float amount = blurAmount;

            kernel = new float[blurRadius * 2 + 1];
            float sigma = blurRadius / amount;

            float twoSigmaSquare = 2.0f * sigma * sigma;
            float sigmaRoot = (float)Math.Sqrt(twoSigmaSquare * Math.PI);
            float total = 0.0f;
            float distance = 0.0f;
            int index = 0;

            for (int i = -blurRadius; i <= blurRadius; ++i) {
                distance = i * i;
                index = i + blurRadius;
                kernel[index] = (float)Math.Exp(-distance / twoSigmaSquare) / sigmaRoot;
                total += kernel[index];
            }

            for (int i = 0; i < kernel.Length; ++i)
                kernel[i] /= total;
        }

        public void ComputeOffsets(int blurRadius, float textureWidth, float textureHeight, out Vector2[] horiz, out Vector2[] vert) {
            Vector2[] offsetsHoriz = new Vector2[blurRadius * 2 + 1];
            Vector2[] offsetsVert = new Vector2[blurRadius * 2 + 1];

            int index = 0;
            float xOffset = 1f / textureWidth;
            float yOffset = 1f / textureHeight;

            for (int i = -blurRadius; i <= blurRadius; ++i) {
                index = i + blurRadius;
                offsetsHoriz[index] = new Vector2(i * xOffset, 0.0f);
                offsetsVert[index] = new Vector2(0.0f, i * yOffset);
            }

            horiz = offsetsHoriz;
            vert = offsetsVert;
        }
    }
}
