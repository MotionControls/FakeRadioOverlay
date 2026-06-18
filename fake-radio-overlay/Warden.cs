using Godot;
using System;
using System.IO;
using System.Collections.Generic;

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
    private List<string> music = [];

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

        Godot.FileAccess file = Godot.FileAccess.Open(path + stationPath + "comp.txt", Godot.FileAccess.ModeFlags.Read);
        while(file.GetPosition() < file.GetLength()){
            string[] line = file.GetLine().Split("//");
            music.Add(musicPath + line[1]);
        }
        
        foreach(string filePath in dir.GetFiles()){
            if(Path.GetExtension(filePath) == ".txt"){
                file = Godot.FileAccess.Open(path + stationPath + filePath, Godot.FileAccess.ModeFlags.Read);
                string stationName = Path.GetFileNameWithoutExtension(filePath);
                if(stationName != "comp"){
                    StationContext station = new StationContext(stationName);
                    foreach(string strIndex in file.GetLine().Split(',')){
                        station.music.Add(new MusicContext(strIndex.ToInt()));
                    }

                    station.InitialQueuePopulation();
                    station.ProcessSongInfo(music[station.music[station.playing].index]);
                    stations.Add(station);
                }
            }
        }

        file.Close();

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
                    if(station.position >= station.length){
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
        station.ProcessSongInfo(music[station.music[station.playing].index]);

        log.StoreString("[WARDEN SongFromQueue] Playing " + station.title + " on " + station.name + ".\n");
        GD.Print("[WARDEN SongFromQueue] Playing " + station.title + " on " + station.name + ".");

        station.PickToQueue();
    }

    private void UpdatePlaying(StationContext station){
        AudioStreamMP3 stream = new AudioStreamMP3();
        stream.Data = Godot.FileAccess.GetFileAsBytes(music[station.music[station.playing].index]);
        player.Stream = stream;
        player.Play((float)station.position);

        log.StoreString("[WARDEN UpdatePlaying] Playing " + station.title + " on " + station.name + ".\n");
        GD.Print("[WARDEN UpdatePlaying] Playing " + station.title + " on " + station.name + ".");
    }
    
    private void GetDisplayInfo(){
        completeText = [];
        completeText.Add(stations[stationIndex].artist + " // " + stations[stationIndex].title);
        completeText.Add(stations[stationIndex].name);
        textIndex = 1;
        infoDisplay.Text = completeText[textIndex];
        scroll = false;
        stilled = false;
        timer = 0.0d;
    }
}
