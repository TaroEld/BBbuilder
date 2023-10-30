# BBBuilder


## Overview
This is a command-line application for Windows systems that helps in the development of mods for the game Battle Brothers.  
Shipped with this application is the Battle Brothers mod kit by Adam Milazzo: http://www.adammil.net/blog/v133_Battle_Brothers_mod_kit.html 
This tool has shortcuts to conduct most common tasks for developing mods: 
- Initialising a new mod by creating a folder structure and scaffolding to quickly start writing code
- Building the mod, which does some checks (compiling files for syntax errors), can optionally transpile JavaScript files to ES3, packs brush files, and copies it to the data folder  
- Relaunching the game.

As a command line tool, it can be run with the windows cmd terminal. 
To make things more convenient, the tool will create editor config files for VSCode and Sublime Text which setup projects and provide the basic build commands.  

## How to install
To use this program, simply extract the .zip in a convenient folder. No further installing is needed.  
If you wish to use ES3 transpiling, you will need to install node.js and have it available in your %PATH% environment. Download link: https://nodejs.org/en/download

## Commands
Calling `bbbuilder.exe` without any other arguments will give you an overview of all the commands and parameters/flags. Calling `bbbuilder.exe <commandname>` will give you an overview of that command.  
 
### config
Saves config values in a database. If you first start the program, it will prompt you to provide values for the `datapath` and `modpath` commands. Afterwards, it can be used to change values, or check the current configuration.
#### commands
- `datapath <path>`: The directory of the game to copy the built mod and optionally (re)start the game
- `modpath <path>`: The directory of your mods folder, where newly initialised mods will be placed
- `folders <folder1,folder2,...>`: Comma-separated list of folders to be included in the editor config files (for example, adding the vanilla game files folder). 
- `movezip <true|false>`: Whether you'd like to delete the zip after building the mod and copying it to `datapath`. Apparently, some people don't like having the same .zip file in both places maybe they operate on 1.44 MB floppys.


### init \<modname\>
Create a new mod. with the name `<modname>` A folder structure is created, providing a light scaffold of folders and files. This speeds up the generation of new mods and provides consistency between your creations.  
By default, the mod will be initialised into the folder specified in the `modpath` config value, with the foldername \<modname\>.
The generated folders and files depend on the template used, see the `-template` flag. 
#### Flags
- `-altpath <path>`: Specify another folder to place the new mod. Example: `init mod_my_first_mod -altpath "C:\BB Modding\My_Mods\"` 
- `-template <templatename>`: Specify which template to use. The template defines what files and folders will be created in the new mod directory. The default templates are found in the `Templates` folder within the .zip. You can customize these templates by either editing the existing ones, or adding new folders.  
- `-replace`: Indicate that you'd like to replace the files in an existing folder. Only files that exist in the template will be overwritten in the target folder. If this flag is not specified, the program will show you an error message if you specify a folder that already exists.

Example usage: `bbbuilder init mod_my_first_mod`

### extract \<modpath\>
Equal to the init command, but extracts existing mods instead, decompiling files if necessary. This is useful if you downloaded a mod from someone else, and would like to take a look. The other `init` flags can also be used here.

Example usage: `bbbuilder extract "C:\Users\Taro\Downloads\mod_cool_things.zip" -altpath "C:\BB Modding\Other_peoples_mods\"`

### build
The files of your mod are packed together to create a new zip. .nut files are compiled to test for syntax errors, and sprites are packed into brush files. 
There are a number of flags that can be set to do only parts of the packing progress, for example, only compiling the .nut files to test for syntax errors:
- `-restart`: Exit and then restart BattleBrothers.exe after building the mod.  
- `-scriptonly`: Only pack script (`.nut`) files. The mod will have a '_scripts' suffix.
- `-compileonly`: Compile the `.nut` files without creating a .zip. This is used to check the syntax errors.
- `-uionly`: Only zip the gfx and ui folders. The mod will have a '_ui' suffix.
- `-nocompile`: Speed up the build by not compiling files.
- `-nopack`: Speed up the build by not repacking brushes.

Optionally, you can tell the program to transpile the JavaScript files to ES3. This is the JavaScript version the game uses. With this functionality, you can write modern JS code in your mods, using 'new' features such as `class`es.
 
## Editor config files
This program is best used with VSCode or Sublime Text, as it will generate editor config files for them.
Upon first and every subsequent use, config files for the editors Sublime Text 3 and Visual Studio Code are created. 
### Sublime Text 3 
The Sublime sublime-build file is copied to the %appdata% directory, and the build commands should be available right away.  
If you initialise or extract a mod, a `sublime-project` file will be placed in the mod folder. Use this with Sublime Text (drag it into the editor, or doubleclick the file) to open the mod as a project.
### VSCode 
The VSCode tasks.json file is created in the /tools folder of the program. It must be manually copied or inserted into an existing tasks.json file.  
See here: https://code.visualstudio.com/docs/editor/tasks#_user-level-tasks  
Like with Sublime Text, an editor project file will be created. This is placed in the `.vscode` folder.

## Example usage:
Download and extract the program in a folder, say `C:\BB Modding\bbbuilder`.  
Open cmd in this folder. A convenient way is to click the adress bar of file explorer, and write `cmd`.
Write `bbbuilder.exe` into the terminal. The program will ask for the data path of battle brothers.
For Steam users: 
- Right click the game in Steam, and select `Properties...`
- Click `Installed Files`
- Click `Browse...`
- The file explorer will open in your Battle Brothers installation. Enter the `data` folder, and copy the path.
- Paste it into the cmd terminal  
For GoG users: Find your install location.

In the next step, the game will ask for the mods folder, as in where your mods shall be placed in the future. If you don't have one yet, create it, as it must be an existing folder. 
Also copypaste that path into the terminal.

Now that we defined the basic paths, we can intialise our first mod. Write `bbbuilder.exe init mod_my_first_mod`. The resulting folder will be opened in file explorer.  
To open this project in VSCode or Sublime Text, doubleclick the `.vscode/mod_my_first_mod.code-workspace` or `mod_my_first_mod.sublime-project` file, respectively. I'll be going with the latter.  
After doubleclicking the `mod_my_first_mod.sublime-project`, sublime will open up. On the left-hand sidebar, you can see the folders of this project. As 
At this stage, the most intersting file is `/scripts/!mods_preload/mod_my_first_mod.nut`. A few things 

## Misc
It can be useful to add the program folder to the environment variables, to be able to call them from any cmd window. See here for example: https://linuxhint.com/add-directory-to-path-environment-variables-windows/
