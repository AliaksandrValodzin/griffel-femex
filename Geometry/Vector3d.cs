namespace griffel_femex.Geometry
{
    /// <summary>
    /// A vector in global coordinates. Never serialized and never stored on an
    /// entity: it exists so that the local-axis and load-direction helpers in
    /// <c>FemexModel.LocalAxes.cs</c> can return a direction instead of three
    /// <c>out double</c>s each.
    ///
    /// The global frame is right-handed with <b>Z up</b> —
    /// <see cref="Level.AbsoluteElevation"/> is global Z and increases upward.
    /// Worth saying out loud, because RFEM's global Z points down by default and
    /// that is the commonest source of translator bugs against a Z-up format.
    /// </summary>
    public readonly record struct Vector3d(double X, double Y, double Z)
    {
        /// <summary>The zero vector, which is also what <see cref="Normalized"/> answers for itself.</summary>
        public static Vector3d Zero => new Vector3d(0.0, 0.0, 0.0);

        public static Vector3d UnitX => new Vector3d(1.0, 0.0, 0.0);

        public static Vector3d UnitY => new Vector3d(0.0, 1.0, 0.0);

        public static Vector3d UnitZ => new Vector3d(0.0, 0.0, 1.0);

        public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

        /// <summary>
        /// The unit vector in the same direction, or <see cref="Zero"/> when there
        /// is no direction to speak of. Callers that care tell the two apart by
        /// checking <see cref="Length"/> first — no helper here throws.
        /// </summary>
        public Vector3d Normalized()
        {
            double length = Length;
            if (length <= 0.0 || double.IsNaN(length) || double.IsInfinity(length))
                return Zero;

            return new Vector3d(X / length, Y / length, Z / length);
        }

        public double Dot(Vector3d other) => X * other.X + Y * other.Y + Z * other.Z;

        public Vector3d Cross(Vector3d other)
        {
            return new Vector3d(
                Y * other.Z - Z * other.Y,
                Z * other.X - X * other.Z,
                X * other.Y - Y * other.X);
        }

        public static Vector3d operator +(Vector3d a, Vector3d b) => new Vector3d(a.X + b.X, a.Y + b.Y, a.Z + b.Z);

        public static Vector3d operator -(Vector3d a, Vector3d b) => new Vector3d(a.X - b.X, a.Y - b.Y, a.Z - b.Z);

        public static Vector3d operator *(Vector3d v, double scale) => new Vector3d(v.X * scale, v.Y * scale, v.Z * scale);

        public static Vector3d operator *(double scale, Vector3d v) => v * scale;
    }
}
