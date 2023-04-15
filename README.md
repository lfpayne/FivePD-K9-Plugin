# FivePD-K9-Plugin
A K9 Script that interacts with FivePD to allow the use of information loaded by FivePD for Pedestrians and Vehicles. This Script allows for a Bomb Sniffing K9 and a Drug Sniffing K9.

# Set Up
Place the compiled .dll file in the **fivepd/plugins/k9/** folder along with the config.json file provided in this github.
Additionally the fivepd/fxmainfest.lua file may need to be edited to allow for additional folders in the fivepd/plugins folder.

# Plugin Load Error
This error will occur when the player loads into the game and the K9 plugin attempts to load. If you are getting this you did not edit the fivepd/fxmanifest.lua file. The file must be edited to include searching within folders in the fivepd/plugins folder. To add this functionality add the line ./plugins/\**/*.json somewhere around line 20.
