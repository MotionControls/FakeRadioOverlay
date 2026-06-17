import os, json
import music_tag as mt
from pathlib import Path as pl

musicDir = "music/"
m3uDir = "m3u/"
stationDir = "stations/"

fileList = os.listdir(m3uDir)
fileList = [f for f in fileList if os.path.isfile(m3uDir + f)]
for file in fileList:
    jsonArr = []
    playlist = open(m3uDir + file)
    for line in playlist:
        line = line.rstrip('\n')
        #print(os.path.splitext(line)[1])
        if(os.path.splitext(line)[1] == ".flac"): continue
        song = mt.load_file((musicDir + os.path.basename(line)))
        jsonArr.append(json.dumps({
            "path": os.path.basename(line),
            "name": str(song["title"]),
            "artist": str(song["artist"]).split(";")[0],
            "album": str(song["album"])
        }))
    station = open(stationDir + os.path.splitext(file)[0] + ".json", "w")
    station.write("[")
    for i in range(len(jsonArr)):
        station.write(jsonArr[i])
        if(i < len(jsonArr) - 1):
            station.write(",")
    station.write("]")
    station.close()
    playlist.close()