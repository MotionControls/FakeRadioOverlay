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
    }

    public void PickToQueue(){
        do{
            int index = (int)(GD.Randi() % music.Count);
            if(index == playing || index == lastPlayed || queue.Contains(index)) continue;

            float skip = GD.Randf();
            if(music[index].skipChance >= 1.0f || skip <= music[index].skipChance){
                GD.Print("[StationContext PickToQueue] Skipping " + music[index].index + " w/ " + music[index].skipChance.ToString("0.0000") + " chance.");
                music[index].skipChance -= 1.0f / queueSize;
            }else{
                queue.Enqueue(index);
                music[index].skipChance = 1.0f;
                GD.Print("[StationContext PickToQueue] Queuing " + music[index].index + " on " + name + ".");
                return;
            }
        }while(true);
    }

    public void ProcessSongInfo(string path){
        TagLib.File file = TagLib.File.Create(path);
        title = file.Tag.Title;
        artist = file.Tag.Performers[0];
        length = (float)file.Properties.Duration.TotalSeconds;
        GD.Print("[StationContext ProcessSongInfo] Retrieved " + title + ", " + artist + ", " + length.ToString("0.00") + " for " + name + ".");
    }
}