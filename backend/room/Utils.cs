using System.Diagnostics.Tracing;

sealed class HttpEventListener : EventListener
{
    protected override void OnEventSourceCreated(EventSource eventSource)
    {
        switch (eventSource.Name)
        {
            case "System.Net.Http":
                EnableEvents(eventSource, EventLevel.Informational, EventKeywords.All);
                break;

            case "System.Threading.Tasks.TplEventSource":
                const EventKeywords TasksFlowActivityIds = (EventKeywords)0x80;
                EnableEvents(eventSource, EventLevel.LogAlways, TasksFlowActivityIds);
                break;
        }

        base.OnEventSourceCreated(eventSource);
    }

    protected override void OnEventWritten(EventWrittenEventArgs eventData)
    {
        if (eventData.EventId == 1)
        {
            var scheme = (string)eventData.Payload[0];
            var host = (string)eventData.Payload[1];
            var port = (int)eventData.Payload[2];
            var pathAndQuery = (string)eventData.Payload[3];
            var versionMajor = (byte)eventData.Payload[4];
            var versionMinor = (byte)eventData.Payload[5];
            var policy = (HttpVersionPolicy)eventData.Payload[6];

            Console.WriteLine($"{eventData.ActivityId} {eventData.EventName} {scheme}://{host}:{port}{pathAndQuery} HTTP/{versionMajor}.{versionMinor}");
        }
        else if (eventData.EventId == 2)
        {
            Console.WriteLine(eventData.ActivityId + " " + eventData.EventName);
        }
    }
}

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
        Random random = new Random();
        List<string> musicAdjectives = new List<string>
    {
        "Melodic", "Rhythmic", "Harmonic", "Acoustic", "Electric", "Jazzy", "Funky", "Soulful", "Groovy", "Bluesy",
        "Classical", "Rockin'", "Poppin'", "Operatic", "Orchestral", "Symphonic", "Choral", "Instrumental", "Vocal", "Lyrical",
        "Brassy", "Percussive", "Synthy", "Bassy", "Treble", "Reggae", "Folky", "Country", "Hip-Hop", "Rap",
        "Techno", "House", "Trance", "Ambient", "Indie", "Alternative", "Punk", "Metal", "Grunge", "Emo",
        "Reggaeton", "Salsa", "Samba", "Bossa Nova", "Flamenco", "Tango", "Disco", "Dubstep", "EDM", "Lo-Fi",
        "Psychedelic", "Progressive", "Experimental", "Avant-garde", "Minimalist", "Baroque", "Renaissance", "Medieval", "Tribal", "World"
    };

        List<string> musicNouns = new List<string>
    {
        "Chord", "Melody", "Rhythm", "Beat", "Harmony", "Tune", "Song", "Ballad", "Anthem", "Lullaby",
        "Symphony", "Concerto", "Sonata", "Aria", "Opus", "Overture", "Fugue", "Etude", "Nocturne", "Rhapsody",
        "Scale", "Arpeggio", "Riff", "Hook", "Bridge", "Chorus", "Verse", "Refrain", "Coda", "Interlude",
        "Solo", "Duet", "Trio", "Quartet", "Ensemble", "Orchestra", "Band", "Choir", "Crescendo", "Diminuendo",
        "Forte", "Piano", "Allegro", "Adagio", "Andante", "Tempo", "Timbre", "Pitch", "Octave", "Interval",
        "Note", "Staff", "Clef", "Key", "Signature", "Measure", "Bar", "Rest", "Fermata", "Staccato",
        "Legato", "Vibrato", "Tremolo", "Glissando", "Pizzicato", "Portamento", "Falsetto", "Soprano", "Alto", "Tenor",
        "Bass", "Baritone", "Treble", "Contralto", "Countertenor", "Mezzo", "Castrato", "Coloratura", "Bel Canto", "Scat",
        "Guitar", "Piano", "Drums", "Violin", "Cello", "Flute", "Trumpet", "Saxophone", "Clarinet", "Harp",
        "Accordion", "Banjo", "Ukulele", "Harmonica", "Oboe", "Bassoon", "Tuba", "Trombone", "Xylophone", "Marimba",
        "Synthesizer", "Keyboard", "Bass", "Mandolin", "Sitar", "Tabla", "Didgeridoo", "Bagpipes", "Fiddle", "Lute",
        "Theremin", "Glockenspiel", "Vibraphone", "Timpani", "Bongos", "Congas", "Djembe", "Cajon", "Castanets", "Tambourine"
    };

        string adjective = musicAdjectives[random.Next(musicAdjectives.Count)];
        string noun = musicNouns[random.Next(musicNouns.Count)];

        int randomNumber = random.Next(5);

        return $"{adjective}{noun}";
    }

    public static string GenRandomAvatar()
    {
        string[] seeds = {
            "Destiny", "Aidan", "Adrian", "Christopher", "Alexander", "Aiden",
            "Easton", "Eden", "Chase", "Amaya", "Avery", "Brooklynn", "Caleb",
            "Christian", "Andrea", "Brian", "Eliza", "Emery", "George", "Jessica"
        };

        Random random = new Random();
        int index = random.Next(seeds.Length);

        string seed = seeds[index];
        return $"https://api.dicebear.com/9.x/adventurer/svg?seed=${seed}&hair=long10,short19,short18,short17,short16,short15,short14,long01,long02,short10,short09,short08,short11,short12,short13,short03,short02,short01,long24,long23,long22,long15,long14&skinColor=f2d3b1,ecad80&backgroundColor=c0aede,d1d4f9,ffd5dc,ffdfbf,b6e3f4";
    }
}
