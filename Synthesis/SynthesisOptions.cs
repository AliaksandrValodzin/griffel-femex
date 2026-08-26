namespace griffel_femex.Synthesis
{
    /// <summary>
    /// The two tolerances <see cref="GeometrySynthesis"/> clusters with, for the
    /// caller who has measured its program and knows better than the default.
    ///
    /// Both are null by default, meaning "derive it from the finished extent". A
    /// number here is <b>absolute</b>, in the model's own length unit, and is the
    /// caller taking responsibility for the thing <c>FEMEX_Adapters.md</c> §6.1
    /// warns about: an absolute millimetre means something different in a metre
    /// model and a millimetre one.
    /// </summary>
    public sealed class SynthesisOptions
    {
        /// <summary>
        /// How close an incoming elevation has to be to an existing
        /// <see cref="Geometry.Level"/> to be snapped to it rather than to create
        /// one. Null derives it from the vertical extent of everything collected.
        /// </summary>
        public double? LevelTolerance { get; set; }

        /// <summary>
        /// How close two incoming points have to be to become one node. Null derives
        /// it from the bounding diagonal of everything collected, which is what
        /// <see cref="FemexModel.GetCoincidenceTolerance"/> does over the nodes a
        /// model already has.
        /// </summary>
        public double? NodeTolerance { get; set; }

        /// <summary>
        /// What to call a level nobody asked for. The elevation is appended, so the
        /// default reads "Level +3.500". Named rather than left null because a
        /// storey the native model did not have is exactly the thing a reader of the
        /// converted model will want to find and question.
        /// </summary>
        public string? InventedLevelNamePrefix { get; set; } = "Level";
    }
}
