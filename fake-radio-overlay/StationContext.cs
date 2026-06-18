using Godot;
using System;
using System.Collections.Generic;

public class MusicContext
{
    public int index = 0;
    public float skipChance = 0.0f;

    public MusicContext(int _index){
        index = _index;
    }
}

public class StationContext
{
    public string name = "PLACEHOLDER";
    public string title = "TITLE PLACE";
    public string artist = "ARTIST PLACE";
    public float length = float.MaxValue;
    public double position = 0.0d;
    public List<MusicContext> music = [];
    public Queue<int> queue = [];
    public int queueSize = 3;
    public int playing = 0;
    public int lastPlayed = 0;

    public StationContext(string _name, int _queueSize = 3){
        name = _name;
        queueSize = _queueSize;
    }

    public void InitialQueuePopulation(){
        playing = (int)(GD.Randi() % music.Count);
        lastPlayed = playing;
        music[playing].skipChance = 1.0f;
        
        for(int i = 0; i < queueSize; i++){
            PickToQueue();
        }

        //log.StoreString("[WARDEN InitialQueuePopulation] Playing " + station.music[station.playing].name + " on " + station.name + ".\n");
        GD.Print("[WARDEN InitialQueuePopulation] Playing " + title + " on " + name + ".");
    }

    public void PickToQueue(){
        do{
            int index = (int)(GD.Randi() % music.Count);
            if(index == playing || index == lastPlayed || queue.Contains(index)) continue;

            float skip = GD.Randf();
            if(music[index].skipChance >= 1.0f || skip <= music[index].skipChance){
                //log.StoreString("[WARDEN PickToQueue] Skipping " + music[index].name + " w/ " + music[index].skipChance.ToString("0.0000") + " chance.\n");
                music[index].skipChance -= 1.0f / queueSize;
            }else{
                queue.Enqueue(index);
                music[index].skipChance = 1.0f;
                //log.StoreString("[WARDEN PickToQueue] Queuing " + title + " on " + name + ".\n");
                return;
            }
        }while(true);
    }

    public void ProcessSongInfo(string path){
        GDScript tagScript = GD.Load<GDScript>("res://addons/Id3TagParser/MP3ID3Tag.gd");
        GodotObject tagReader = (GodotObject)tagScript.New();
        tagReader.Set("stream", Godot.FileAccess.GetFileAsBytes(path));
        title = (string)tagReader.Call("getTrackName");
        artist = (string)tagReader.Call("getArtist");
    }
}