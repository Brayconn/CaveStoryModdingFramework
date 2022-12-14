using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace CaveStoryModdingFramework
{
    public static class BackgroundTypes
    {
        public static readonly ReadOnlyDictionary<long, string> BackgroundTypeList
            = new ReadOnlyDictionary<long, string>(new Dictionary<long, string>()
        {
            {0, BackgroundTypeNames.FixedToCamera },
            {1, BackgroundTypeNames.FollowSlowly },
            {2, BackgroundTypeNames.FixedToForeground },
            {3, BackgroundTypeNames.Water },
            {4, BackgroundTypeNames.NoDraw },
            {5, BackgroundTypeNames.ScrollItems },
            {6, BackgroundTypeNames.ParallaxItems },
            {7, BackgroundTypeNames.Parallax }
        });
    }
}
