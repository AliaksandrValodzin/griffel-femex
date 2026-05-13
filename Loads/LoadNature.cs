namespace griffel_femex.Loads
{
    /// <summary>
    /// Defines the category of the load, which dictates partial safety factors 
    /// and combination rules in structural design codes (e.g., ASCE 7, Eurocodes).
    /// </summary>
    public enum LoadNature
    {
        Dead,
        Live,
        Wind,
        Seismic,
        Snow,
        Accidental,
        Temperature
    }
}
