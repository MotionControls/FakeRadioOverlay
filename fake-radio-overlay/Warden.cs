using Godot;
using System;
using System.IO;
using System.Collections.Generic;

public class MusicContext
{
    public string name = "PLACEHOLDER NAME";
    public string artist = "PLACEHOLDER ARTIST";
    public string album = "PLACEHOLDER ALBUM";
    public string path = null;
    public float skipChance = 0.0f;     // Chance of song being skipped over when choosing the queue.
    public double length = Double.MaxValue;

    public MusicContext(string _name, string _artist, string _album, string _path){
        name = _name;
        artist = _artist;
        album = _album;
        path = _path;

        AudioStream temp = GetAudioStream(path);
        length = temp.GetLength();
    }

    public static AudioStream GetAudioStream(string path){
        switch(path.GetExtension()){
            case "mp3":
                return AudioStreamMP3.LoadFromFile(path);
            case "wav":
                return AudioStreamWav.LoadFromFile(path);
        }
        return null;
    }
}

public class StationContext
{
    public string name = "PLACEHOLDER";
    public double position = 0.0d;
    public List<MusicContext> music = new List<MusicContext>();
    public int playing = 0;
    public int lastPlayed = 0;
    public Queue<int> queue = new Queue<int>();   // List of indices.

    public StationContext(string _name){
        name = _name;
    }
}

public partial class Warden : Node2D
{
    [Export] public string logPath = "logs";
    private Godot.FileAccess log;
    private Vector2 mouseVelocity = new Vector2();
    private Vector2 lastMousePos = new Vector2();
    
    // Text display.
    [Export] public RichTextLabel infoDisplay;
    [Export] public Button moveArea;
    [Export] public int textLengthRelax = 18;   // Number of characters that get displayed before the text stops scrolling.
    [Export] public double stepTime = 0.5d;     // Time until text scrolls by 1.
    [Export] public double stillTime = 5.0d;    // Time until text starts scrolling.
    [Export] public double infoTime = 3.0d;     // Time until swapping information.
    private bool moveWindow = false;
    private List<string> completeText = [];
    private int textIndex = 0;
    private double timer = 0.0d;        // General-use timer.
    private bool scroll = false;
    private bool stilled = false;

    // Stations.
    [Export] public string stationPath = "stations";
    [Export] public int queueSize = 3;
    private List<StationContext> stations = [];
    private int stationIndex = 0;

    // Music.
    [Export] public string musicPath = "music";
    [Export] public AudioStreamPlayer player;
    [Export] public Button pauseButton;
    [Export] public Button cycleButton;
    [Export] public HSlider volumeSlider;

    public override void _Ready(){
        base._Ready();
        GD.Randomize();
        
        string path;
        if(Engine.IsEmbeddedInEditor()){
            path = "res://";
            logPath += "/";
            musicPath += "/";
            stationPath += "/";
        }else{
            path = Path.GetDirectoryName(OS.GetExecutablePath()) + "\\";
            logPath += "\\";
            musicPath += "\\";
            stationPath += "\\";
        }
        GD.Print(path + logPath + DateTime.Now.ToString("dd.MMM.yyyy-HH.mm.ss") + ".log");
        log = Godot.FileAccess.Open(path + logPath + DateTime.Now.ToString("d.MMM.yyyy-h.mm.ss") + ".log", Godot.FileAccess.ModeFlags.Write);
        if(log == null){
            Console.Write("ERR: logs directory must exist.\n");
            GetTree().Quit(1);
        }

        if(log == null) GD.PrintErr("Log file could not be opened.");
        log.StoreString("...Init...\n");
        
        DirAccess dir = DirAccess.Open(path + stationPath);
        if(dir == null){
            log.StoreString("[ERR: WARDEN _Ready] stations directory must exist.");
            GetTree().Quit(1);
        }

        foreach(string filePath in dir.GetFiles()){
            if(Path.GetExtension(filePath) == ".json"){
                Godot.FileAccess file = Godot.FileAccess.Open(path + stationPath + filePath, Godot.FileAccess.ModeFlags.Read);
                Godot.Collections.Array<Godot.Collections.Dictionary<string, string>> arr = (Godot.Collections.Array<Godot.Collections.Dictionary<string, string>>)Json.ParseString(file.GetAsText());
                if(arr != null){
                    string stationName = Path.GetFileNameWithoutExtension(filePath);
                    log.StoreString("[WARDEN _Ready] Found station " + stationName + ".\n");
                    StationContext station = new StationContext(stationName);
                    
                    foreach(Godot.Collections.Dictionary<string, string> dict in arr){
                        string musicFilePath = path + musicPath + dict["path"];
                        Godot.FileAccess musicFile = Godot.FileAccess.Open(musicFilePath, Godot.FileAccess.ModeFlags.Read);
                        if(musicFile != null){
                            log.StoreString("[WARDEN _Ready] Found " + dict["path"] + ".\n");
                            
                            string musicName = dict["name"];
                            string musicArtist = dict["artist"];
                            string musicAlbum = dict["album"];
                            
                            station.music.Add(new MusicContext(musicName, musicArtist, musicAlbum, musicFilePath));
                        }else{
                            log.StoreString("[WARDEN _Ready] Couldn't open " + musicFilePath + ".\n");
                        }
                    }

                    InitialQueuePopulation(ref station);
                    stations.Add(station);
                }else{
                    GD.PrintErr("Failed to parse " + filePath);
                }

                file.Close();
            }
        }

        stationIndex = (int)(GD.Randi() % stations.Count);
        UpdatePlaying(stations[stationIndex]);

        NinePatchRect bevel = GetNode<NinePatchRect>("Overlay/Background");
        moveArea.Position = bevel.Position;
        moveArea.Size = bevel.Size;
        GetWindow().InitialPosition = Window.WindowInitialPosition.Absolute;

        player.Finished += () => HandleSongFinished();
        pauseButton.Pressed += () => {player.Playing = !player.Playing;};
        cycleButton.Pressed += () => HandleStationSwitch(1);
        volumeSlider.ValueChanged += (val) => {player.VolumeLinear = (float)val;};
        moveArea.ButtonDown += () => {moveWindow = true;};
        moveArea.ButtonUp += () => {moveWindow = false;};
    }

