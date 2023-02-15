## BBBuilder

This is a command-line application that helps in the development of mods for the game Battle Brothers.  
It is built on .net 5.0, but the releases are built using the self-contained option.  
Bundled with this application is the Battle Brothers mod kit by Adam Milazzo: http://www.adammil.net/blog/v133_Battle_Brothers_mod_kit.html  

To use this program, simply extract the .zip in a convenient location.  
Upon first and every subsequent use, config files for the editors Sublime Text 3 and Visual Studio Code are created.  
The Sublime sublime-build file is copied to the %appdata% directory, and the build commands should be available right away.  
The VSCode tasks.json file is created in the /tools folder. It must be manually copied or inserted into an existing tasks.json file.  
See here: https://code.visualstudio.com/docs/editor/tasks#_user-level-tasks  



The main commands are:  
- config: Save config values, such as the directory of the game, the directory of the mods that you're working on and folders to be included in new mods.
- init: To create a new mod. A folder structure is created, providing a light scaffold of folders and files.
- build: The files of your mod are packed together to create a new zip. .nut files are  compiled to test for syntax errors, and sprites are packed into brush files.
- extract: Equal to the init command, but extracts existing mods instead, decompiling files if necessary.  

Further details about the commands can be obtained by running the program without any parameters.

