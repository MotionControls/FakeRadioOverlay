import os, sys
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
    print("Processing " + file + ".")
    
    musicList = []
    playlist = open(m3uDir + file)
    station = open(stationDir + os.path.splitext(file)[0] + ".txt", "w")
    toWrite = []
    
    for line in playlist:
        line = os.path.basename(line.rstrip('\n'))
        basename, extension = os.path.splitext(line)
        if(extension != ".mp3"):
            print("Converting " + line + ".")
            stream = ffmpeg.input(musicDir + line)
            stream = ffmpeg.output(stream, musicDir + basename + ".mp3", loglevel="quiet", n=None)
            ffmpeg.run(stream)
        
        index = musicLen
        if(basename not in compList):
            print("Adding " + basename + " to comp.")
            compList.append(basename)
            compFile.write(str(musicLen) + "//" + basename + ".mp3" + "\n")
            musicLen += 1
        else:
            index = compList.index(basename)
        
        print("Adding " + basename + " to station.")
        toWrite.append(str(index))
    
    for i in range(len(toWrite)):
        station.write(toWrite[i])
        if(i < len(toWrite) - 1): station.write(",")
    
    print("Finished " + file + ".")
    station.close()
    playlist.close()