    public override void _PhysicsProcess(double delta){
        timer += delta;
        if(!scroll && timer >= stillTime){
            if(stilled){
                textIndex = (textIndex + 1) % completeText.Count;
                infoDisplay.Text = completeText[textIndex];
                stilled = false;
            }else{
                scroll = true;
            }
            timer = 0.0d;
        }

        if(scroll){
            if(infoDisplay.Text.Length < textLengthRelax && timer >= infoTime){
                timer = 0.0d;
                scroll = false;
                stilled = true;
            }else if(infoDisplay.Text.Length >= textLengthRelax && timer >= stepTime){
                timer = 0.0d;
                infoDisplay.Text = infoDisplay.Text.Right(1);
            }
        }
        
        Vector2 mousePos = DisplayServer.MouseGetPosition();
        mouseVelocity = mousePos - lastMousePos;
        lastMousePos = mousePos;
        
        if(player.Playing){
            for(int i = 0; i < stations.Count; i++){
                StationContext station = stations[i];
                
                if(stationIndex != i){
                    station.position += delta;
                    if(station.position >= station.music[station.playing].length){
                        SongFromQueue(station);
                    }
                }
            }
        }
    }

    public override void _Input(InputEvent @event){
        if(moveWindow && @event is InputEventMouseMotion evt){
            Vector2 pos = GetWindow().Position;
            pos += evt.ScreenRelative;
            GetWindow().Position = new Vector2I((int)pos.X, (int)pos.Y);
        }
    }

    private void HandleStationSwitch(int offset){
        stations[stationIndex].position = player.GetPlaybackPosition();
        stationIndex += offset;
        stationIndex = Mathf.Abs(stationIndex % stations.Count);
        UpdatePlaying(stations[stationIndex]);
    }

    private void HandleSongFinished(){
        SongFromQueue(stations[stationIndex]);
        UpdatePlaying(stations[stationIndex]);
    }
    
    private void SongFromQueue(StationContext station){
        station.lastPlayed = station.playing;
        station.playing = station.queue.Dequeue();
        station.position = 0.0d;
        log.StoreString("[WARDEN SongFromQueue] Playing " + station.music[station.playing].name + " on " + station.name + ".\n");
        GD.Print("[WARDEN SongFromQueue] Playing " + station.music[station.playing].name + " on " + station.name + ".");

        PickToQueue(ref station);
    }

    private void UpdatePlaying(StationContext station){
        player.Stream = MusicContext.GetAudioStream(station.music[station.playing].path);
        player.Play((float)station.position);
        GetDisplayInfo(station.music[station.playing]);

        log.StoreString("[WARDEN UpdatePlaying] Playing " + station.music[station.playing].name + " on " + station.name + ".\n");
        GD.Print("[WARDEN UpdatePlaying] Playing " + station.music[station.playing].name + " on " + station.name + ".");
    }
    
    private void InitialQueuePopulation(ref StationContext station){
        station.playing = (int)(GD.Randi() % station.music.Count);
        station.lastPlayed = station.playing;
        station.music[station.playing].skipChance = 1.0f;
        log.StoreString("[WARDEN InitialQueuePopulation] Playing " + station.music[station.playing].name + " on " + station.name + ".\n");
        GD.Print("[WARDEN InitialQueuePopulation] Playing " + station.music[station.playing].name + " on " + station.name + ".");

        for(int i = 0; i < queueSize; i++){
            PickToQueue(ref station);
        }
    }
    
    private void PickToQueue(ref StationContext station){
        do{
            int index = (int)(GD.Randi() % station.music.Count);
            if(index == station.playing || index == station.lastPlayed || station.queue.Contains(index)) continue;

            float skip = GD.Randf();
            if(station.music[index].skipChance >= 1.0f || skip <= station.music[index].skipChance){
                log.StoreString("[WARDEN PickToQueue] Skipping " + station.music[index].name + " w/ " + station.music[index].skipChance.ToString("0.0000") + " chance.\n");
                station.music[index].skipChance -= 1.0f / queueSize;
            }else{
                station.queue.Enqueue(index);
                station.music[index].skipChance = 1.0f;
                log.StoreString("[WARDEN PickToQueue] Queuing " + station.music[index].name + " on " + station.name + ".\n");
                return;
            }
        }while(true);
    }

    private void GetDisplayInfo(MusicContext music){
        completeText = [];
        completeText.Add(music.artist + " // " + music.name);
        completeText.Add(stations[stationIndex].name);
        textIndex = 1;
        infoDisplay.Text = completeText[textIndex];
        scroll = false;
        stilled = false;
        timer = 0.0d;
    }

    private void CreateAudioStream(string path){

    }
}
