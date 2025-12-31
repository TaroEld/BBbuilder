# BBBuilder

## Overview
This is a command-line application for Windows systems that helps in the development of mods for the game Battle Brothers.  
It has shortcuts to conduct most common tasks for developing mods: 
- Initialising a new mod by creating a folder structure and scaffolding to quickly start writing code
- Building the mod, which does some checks (compiling files for syntax errors), can optionally transpile JavaScript files to ES3, packs brush files, and copies it to the data folder  
- Relaunching the game.
It also comes with other useful features, such as convenient extracting of mods by other people.  

As a command line tool, it can be run with the windows cmd terminal, powershell or other means. 
To make things more convenient, the tool will create editor config files for VSCode and Sublime Text which setup projects and provide the basic build commands. VScode is entirely free, Sublime Text is free but will occasionally nag you to buy it.  
Shipped with this application is the Battle Brothers mod kit by Adam Milazzo: http://www.adammil.net/blog/v133_Battle_Brothers_mod_kit.html . This kit is needed for many basic operations.  There is also a modified version of the squirrel binary `sq_taro.exe` that is used for mass compiling.  

## How to install
To use this program, simply extract the .zip in a convenient folder. No further installing is needed.  
When on Windows, calling the .exe will cause it to add itself to PATH. If you move the folder, it will update to the new location.  
If you wish to use ES3 transpiling, you will need to install node.js and have it available in your %PATH% environment. Download link: https://nodejs.org/en/download  
Some optional features can only be used if you have git installed (so, available in your PATH / accessible from the command line). You can get it here: https://git-scm.com/downloads  . You should be using it when developing mods, anyways.  

