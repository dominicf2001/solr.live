public static class RoomUtils
{
    public static List<string> GetMusicKeywords()
    {
        return new List<string>
            {
                "music", "song", "track", "album", "lyrics", "melody", "beat", "rhythm", "instrumental",
                "audio", "cover", "remix", "DJ", "karaoke", "official", "studio",
                "single", "duet", "performance", "concert", "setlist", "mixtape", "EP", "LP", "record",
                "pop", "rock", "hip hop", "rap", "jazz", "classical", "blues", "reggae", "soul", "R&B",
                "country", "indie", "EDM", "metal", "folk", "punk", "dubstep", "trap", "house music",
                "electronic", "acoustic", "remastered", "unplugged", "live performance", "festival",
                "orchestra", "symphony", "session", "guitar", "piano", "drums", "bass", "synth", "violin",
                "cello", "trumpet", "saxophone", "flute", "background music", "relaxing", "chill", "party",
                "workout music", "study music", "sleep music", "motivational music", "gaming music",
                "dance music", "TikTok music", "viral", "playlist", "trending", "mashup", "bootleg",
                "fan edit", "sped up", "slowed", "reverb", "canción", "música", "chanson", "musik",
                "歌曲", "音楽", "Spotify", "Apple Music", "SoundCloud", "Pandora", "Deezer", "Bandcamp"
            };
    }

    public static string GenRandomUsername()
    {
        Random Random = new Random();

        List<string> Adjectives = new List<string>
        {
            "Brave", "Clever", "Swift", "Mighty", "Wise", "Fierce", "Gentle", "Noble", "Bold", "Curious",
            "Quiet", "Loyal", "Daring", "Fearless", "Friendly", "Witty", "Eager", "Kind", "Charming", "Jolly"
        };

        List<string> Nouns = new List<string>
        {
            "Lion", "Tiger", "Eagle", "Bear", "Fox", "Wolf", "Dragon", "Hawk", "Panther", "Falcon",
            "Raven", "Phoenix", "Shark", "Dolphin", "Otter", "Panda", "Gorilla", "Leopard", "Whale", "Unicorn"
        };
        string adjective = Adjectives[Random.Next(Adjectives.Count)];
        string noun = Nouns[Random.Next(Nouns.Count)];
        return $"{adjective} {noun}";

    }
}
