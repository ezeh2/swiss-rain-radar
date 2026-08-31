namespace SwissRainRadar.Web.Services;

/// <summary>
/// Approximate WGS84 to Swiss LV95 conversion published by swisstopo.
/// The approximation is sufficiently accurate for a one-kilometre radar grid.
/// </summary>
public static class SwissProjection
{
    public static (double Easting, double Northing) ToLv95(double latitude, double longitude)
    {
        var latitudeAux = ((latitude * 3600) - 169_028.66) / 10_000;
        var longitudeAux = ((longitude * 3600) - 26_782.5) / 10_000;

        var easting = 2_600_072.37
            + (211_455.93 * longitudeAux)
            - (10_938.51 * longitudeAux * latitudeAux)
            - (0.36 * longitudeAux * Math.Pow(latitudeAux, 2))
            - (44.54 * Math.Pow(longitudeAux, 3));

        var northing = 1_200_147.07
            + (308_807.95 * latitudeAux)
            + (3_745.25 * Math.Pow(longitudeAux, 2))
            + (76.63 * Math.Pow(latitudeAux, 2))
            - (194.56 * Math.Pow(longitudeAux, 2) * latitudeAux)
            + (119.79 * Math.Pow(latitudeAux, 3));

        return (easting, northing);
    }
}

