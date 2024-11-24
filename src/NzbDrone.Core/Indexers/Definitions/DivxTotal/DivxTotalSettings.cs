using NzbDrone.Core.Indexers.Settings;

namespace NzbDrone.Core.Indexers.Definitions.DivxTotal
{
    public class DivxTotalSettings : NoAuthTorrentBaseSettings
    {
        public DivxTotalSettings()
        {
        }
    }

    internal static class DivxTotalCategories
    {
        public static string Peliculas => "peliculas";
        public static string PeliculasHd => "peliculas-hd";
        public static string Peliculas3D => "peliculas-3-d";
        public static string PeliculasDvdr => "peliculas-dvdr";
        public static string Series => "series";
        public static string Programas => "programas";
        public static string Otros => "otros";
    }

    internal static class DivxTotalFizeSizes
    {
        public static long Peliculas => 2147483648; // 2 GB
        public static long PeliculasDvdr => 5368709120; // 5 GB
        public static long Series => 536870912; // 512 MB
        public static long Otros => 536870912; // 512 MB
    }
}
