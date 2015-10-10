using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace PolarNoise {
    class Camera {
        public Vector3 Position;
        public Vector3 Rotation;
        public Matrix Projection { get; private set; }
        public Matrix View;
        public Vector3 offset;

        public Camera(float aspect) {
            Position = Vector3.Zero;
            Rotation = Vector3.Zero;
            setAspect(aspect);
            View = getView();
        }

        public void setAspect(float aspect) {
            Projection = Matrix.CreatePerspectiveFieldOfView(MathHelper.ToRadians(70f), aspect, .1f, 100000f);
        }

        public Matrix getView() {
            Matrix rot = Matrix.CreateRotationX(Rotation.X) * Matrix.CreateRotationY(Rotation.Y);
            return Matrix.CreateLookAt(offset + Position, offset + Position + Vector3.Transform(Vector3.Forward, rot), Vector3.Transform(Vector3.Up, rot));
        }

        public Matrix getRot() {
            return Matrix.CreateRotationX(Rotation.X) * Matrix.CreateRotationY(Rotation.Y);
        }
    }
}
