namespace BingWallpapers
{
    internal struct ImageMetadata
    {
        internal readonly string Name;
        internal readonly string Description;
        internal readonly string Url;

        internal ImageMetadata(string url, string name, string description)
        {
            Url = url;
            Name = name;
            Description = description;
        }
    }
}
