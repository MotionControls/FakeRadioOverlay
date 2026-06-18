import os, json
import music_tag as mt
from pathlib import Path as pl
import ffmpeg

musicDir = "music/"
m3uDir = "m3u/"
stationDir = "stations/"

musicLen = 0
compList = []
compFile = open(stationDir + "comp.txt", "w")

fileList = os.listdir(m3uDir)
fileList = [f for f in fileList if os.path.isfile(m3uDir + f)]
for file in fileList:
    musicList = []
    playlist = open(m3uDir + file)
    station = open(stationDir + os.path.splitext(file)[0] + ".txt", "w")
    for line in playlist:
        line = os.path.basename(line.rstrip('\n'))
        basename, extension = os.path.splitext(line)
        if(extension != ".mp3"):
            print("Converting " + line + ".")
            stream = ffmpeg.input(musicDir + line)
            stream = ffmpeg.output(stream, musicDir + basename + ".mp3", loglevel="error", )
            stream = ffmpeg.overwrite_output(stream)
            ffmpeg.run(stream)
        
        index = musicLen
        if(basename not in compList):
            compList.append(basename)
            compFile.write(str(musicLen) + "//" + basename + ".mp3" + "\n")
            musicLen += 1
        else:
            index = compList.index(basename)
        station.write(str(index) + ",")
    station.write(";")
    station.close()
    playlist.close()