## How to use
This is a command line tool / CLI. This means that the program will be called over a cool looking black hacker terminal, while passing it [commands](#commands) and other values.   
As that is often not very convenient, the tool will generate editor files for you, specifically for VSCode and Sublime Text. See here: [Editor config files](#editor-config-files) This will allow you to "build" your mod, as in create a new zip, copy it to data and relaunch the game, in one keybind press.  
Also, jump to [here](#example-usage) to get an idea of how that looks like.  

## Commands
Calling `bbbuilder.exe` without any other arguments will give you an overview of all the commands. Add -help for a full overview including the arguments and flags. Call `bbbuilder.exe <commandname>` for an overview of that command.  
The flags/parameters can be used with aliases. For most it's just the first character, but some have abbreviations due to conflicting first characters. The aliases are noted in the overview.    
 
### config
A number of config options are available to be added and changed with `config`. If you first start the program, it will prompt you to provide values for the `-datapath` and `-modpath` commands. Afterwards, it can be used to change values, or check the current configuration.
#### commands
- `-datapath <path>`: Set the path to the directory of the game to copy the .zip of the mod to and optionally (re)start the game.
- `-modpath <path>`: Set the path to the directory of your mods folder, where newly initialised or extracted mods will be placed by default.
- `-folders <folder1,[folder2],[...]>`: Comma-separated list **without spaces inbetween** of folders to be included in the editor config files (for example, adding the vanilla game files folder).
- `-movezip <true|false>`: Whether you'd like to delete the zip after building the mod and copying it to `datapath`. You will need to pass either `true` or `false`. Default is `false`.  
- `-usesteam <true|false>`: Whether BB should start via Steam. If this is set to true, the program will find the steam install location using the registry, and start the game using the steam API. Requires Windows. Primarly intended to be used with the BB launcher program.  
- `-verbose <true|false>`

### init \<modname\>
Create a new mod with the name `<modname>`. A folder structure is created, providing a light scaffold of folders and files. This speeds up the generation of new mods and provides consistency between your creations.  
By default, the mod will be initialised into the folder specified in the `modpath` config value, with the foldername \<modname\>.
The generated folders and files depend on the template used, see [Templates](#templates)
If `git` is available (in PATH), a git repository will be initialised.  
#### Flags
- `-directory <path>`: Specify another folder to place the new mod. Example: `init mod_my_first_mod -directory "C:\BB Modding\My_Mods\"` 
- `-template <templatename>`: Specify which template to use. 
\n Example: `bbbuilder init my_cool_mod -template ui`
- `-overwrite`: Indicate that you'd like to replace the files in an existing folder. Only files that exist in the template will be overwritten in the target folder. If this flag is not specified, the program will show you an error message if you specify a folder that already exists.

#### Templates
The template defines what files and folders will be created in the new mod directory. 
The default templates are found in the [Templates](Templates) folder within the .zip. 
You can customize these templates by either editing the existing ones, or adding new folders. You refer to them by their name.  	
Within the files and filenames, certain template strings are replaced: 

	- "$modname" -> <modname>
	- "$uppercase" -> Upper-case-modname
	- "$namespace" -> CamelCase modname, where underscores are removed and the following letter capitalised. Example: "mod_new_thing" -> "ModNewThing"

Example usage: `bbbuilder init mod_my_first_mod -template ui -overwrite`

### extract \<zipPath\>
Equal to the init command, but extracts existing mods instead, decompiling files if necessary. This is useful if you downloaded a mod from someone else, and would like to take a look. The other `init` flags can also be used here.
*** <zipPath>: Specify path of mod to extract. The file will be put in your specified 'mods' directory. (Example: bbuilder extract C:/Users/user/Desktop/mod_test.zip)
#### Flags
- `-overwrite`: Replace the files in an existing folder.
- `-name <newname>`: Renames the extracted mod.
- `-directory <path>`: Specify alternative path to extract the mod to.
Example usage: `bbbuilder extract "C:\Users\Taro\Downloads\mod_cool_things.zip" -directory "C:\BB Modding\Other_peoples_mods\"`

### build \<modname\>
The files of your mod are packed together to create a new zip. .nut files are compiled to test for syntax errors, and sprites are packed into brush files.  
To speed things up, the game will compare the hashes of the file(s) and only treat those that have been modified since the last time you packed a zip. To keep track of these hashes, a little json file is kept in the .bbbuilder folder of your mod.  
It will also go through an existing .zip (either in the mod folder, or that in /data if the `movezip` config is set to `true`) and check if files were removed.  
To avoid creating multiple copies of the same mod in the .data folder, the program will compare the names of existing zip files in data with the one we're currently building. If one is found that starts the same way (for example, mod_mymod and mod_mymod_1), you will be prompted to confirm to continue building or not. This choice can be remembered and saved in a text file within the mod folder.  l
You can force a complete rebuild of the mod with the `-rebuild` flag.  

#### Flags
- `-restart`: Exit and then start BattleBrothers.exe after building the mod.  
- `-rebuild`: Delete the database and the .zip to start from a clean slate.  
- `-diff <referencebranch>,<wipbranch>`: Create the zip based on the diff between <referencebranch> and <wipbranch> Pass them comma-separated WITHOUT SPACE INBETWEEN. This requires the `git` command to be available via cmd. The purpose of this is creating patches.
- `-zipname <name>`: Specify an alternative name for the created zip file.
- `-excludedfolders <folder1,folder2,...>`: comma-separated list of folders to be excluded from the zip file. All the other processes (compile .nut, pack brushes) will still be executed. The folder unpacked_brushes is always excluded. Useful for things like debug folders or separating assets and code.
- `-includedfolders <folder1,folder2,...>`: comma-separated list of folders to be included into the zip file. The purpose of this command is to force the inclusion of files that would otherwise be excluded via excludedfolders.
- `-transpile`: Translate js file to es3. It allows you to use modern js syntax and features to create your mod. Advanced feature, you probably don't need to worry about it.  

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
### Step 1: Download and set up the config
Download and extract the program in a folder, for example `C:\BB Modding\bbbuilder`.  
Open a terminal in this folder. A convenient way is to click the adress bar of file explorer within that folder, and write `cmd` or `ps`. You can also search for `cmd` or `powershell` in the windows search. I probably don't have to tell Linux nerds how this works.  
Write `bbbuilder.exe` into the terminal to launch the program.
As it's your first time using it, the program will ask for the `data` path of battle brothers.
For Steam users: 
- Right click the game in Steam, and select `Properties...`
- Click `Installed Files`
- Click `Browse...`
- The file explorer will open in your Battle Brothers installation. Enter the `data` folder, and copy the path.
- Paste it into the cmd terminal  
For GoG users: Find your install location.

In the next step, the game will ask for the mods folder, as in where your mods will be placed in the future. If you don't have one yet, create it, as it must be an existing folder. Example: `C:\BB_Modding\My_Mods`
Also copypaste that path into the terminal.

### Step 2: Initialize a mod
Now that we set up the config, we can intialise our first mod. Write `bbbuilder.exe init mod_my_first_mod`. It will be created in the modpath folder that we defined in the previous step. The result will be opened in file explorer.  
To open this project in VSCode or Sublime Text, doubleclick the `.vscode/mod_my_first_mod.code-workspace` or `mod_my_first_mod.sublime-project` file, respectively. I'll be going with the latter.  
After doubleclicking the `mod_my_first_mod.sublime-project`, sublime will open up. On the left-hand sidebar, you can see the folders of this project. As 
At this point, you can work on your mod. I won't be writing a modding tutorial here, so head on to the modding discord or other places.
To try out if you've set your tools up correctly, you can open the file `/scripts/!mods_preload/mod_my_first_mod.nut`, delete all the content, and write `::logInfo("Testing my_first_mod`.


### Step 3: Building a mod
When we talk of `Building`, we mean testing if it works (as much as possible without actually launching the game), creating a .zip of the file, copying it into the data folder, and optionally starting the game.
There are two options to build the mod:
#### Option 1: Manual command
From the command line we used earlier, we can write the command `bbbuilder.exe build C:\BB_Modding\My_Mods\mod_my_first_mod -restart`.
If everything goes right, you will see a few lines of details, and then the game should start.
#### Option 2: Editor tools
If you've set up the build commands in the [earlier step](#editor-config-files), you can also bring up the build options in your editor (ctrl+shift+b in sublime text).
The option `bb_build - Update Mod and launch` replicates the manual command from above.
