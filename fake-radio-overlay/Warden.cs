using Godot;
using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;

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

        AudioStream temp = AudioStreamMP3.LoadFromFile(path);
        length = temp.GetLength();
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
    [Export] public bool editor = true;
    [Export] public string logPath = "logs/";
    private Godot.FileAccess log;
    
    // Text display.
    [Export] public RichTextLabel infoDisplay;
    [Export] public int charLimit = 11;
    private string displayedText = "";
    private string completeText = "";
    private double timer = 0.0d;        // General-use timer.
    private double stepTime = 0.5d;     // Time until text scroll.
    private double infoTime = 3.0d;     // Time until swapping information.

    // Stations.
    [Export] public string stationPath = "stations/";
    [Export] public int queueSize = 3;
    private List<StationContext> stations = new List<StationContext>();
    private int stationIndex = 0;

    // Music.
    [Export] public string musicPath = "music/";

    public override void _Ready(){
        base._Ready();
        GD.Randomize();
        
        string path = (editor) ? "res://" : Path.GetDirectoryName(OS.GetExecutablePath());
        GD.Print(path + logPath + DateTime.Now.ToString("d.MMM.yyyy-h.mm.ss") + ".log");
        log = Godot.FileAccess.Open(path + logPath + DateTime.Now.ToString("d.MMM.yyyy-h.mm.ss") + ".log", Godot.FileAccess.ModeFlags.Write);
        if(log == null) GD.PrintErr("Log file could not be opened.");
        log.StoreString("...Init...\n");
        
        DirAccess dir = DirAccess.Open(path + stationPath);
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
    }

    
    public override void _PhysicsProcess(double delta){
        for(int i = 0; i < stations.Count; i++){
            StationContext station = stations[i];
            
            if(stationIndex != i){
                station.position += delta;
                if(station.position >= station.music[station.playing].length){
                    SongFromQueue(ref station);
                }
            }else{
                // TODO: Get info from current song.
            }
        }
    }

    private void SongFromQueue(ref StationContext station){
        station.lastPlayed = station.playing;
        station.playing = station.queue.Dequeue();
        PickToQueue(ref station);
    }
    
    private void InitialQueuePopulation(ref StationContext station){
        station.playing = (int)(GD.Randi() % station.music.Count);
        station.lastPlayed = station.playing;
        station.music[station.playing].skipChance = 1.0f;
        log.StoreString("[WARDEN InitialQueuePopulation] Playing " + station.music[station.playing].name + " on " + station.name + ".\n");
        
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
        completeText = music.artist + " // " + music.name;
    }
}
