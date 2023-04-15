# FivePD-K9-Plugin
A K9 Script that interacts with FivePD to allow the use of information loaded by FivePD for Pedestrians and Vehicles. This Script allows for a Bomb Sniffing K9 and a Drug Sniffing K9.

# Set Up
Place the compiled .dll file in the fivepd/plugins/k9/ folder along with the config.json file provided in this github.


Additionally the fivepd/fxmainfest.lua file will need to be edited to allow for additional folders in the fivepd/plugins folder. The line "'./plugins/**/*.json'," must be added somewhere around line 20.
